// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation;

/// <summary>
/// Zeichnet ein <see cref="Kartenbild"/> auf eine Zeichenfläche (nicht im Original): Bundesländer, Städte, Fahrten und
/// Spielorte – dieselbe Zeichnung für die Ansicht „Kompaktspieltage“ und für den Ausdruck (Peter, 14.10.2026).
/// </summary>
/// <param name="f">Die Zeichenfläche.</param>
public sealed class Kartenzeichner(IZeichenflaeche f)
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Weiß der Länder, der Ränder um Hallen und Pfeile und des Hofs um die Namen (Ton „Flaeche“).</summary>
    private static readonly Farbe Hell = Farbe.Weiss;

    /// <summary>Grenzen und Namen der Bundesländer (Ton „Rand“ der Oberfläche).</summary>
    private static readonly Farbe Grenze = new(0xD6, 0xD3, 0xCB);

    /// <summary>Gelb der Einzelbegegnungen, wie in der Planmatrix.</summary>
    private static readonly Farbe EinzelGelb = new(0xEA, 0xB3, 0x08);

    /// <summary>Zweiter Spieltag am selben Sonntag (wie ★/● im Raster).</summary>
    private static readonly Farbe ZweiterSpieltag = new(0xC2, 0x41, 0x0C);

    /// <summary>Dritter (oder weiterer) Spieltag am selben Sonntag.</summary>
    private static readonly Farbe DritterSpieltag = new(0x7E, 0x22, 0xCE);

    /// <summary>Holt den Hintergrund der Karte (Ton „Schwebe“ der Oberfläche).</summary>
    public static Farbe Hintergrund { get; } = new(0xF0, 0xEF, 0xEA);

    /// <summary>Zeichnet die Karte in die angegebene Größe (die Breite bestimmt den Maßstab).</summary>
    /// <param name="bild">Das Bild.</param>
    /// <param name="breite">Breite der Zeichnung.</param>
    /// <param name="hoehe">Höhe der Zeichnung (für den Maßstab unten).</param>
    public void Zeichnen(Kartenbild bild, double breite, double hoehe)
    {
        ArgumentNullException.ThrowIfNull(bild);
        if (bild.Breite <= 0 || breite <= 0)
        {
            return;
        }

        double s = breite / bild.Breite;
        foreach (IReadOnlyList<Kartenpunkt> flaeche in bild.Flaechen.Where(x => x.Count > 2))
        {
            f.Vieleck(flaeche.Select(p => (p.X * s, p.Y * s)).ToList(), Hell, Grenze, 1, geschlossen: true);
        }

        foreach (Kartenbeschriftung name in bild.Laender)
        {
            string text = name.Text.ToUpper(Deutsch);
            f.Text(text, (name.Punkt.X * s) - (f.TextBreite(text, 11, false) / 2), (name.Punkt.Y * s) - (11 * 1.31 / 2), 11, Grenze, false);
        }

        // Namen erst hier setzen: Ihre Breite hängt von der gezeichneten Größe ab, nicht vom Maßstab der Karte.
        (Dictionary<Kartenhalle, Hallenname> namen, List<Kartenbeschriftung> staedte, Legende? legende) = Beschriften(bild, s, breite, hoehe);
        foreach (Kartenbeschriftung stadt in staedte)
        {
            f.Rechteck((stadt.Punkt.X * s) - 2.5, (stadt.Punkt.Y * s) - 2.5, 5, 5, Farbe.Tinte2);
            f.Text(stadt.Text, (stadt.Punkt.X * s) + 6, (stadt.Punkt.Y * s) - 8, 11, Farbe.Tinte2, false);
        }

        // Gerade Linien; mehrere Fahrten zwischen denselben Hallen liegen nebeneinander. Kurze Fahrten, die unter den
        // Hallenpunkten verschwänden, kommen erst nach den Hallen in den Vordergrund.
        foreach (Kartenfahrt fahrt in bild.Fahrten.Where(x => !Kurz(x, s)))
        {
            if (Geteilt(bild, fahrt))
            {
                HalbeFahrt(fahrt, s);
                if (fahrt.Versatz > 0 && Gleichfarbig(bild, fahrt))
                {
                    // Zwei Fahrten in derselben Farbe (z. B. zweimal zum selben Ausrichter): eine helle Mittellinie trennt die
                    // Hälften, sonst sähe es aus wie eine Fahrt.
                    Trennlinie(fahrt, s);
                }
            }
            else
            {
                Fahrt(fahrt, s, umriss: false);
            }
        }

        foreach (Kartenhalle halle in bild.Hallen)
        {
            using (f.Deckkraft(halle.Ort.Gedimmt ? 0.45 : 1))
            {
                HallePunkt(bild, halle, s);
                Hallenname name = namen[halle];
                if (name.Leitlinie)
                {
                    Leitlinie(halle, name, s);
                }

                f.TextMitRand(name.Text, name.X + 2, name.Y, 12, Farbe.Tinte, true, Hell, 3);
            }
        }

        if (legende is not null)
        {
            LegendeZeichnen(legende);
        }

        // Kurze Fahrten im Vordergrund mit hellem Umriss.
        foreach (Kartenfahrt fahrt in bild.Fahrten.Where(x => Kurz(x, s)))
        {
            Fahrt(fahrt, s, umriss: true);
        }

        // Pfeilspitzen am Rand des Ziels zeigen die Richtung der Fahrt (bei kurzen Fahrten fehlt der Platz).
        foreach (Kartenfahrt fahrt in bild.Fahrten.Where(x => x.Kraeftig && !Kurz(x, s)))
        {
            if (Geteilt(bild, fahrt))
            {
                // Mehrere Fahrten zum selben Ziel: ein gemeinsamer Pfeil, jede Fahrt ihre Hälfte in ihrer Farbe.
                HalberPfeil(Linie(fahrt with { Versatz = 0 }, s), Math.Sign(fahrt.Versatz), Linienfarbe(fahrt));
                if (fahrt.Versatz > 0 && Gleichfarbig(bild, fahrt))
                {
                    PfeilTrennen(Linie(fahrt with { Versatz = 0 }, s));
                }
            }
            else
            {
                Pfeil(Linie(fahrt, s), Linienfarbe(fahrt));
            }
        }

        if (bild.MassstabKm > 0)
        {
            double y = hoehe - 14;
            f.Linie(14, y, 14 + (bild.MassstabBreite * s), y, Farbe.Tinte2, 2);
            f.Text(bild.MassstabKm.ToString(Deutsch) + " km", 14, y - 17, 10.5, Farbe.Tinte2, false);
        }
    }

    /// <summary>
    /// Anfang und Ende einer Fahrt in Bildpunkten, quer zur Fahrtrichtung um ihren Versatz verschoben: 2,2 Pixel je Stufe,
    /// sodass mehrere Fahrten zwischen denselben Hallen lückenlos nebeneinander liegen wie ein zweifarbiger Pfeil.
    /// </summary>
    private static (V Von, V Nach) Linie(Kartenfahrt fahrt, double s)
    {
        var von = new V(fahrt.Von.X * s, fahrt.Von.Y * s);
        var nach = new V(fahrt.Nach.X * s, fahrt.Nach.Y * s);
        V richtung = nach - von;
        double laenge = richtung.Laenge;
        if (fahrt.Versatz == 0 || laenge < 1)
        {
            return (von, nach);
        }

        V quer = new V(-richtung.Y / laenge, richtung.X / laenge) * (2.2 * fahrt.Versatz);
        return (von + quer, nach + quer);
    }

    /// <summary>Ob eine Fahrt so kurz ist, dass die Hallenpunkte sie verdecken würden.</summary>
    private static bool Kurz(Kartenfahrt fahrt, double s) => (new V(fahrt.Nach.X - fahrt.Von.X, fahrt.Nach.Y - fahrt.Von.Y) * s).Laenge < 30;

    /// <summary>Ob eine Fahrt eine von genau zwei kräftigen Fahrten in dieselbe Richtung ist (dann ein Pfeil aus zwei Hälften).</summary>
    private static bool Geteilt(Kartenbild bild, Kartenfahrt fahrt) =>
        fahrt.Kraeftig && bild.Fahrten.Count(x => x.Kraeftig && x.Von == fahrt.Von && x.Nach == fahrt.Nach) == 2;

    /// <summary>Ob die zweite Fahrt derselben Strecke dieselbe Farbe hat (dann trennt eine helle Linie die Hälften).</summary>
    private static bool Gleichfarbig(Kartenbild bild, Kartenfahrt fahrt) =>
        bild.Fahrten.Any(x => x != fahrt && x.Kraeftig && x.Von == fahrt.Von && x.Nach == fahrt.Nach && Linienfarbe(x) == Linienfarbe(fahrt));

    /// <summary>
    /// Die Farbe einer Fahrt wie ihre Punkte im Raster der Kompaktspieltage: Einzelbegegnung gelb, sonst nach dem Spieltag am
    /// Sonntag schwarz, orangerot, violett; ohne Spieltag blau.
    /// </summary>
    private static Farbe Linienfarbe(Kartenfahrt fahrt) => (fahrt.Einzel, fahrt.Spieltag) switch
    {
        (true, _) => EinzelGelb,
        (_, 0) => Farbe.Tinte,
        (_, 1) => ZweiterSpieltag,
        (_, >= 2) => DritterSpieltag,
        _ => Farbe.Akzent,
    };

    private static (double X, double Y) P(V v) => (v.X, v.Y);

    /// <summary>Ein Kreisausschnitt ab einem Winkel (Grad, im Uhrzeigersinn auf dem Bildschirm) als Vieleck.</summary>
    private static List<(double X, double Y)> Sektor(V mitte, double r, double anfang, double weite)
    {
        var punkte = new List<(double X, double Y)> { P(mitte) };
        int schritte = Math.Max(2, (int)Math.Ceiling(weite / 6));
        for (int i = 0; i <= schritte; i++)
        {
            double grad = (anfang + (weite * i / schritte)) * Math.PI / 180;
            punkte.Add((mitte.X + (r * Math.Cos(grad)), mitte.Y + (r * Math.Sin(grad))));
        }

        return punkte;
    }

    /// <summary>Die nahen Lagen eines Namens der Breite <paramref name="w"/> am Punkt: rechts, links, schräg darüber oder darunter, darüber, darunter.</summary>
    private static (double X, double Y)[] NaheLagen(double x, double y, double w, double h) =>
    [
        (x + 10, y - 10), (x - 10 - w, y - 10), (x + 9, y - 20), (x + 9, y + 2), (x - 9 - w, y - 20), (x - 9 - w, y + 2),
        (x - (w / 2), y + 9), (x - (w / 2), y - 9 - h),
    ];

    /// <summary>Weiter entfernte Lagen in acht Richtungen (mit Hinweislinie zum Punkt), zuerst die näheren.</summary>
    private static IEnumerable<(double X, double Y)> FerneLagen(double x, double y, double w, double h)
    {
        foreach (double d in new[] { 28.0, 44.0, 62.0 })
        {
            double e = d * 0.7;
            yield return (x + d, y - (h / 2));
            yield return (x - d - w, y - (h / 2));
            yield return (x + e, y - e - h);
            yield return (x + e, y + e);
            yield return (x - e - w, y - e - h);
            yield return (x - e - w, y + e);
            yield return (x - (w / 2), y - d - h);
            yield return (x - (w / 2), y + d);
        }
    }

    /// <summary>
    /// Setzt die Namen der Hallen und Städte mit ihrer gemessenen Breite so, dass sie ganz im Bild liegen und sich nicht
    /// überdecken: nah am Punkt (rechts, links, schräg, darüber, darunter), sonst weiter weg mit einer Hinweislinie. Findet
    /// ein voller Mannschaftsname nirgends Platz (dichte Stadtklassen wie in Hamburg), steht am Punkt eine Nummer und der
    /// Name in einer Legende in einer freien Ecke (Peter, 14.10.2026). Hallen der Auswahl zuerst, gedimmte zuletzt; Städte
    /// nur, wo dann noch Platz ist.
    /// </summary>
    private (Dictionary<Kartenhalle, Hallenname> Hallen, List<Kartenbeschriftung> Staedte, Legende? Legende) Beschriften(Kartenbild bild, double s, double breite, double hoehe)
    {
        const double H = 16;
        var belegt = new List<(double L, double O, double R, double U)>();
        bool Frei((double L, double O, double R, double U) b) =>
            b.L >= 2 && b.O >= 2 && b.R <= breite - 2 && b.U <= hoehe - 2 && !belegt.Exists(c => b.L < c.R && b.R > c.L && b.O < c.U && b.U > c.O);
        belegt.AddRange(bild.Hallen.Select(h => ((h.Punkt.X * s) - 9, (h.Punkt.Y * s) - 9, (h.Punkt.X * s) + 9, (h.Punkt.Y * s) + 9)));
        if (bild.MassstabKm > 0)
        {
            belegt.Add((8, hoehe - 34, 24 + (bild.MassstabBreite * s), hoehe - 6));
        }

        (double X, double Y)? Erste(IEnumerable<(double X, double Y)> lagen, double w) =>
            lagen.Where(l => Frei((l.X, l.Y, l.X + w, l.Y + H))).Select(l => ((double X, double Y)?)l).FirstOrDefault();

        var hallen = new Dictionary<Kartenhalle, Hallenname>();
        var nummeriert = new List<(string Nummer, string Name)>();
        foreach (Kartenhalle halle in bild.Hallen.OrderBy(h => h.Ort.Gedimmt).ThenByDescending(h => h.Ort.Hervorgehoben))
        {
            double x = halle.Punkt.X * s;
            double y = halle.Punkt.Y * s;
            string text = Textkuerzung.Kuerzen(f, halle.Ort.Beschriftung, Textkuerzung.Namensbreite, 12, true);
            double w = f.TextBreite(text, 12, true) + 6;
            (double X, double Y)? lage = Erste(NaheLagen(x, y, w, H), w);
            bool leitlinie = false;
            if (lage is null)
            {
                lage = Erste(FerneLagen(x, y, w, H), w);
                leitlinie = lage is not null;
            }

            if (lage is null)
            {
                // Kein Platz für den Namen: eine Nummer am Punkt, der Name steht in der Legende.
                text = (nummeriert.Count + 1).ToString(Deutsch);
                nummeriert.Add((text, Textkuerzung.Kuerzen(f, halle.Ort.Beschriftung, Textkuerzung.Namensbreite, 11, false)));
                w = f.TextBreite(text, 12, true) + 6;
                lage = Erste(NaheLagen(x, y, w, H), w)
                    ?? (Math.Clamp(x + 9, 2, Math.Max(2, breite - 2 - w)), Math.Clamp(y - 10, 2, Math.Max(2, hoehe - 2 - H)));
            }

            (double lx, double ly) = lage.Value;
            belegt.Add((lx, ly, lx + w, ly + H));
            hallen[halle] = new Hallenname(text, lx, ly, w, H, leitlinie);
        }

        Legende? legende = null;
        if (nummeriert.Count > 0)
        {
            const double Zeile = 15;
            double lw = nummeriert.Max(n => f.TextBreite(n.Name, 11, false)) + 34;
            double lh = (nummeriert.Count * Zeile) + 10;
            (double X, double Y)[] ecken = [(6, 6), (breite - lw - 6, 6), (breite - lw - 6, hoehe - lh - 6), (6, hoehe - lh - 40)];
            int Ueberdeckt((double X, double Y) e) => belegt.Count(c => e.X < c.R && e.X + lw > c.L && e.Y < c.U && e.Y + lh > c.O);
            (double ex, double ey) = ecken.MinBy(Ueberdeckt);
            belegt.Add((ex, ey, ex + lw, ey + lh));
            legende = new Legende(ex, ey, lw, lh, nummeriert);
        }

        var staedte = new List<Kartenbeschriftung>();
        foreach (Kartenbeschriftung stadt in bild.Staedte)
        {
            double x = stadt.Punkt.X * s;
            double y = stadt.Punkt.Y * s;
            (double L, double O, double R, double U) box = (x - 4, y - 9, x + 8 + f.TextBreite(stadt.Text, 11, false), y + 7);
            if (Frei(box))
            {
                belegt.Add(box);
                staedte.Add(stadt);
            }
        }

        return (hallen, staedte, legende);
    }

    /// <summary>Dünne Linie vom Rand des Hallenpunkts zur nächsten Stelle des weiter entfernten Namens.</summary>
    private void Leitlinie(Kartenhalle halle, Hallenname name, double s)
    {
        var mitte = new V(halle.Punkt.X * s, halle.Punkt.Y * s);
        var ziel = new V(Math.Clamp(mitte.X, name.X + 2, name.X + name.Breite - 2), Math.Clamp(mitte.Y, name.Y + 2, name.Y + name.Hoehe - 2));
        V r = ziel - mitte;
        double laenge = r.Laenge;
        if (laenge < 10)
        {
            return;
        }

        V anfang = mitte + (r * (8 / laenge));
        f.Linie(anfang.X, anfang.Y, ziel.X, ziel.Y, Farbe.Tinte2, 0.8);
    }

    /// <summary>Kasten mit den Namen der nummerierten Hallen.</summary>
    private void LegendeZeichnen(Legende l)
    {
        f.Vieleck([(l.X, l.Y), (l.X + l.Breite, l.Y), (l.X + l.Breite, l.Y + l.Hoehe), (l.X, l.Y + l.Hoehe)], Hell, Grenze, 1, geschlossen: true);
        for (int i = 0; i < l.Eintraege.Count; i++)
        {
            double y = l.Y + 5 + (i * 15);
            (string nummer, string name) = l.Eintraege[i];
            f.Text(nummer, l.X + 8, y, 11, Farbe.Tinte, true);
            f.Text(name, l.X + 28, y, 11, Farbe.Tinte, false);
        }
    }

    /// <summary>
    /// Zeichnet den Punkt einer Halle. In einer Auswahl ist der Ausgangspunkt der Fahrten grün, ein Ziel in der Farbe seiner
    /// Linie; kommen dort verschieden gefärbte Linien an (z. B. 3er-Spieltag und Einzelbegegnung), wird der Kreis im Winkel der
    /// Linie in Halbkreise (bzw. Sektoren) je Linie geteilt.
    /// </summary>
    private void HallePunkt(Kartenbild bild, Kartenhalle halle, double s)
    {
        var mitte = new V(halle.Punkt.X * s, halle.Punkt.Y * s);
        List<Kartenfahrt> ankommend = bild.Fahrten.Where(x => x.Kraeftig && x.Nach == halle.Punkt).OrderByDescending(x => x.Versatz).ToList();
        bool start = !halle.Ort.Hervorgehoben && bild.Fahrten.Any(x => x.Kraeftig && x.Von == halle.Punkt);
        if (!halle.Ort.Hervorgehoben || ankommend.Count == 0)
        {
            f.Kreis(mitte.X, mitte.Y, start ? 8 : 5.5, start ? Farbe.Gut : Farbe.Akzent, Hell, 2);
            return;
        }

        const double Radius = 8;

        // Geteilt wird bei verschieden gefärbten Linien und bei mehreren Fahrten vom selben Ausgangspunkt (zweimal zum selben
        // Ausrichter) – sonst sähe man nur eine.
        bool mehrereVonEinem = ankommend.GroupBy(x => x.Von).Any(g => g.Count() > 1);
        if (ankommend.Select(Linienfarbe).Distinct().Count() == 1 && !mehrereVonEinem)
        {
            f.Kreis(mitte.X, mitte.Y, Radius, Linienfarbe(ankommend[0]), Hell, 2);
            return;
        }

        // Teilung im Winkel der Linie: die Linie mit dem größten Versatz (links der Fahrtrichtung) bekommt die linke Hälfte.
        (V von, V nach) = Linie(ankommend[0], s);
        double winkel = Math.Atan2(nach.Y - von.Y, nach.X - von.X) * 180 / Math.PI;
        double teil = 360.0 / ankommend.Count;
        for (int i = 0; i < ankommend.Count; i++)
        {
            f.Vieleck(Sektor(mitte, Radius, winkel + (i * teil), teil), Linienfarbe(ankommend[i]), null, 0, geschlossen: true);
        }

        // Dünne helle Linie an den Grenzen der Hälften (bzw. Ausschnitte).
        for (int i = 0; i < ankommend.Count; i++)
        {
            double grad = (winkel + (i * teil)) * Math.PI / 180;
            f.Linie(mitte.X, mitte.Y, mitte.X + (Radius * Math.Cos(grad)), mitte.Y + (Radius * Math.Sin(grad)), Hell, 1);
        }

        f.Kreis(mitte.X, mitte.Y, Radius, null, Hell, 2);
    }

    /// <summary>
    /// Zeichnet eine Fahrt. Kurze Fahrten (über den Hallenpunkten) als Pfeil vom Rand des Ausgangspunkts zur Mitte des Ziels
    /// mit seitlich hellem Umriss.
    /// </summary>
    private void Fahrt(Kartenfahrt fahrt, double s, bool umriss)
    {
        double breite = fahrt.Kraeftig ? 2.5 : 1.5;
        (V von, V nach) = Linie(fahrt, s);
        Farbe farbe = Linienfarbe(fahrt);
        using (f.Deckkraft(fahrt.Kraeftig ? 1 : 0.35))
        {
            V r = nach - von;
            double laenge = r.Laenge;
            if (!umriss)
            {
                // Mit Pfeil (in der Auswahl) endet die Linie am Fuß der Spitze, damit Spitze bzw. Pfeilhälften sauber für sich
                // stehen.
                V ende = fahrt.Kraeftig && laenge > 30 ? nach - (r * (15.5 / laenge)) : nach;
                f.Linie(von.X, von.Y, ende.X, ende.Y, farbe, breite);
                return;
            }

            if (laenge < 1)
            {
                return;
            }

            V t = r / laenge;
            var quer = t.Links;

            // Vom Rand der farbigen Fläche des Ausgangspunkts (Radius 7 innerhalb des hellen Rands) bis knapp vor die Mitte des
            // Ziels; heller Umriss seitlich an Schaft und Spitze und ein minimaler am Fuß, nicht über die Spitze hinaus.
            double ab = Math.Min(7, laenge / 2);
            V anfang = von + (t * ab);
            double kopf = Math.Min(7, (laenge - ab - 1.5) * 0.6);
            V spitze = nach - (t * 1.5);
            V basis = spitze - (t * kopf);
            V links = basis + (quer * 3.3);
            V rechts = basis - (quer * 3.3);
            V fuss = anfang - t;
            f.Linie(fuss.X, fuss.Y, basis.X, basis.Y, Hell, breite + 2);
            f.Vieleck([P(links), P(spitze), P(rechts)], null, Hell, 2, geschlossen: false);
            f.Linie(anfang.X, anfang.Y, basis.X, basis.Y, farbe, breite);
            f.Vieleck([P(spitze), P(links), P(rechts)], farbe, null, 0, geschlossen: true);
        }
    }

    /// <summary>Kleine Pfeilspitze am Rand des Zielpunkts, in Richtung der Linie.</summary>
    private void Pfeil((V Von, V Nach) linie, Farbe farbe)
    {
        V r = linie.Nach - linie.Von;
        double laenge = r.Laenge;
        if (laenge < 1)
        {
            return;
        }

        V t = r / laenge;
        var quer = t.Links;
        V spitze = linie.Nach - (t * 9.5);
        V basis = spitze - (t * 7);
        f.Vieleck([P(spitze), P(basis + (quer * 3.8)), P(basis - (quer * 3.8))], farbe, null, 0, geschlossen: true);
    }

    /// <summary>
    /// Die Hälfte einer Pfeilspitze auf der Mittelachse: links (<paramref name="seite"/> = 1) oder rechts (-1) der
    /// Fahrtrichtung, minimal über die Achse hinaus, damit zwischen den Hälften keine Fuge zu sehen ist.
    /// </summary>
    private void HalberPfeil((V Von, V Nach) achse, int seite, Farbe farbe)
    {
        V r = achse.Nach - achse.Von;
        double laenge = r.Laenge;
        if (laenge < 1 || seite == 0)
        {
            return;
        }

        V t = r / laenge;
        V quer = t.Links * seite;
        V spitze = achse.Nach - (t * 9.5);
        V basis = spitze - (t * 7);
        f.Vieleck([P(spitze - (quer * 0.3)), P(basis + (quer * 3.8)), P(basis - (quer * 0.3))], farbe, null, 0, geschlossen: true);
    }

    /// <summary>Helle Linie auf der Mittelachse einer geteilten Fahrt (vom Start bis zum Fuß der Spitze).</summary>
    private void Trennlinie(Kartenfahrt fahrt, double s)
    {
        (V von, V nach) = Linie(fahrt with { Versatz = 0 }, s);
        V r = nach - von;
        double l = r.Laenge;
        if (l < 1)
        {
            return;
        }

        V ende = l > 30 ? nach - (r / l * 15.5) : nach;
        f.Linie(von.X, von.Y, ende.X, ende.Y, Hell, 0.8);
    }

    /// <summary>Helle Linie auf der Achse einer geteilten Pfeilspitze.</summary>
    private void PfeilTrennen((V Von, V Nach) achse)
    {
        V r = achse.Nach - achse.Von;
        double laenge = r.Laenge;
        if (laenge < 1)
        {
            return;
        }

        V t = r / laenge;
        V spitze = achse.Nach - (t * 9.5);
        V basis = spitze - (t * 7);
        f.Linie(basis.X, basis.Y, spitze.X, spitze.Y, Hell, 0.8);
    }

    /// <summary>
    /// Eine Hälfte des zweifarbigen Pfeils: halbe Breite, auf ihrer Seite der Mittelachse (minimal überlappend), sodass beide
    /// zusammen nicht dicker erscheinen als ein einfacher Pfeil; endet am Fuß der Spitze.
    /// </summary>
    private void HalbeFahrt(Kartenfahrt fahrt, double s)
    {
        (V von, V nach) = Linie(fahrt with { Versatz = 0 }, s);
        V r = nach - von;
        double l = r.Laenge;
        if (l < 1)
        {
            return;
        }

        V t = r / l;
        V quer = t.Links * (Math.Sign(fahrt.Versatz) * 0.55);
        V ende = l > 30 ? nach - (t * 15.5) : nach;
        f.Linie(von.X + quer.X, von.Y + quer.Y, ende.X + quer.X, ende.Y + quer.Y, Linienfarbe(fahrt), 1.4);
    }

    /// <summary>Ein Punkt bzw. Richtungsvektor in Bildpunkten.</summary>
    private readonly record struct V(double X, double Y)
    {
        public double Laenge => Math.Sqrt((X * X) + (Y * Y));

        /// <summary>Holt den Vektor um 90° nach links gedreht (quer zur Richtung).</summary>
        public V Links => Gedreht(this);

        public static V operator +(V a, V b) => new(a.X + b.X, a.Y + b.Y);

        public static V operator -(V a, V b) => new(a.X - b.X, a.Y - b.Y);

        public static V operator *(V a, double k) => new(a.X * k, a.Y * k);

        public static V operator /(V a, double k) => new(a.X / k, a.Y / k);

        private static V Gedreht(V v)
        {
            double quer = -v.Y;
            double laengs = v.X;
            return new V(quer, laengs);
        }
    }

    /// <summary>Gesetzter Name einer Halle: Text (Name oder Nummer), Lage und Größe, ob mit Hinweislinie.</summary>
    private sealed record Hallenname(string Text, double X, double Y, double Breite, double Hoehe, bool Leitlinie);

    /// <summary>Legende der nummerierten Hallen: Lage, Größe und je Eintrag Nummer und Name.</summary>
    private sealed record Legende(double X, double Y, double Breite, double Hoehe, IReadOnlyList<(string Nummer, string Name)> Eintraege);
}
