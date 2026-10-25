// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Turniere;
using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation;

/// <summary>
/// Übersicht der Kompaktspieltage für die Vereine im Ausdruck (nicht im Original; Peter, 14.10.2026), je Teil eine neue Seite:
/// Klassenname und Raster „Meldungen und Plan“ mit Legende; die Karte aller Fahrten; je Spieltag Ausrichter, Halle und
/// Begegnungen mit Uhrzeit; je Mannschaft genau eine Seite mit der Karte ihrer Fahrten und ihren Spieltagen.
/// </summary>
public static class Kompaktdruck
{
    /// <summary>Breite der Karte aller Fahrten (volle Seitenbreite im Hochformat).</summary>
    private const double Kartenbreite = 520;

    /// <summary>Breite der Karte je Mannschaft (Karte und Spieltage passen auf eine Seite).</summary>
    private const double Mannschaftskarte = 440;

    /// <summary>Höchste Höhe der Karte je Mannschaft; eine höhere Karte wird schmaler gezeichnet.</summary>
    private const double Mannschaftskartenhoehe = 340;

    /// <summary>Abstand zwischen den Teilen einer Seite.</summary>
    private const double Luft = 18;

    // Raster wie in der Ansicht: Datumsspalte, Felder 30 × 22 mit 1 Abstand, Kopf mit senkrechten Namen.
    private const double Datumsbreite = 64;
    private const double Spaltenbreite = 32;
    private const double Zeilenhoehe = 24;
    private const double Legendenhoehe = 84;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");
    private static readonly Farbe HeimFarbe = new(0x8F, 0xCB, 0x9E);
    private static readonly Farbe AuswaertsFarbe = new(0xCA, 0xDD, 0xF7);
    private static readonly Farbe Zweiter = new(0xC2, 0x41, 0x0C);
    private static readonly Farbe Dritter = new(0x7E, 0x22, 0xCE);
    private static readonly Farbe EinzelGelb = new(0xEA, 0xB3, 0x08);
    private static readonly Farbe SpitzeRing = new(0x11, 0x18, 0x27);
    private static readonly Farbe KellerRing = new(0x1D, 0x4E, 0xD8);
    private static readonly Farbe Randfarbe = new(0xD6, 0xD3, 0xCB);

    /// <summary>
    /// Baut die Abschnitte (jeder beginnt auf einer neuen Seite): „Meldungen und Plan“, „Alle Fahrten“, „Kompaktspieltage“ und je
    /// Mannschaft einen.
    /// </summary>
    /// <param name="auswertung">Die Auswertung des Plans.</param>
    /// <param name="halle">Adresse der Halle zu Mannschaft und Spiellokal (leer = Standardlokal); leer, wenn unbekannt.</param>
    /// <param name="klasse">Name der Klasse über dem Raster.</param>
    /// <param name="gruppentage">Spitzen- und Abstiegsspieltag des Plans (Ringe im Raster) oder <c>null</c>.</param>
    /// <returns>Die Abschnitte.</returns>
    public static IReadOnlyList<Abschnitt> Abschnitte(
        Kompaktauswertung auswertung,
        Func<string, string, string> halle,
        string klasse,
        (Kompaktspieltag? Spitze, Kompaktspieltag? Keller) gruppentage)
    {
        ArgumentNullException.ThrowIfNull(auswertung);
        ArgumentNullException.ThrowIfNull(halle);
        if (auswertung.Spieltage.Count == 0)
        {
            return [new Abschnitt("Kompaktspieltage", [new Textzeile("Der Plan hat keine Kompaktspieltage.", Druckbericht.Groesse, Farbe.Grau)])];
        }

        var abschnitte = new List<Abschnitt>
        {
            new("Meldungen und Plan", [Raster(auswertung, gruppentage, klasse)]),
            new("Alle Fahrten", [Karte(Kompaktfahrtenkarte.Bauen(auswertung, auswertung.Spieltage, null, alleZeigen: true), Kartenbreite, 0), KmJeMannschaft(auswertung)]),
            Spieltage(auswertung, halle),
        };
        abschnitte.AddRange(Mannschaften(auswertung, halle));
        return abschnitte;
    }

    /// <summary>
    /// Das Raster „Meldungen und Plan“ mit Legende als Grafik, gezeichnet wie in der Ansicht; auf Wunsch mit dem Klassennamen
    /// darüber. Der Name gehört zur Grafik, damit ein hohes Raster (viele Tage) als Ganzes auf die erste Seite verkleinert
    /// wird, statt allein auf die nächste Seite zu rutschen und den Namen auf einer sonst leeren Seite zu lassen.
    /// </summary>
    /// <param name="a">Die Auswertung.</param>
    /// <param name="gruppentage">Spitzen- und Abstiegsspieltag (Ringe).</param>
    /// <param name="klasse">Name der Klasse über dem Raster oder <c>null</c>.</param>
    /// <returns>Die Grafik.</returns>
    public static Grafik Raster(Kompaktauswertung a, (Kompaktspieltag? Spitze, Kompaktspieltag? Keller) gruppentage, string? klasse = null)
    {
        ArgumentNullException.ThrowIfNull(a);
        List<Kompaktrasterzeile> zeilen = a.Tage.Select(t => KompaktspieltageAnsichtViewModel.Rasterzeile(a, t, false, null, false, null, gruppentage)).ToList();
        double titelgroesse = Druckbericht.Groesse + 5;
        double oben = klasse is null ? 0 : (titelgroesse * Seitenumbruch.Zeilenfaktor) + Luft;
        double breite = Math.Max(Datumsbreite + (a.Mannschaften.Count * Spaltenbreite), 470);
        double kopf = Kopfhoehe(a.Mannschaften.Select(m => m.Name));
        double hoehe = oben + kopf + (zeilen.Count * Zeilenhoehe) + Legendenhoehe;
        return new Grafik(
            breite,
            hoehe,
            f =>
            {
                if (klasse is not null)
                {
                    f.Text(klasse, 0, 0, titelgroesse, Farbe.Tinte, true);
                }

                // Das Raster mittig über der (breiteren) Legende, beides zusammen mittig auf der Seite.
                double links = Math.Max(0, (breite - Datumsbreite - (a.Mannschaften.Count * Spaltenbreite)) / 2);
                RasterZeichnen(f, a, zeilen, (links, oben), kopf);
            })
        {
            Zentriert = true,
        };
    }

    /// <summary>
    /// Höhe des Rasterkopfs mit den senkrechten Mannschaftsnamen (12 pt), in der Ansicht und im Ausdruck gleich: Platz für die
    /// längste Zeile (lange Namen stehen in zwei Zeilen, siehe <see cref="Kopfzeilen"/>), mindestens 104. Geschätzt mit 6,8 je
    /// Zeichen, weil die Höhe vor dem Zeichnen feststehen muss.
    /// </summary>
    /// <param name="namen">Die Mannschaftsnamen.</param>
    /// <returns>Die Höhe.</returns>
    public static double Kopfhoehe(IEnumerable<string> namen)
    {
        ArgumentNullException.ThrowIfNull(namen);
        int laengste = namen.SelectMany(Kopfzeilen).Select(z => z.Length).DefaultIfEmpty(0).Max();
        return Math.Max(104, Math.Ceiling(12 + (laengste * 6.8)));
    }

    /// <summary>
    /// Ein Mannschaftsname für den senkrechten Rasterkopf: bis 15 Zeichen eine Zeile, sonst zwei – geteilt nach „/“, „-“ oder
    /// an einem Leerzeichen, und zwar dort, wo die längere Hälfte am kürzesten ist (Peter, 14.10.2026), z. B.
    /// „TTG Mündersbach/“ und „Höchstenbach“. Ohne solche Stelle bleibt es bei einer Zeile. Jede Zeile hat höchstens 22 Zeichen
    /// (sonst mit „…“ gekürzt), damit sehr lange Namen den Kopf nicht beliebig hoch machen.
    /// </summary>
    /// <param name="name">Der Mannschaftsname.</param>
    /// <returns>Eine oder zwei Zeilen.</returns>
    public static IReadOnlyList<string> Kopfzeilen(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        const int Einzeilig = 15;
        const int Zeichen = 22;
        static string Kurz(string zeile) => zeile.Length <= Zeichen ? zeile : zeile[..(Zeichen - 1)].TrimEnd() + "…";
        if (name.Length <= Einzeilig)
        {
            return [name];
        }

        // Teilstelle i: „/“ und „-“ bleiben am Ende der ersten Zeile, ein Leerzeichen entfällt.
        var stellen = Enumerable.Range(1, name.Length - 2)
            .Where(i => name[i] is '/' or '-' or ' ')
            .Select(i => name[i] == ' ' ? (Erste: name[..i], Zweite: name[(i + 1)..]) : (Erste: name[..(i + 1)], Zweite: name[(i + 1)..]))
            .Where(t => t.Erste.Trim().Length > 0 && t.Zweite.Trim().Length > 0)
            .ToList();
        if (stellen.Count == 0)
        {
            return [Kurz(name)];
        }

        (string erste, string zweite) = stellen.MinBy(t => Math.Max(t.Erste.Length, t.Zweite.Length));
        return [Kurz(erste.TrimEnd()), Kurz(zweite.TrimStart())];
    }

    /// <summary>„km je Mannschaft“ wie in der Ansicht: je Mannschaft Ausrichtungen, ein Balken und die km, die weiteste zuerst.</summary>
    /// <param name="a">Die Auswertung.</param>
    /// <returns>Die Grafik.</returns>
    public static Grafik KmJeMannschaft(Kompaktauswertung a)
    {
        ArgumentNullException.ThrowIfNull(a);
        const double Zeile = 20;
        const double Kopf = 30;
        List<Kompaktmannschaft> liste = a.Mannschaften.OrderByDescending(m => m.Km).ToList();
        double weiteste = Math.Max(1, liste.Select(m => m.Km).DefaultIfEmpty(0).Max());
        return new Grafik(
            Kartenbreite,
            Kopf + (liste.Count * Zeile),
            f =>
            {
                f.Text("km je Mannschaft", 0, 0, 13, Farbe.Tinte, true);
                const string Hinweis = "Luftlinie, hin und zurück";
                f.Text(Hinweis, Kartenbreite - f.TextBreite(Hinweis, 10, false), 3, 10, Farbe.Tinte2, false);
                static string Ausrichter(Kompaktmannschaft m) => m.EinzelHeim > 0
                    ? string.Create(Deutsch, $"{m.Ausgerichtet}× Ausrichter, {m.EinzelHeim}× Einzel zuhause")
                    : string.Create(Deutsch, $"{m.Ausgerichtet}× Ausrichter");

                // Spalten nach den gemessenen Namen (volle Mannschaftsnamen sind unterschiedlich lang, sehr lange gekürzt), Balken bis vor die km.
                double namensbreite = Math.Min(Textkuerzung.Namensbreite, liste.Select(m => f.TextBreite(m.Name, 11, false)).DefaultIfEmpty(0).Max());
                double xAusrichter = Math.Max(140, namensbreite + 12);
                double xBalken = Math.Max(350, xAusrichter + liste.Select(m => f.TextBreite(Ausrichter(m), 10, false)).DefaultIfEmpty(0).Max() + 12);
                double balken = Math.Max(30, Math.Min(120, Kartenbreite - 50 - xBalken));
                for (int i = 0; i < liste.Count; i++)
                {
                    Kompaktmannschaft m = liste[i];
                    double y = Kopf + (i * Zeile);
                    string km = m.Km.ToString("N0", Deutsch);
                    f.Text(Textkuerzung.Kuerzen(f, m.Name, namensbreite, 11, false), 0, y, 11, Farbe.Tinte, false);
                    f.Text(Ausrichter(m), xAusrichter, y + 1, 10, Farbe.Tinte2, false);
                    f.Rechteck(xBalken, y + 4, balken * m.Km / weiteste, 7, Farbe.Akzent);
                    f.Text(km, Kartenbreite - f.TextBreite(km, 11, false), y, 11, Farbe.Tinte, false);
                }
            },
            Luft);
    }

    /// <summary>Eine Karte als Grafik: Hintergrund, auf die Fläche beschnitten, darin der <see cref="Kartenzeichner"/>.</summary>
    /// <param name="bild">Das Kartenbild.</param>
    /// <param name="breite">Breite der Grafik.</param>
    /// <param name="abstand">Abstand davor.</param>
    /// <returns>Die Grafik.</returns>
    public static Grafik Karte(Kartenbild bild, double breite, double abstand)
    {
        ArgumentNullException.ThrowIfNull(bild);
        double hoehe = bild.Breite <= 0 ? 0 : breite * bild.Hoehe / bild.Breite;
        return new Grafik(
            breite,
            hoehe,
            f =>
            {
                using (f.Begrenzen(0, 0, breite, hoehe))
                {
                    f.Rechteck(0, 0, breite, hoehe, Kartenzeichner.Hintergrund);
                    new Kartenzeichner(f).Zeichnen(bild, breite, hoehe);
                }
            },
            abstand);
    }

    /// <summary>Kopfzeile eines Spieltags, z. B. „So. 13.09.2026 · 4er-Spieltag · Ausrichter: TTC A“.</summary>
    /// <param name="tag">Der Spieltag.</param>
    /// <returns>Der Text.</returns>
    public static string Kopf(Kompaktspieltag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        string datum = tag.Tag.ToString("ddd dd.MM.yyyy", Deutsch);
        string ausrichter = tag.Ausrichter ?? "kein passender Ausrichter";
        return tag.Form switch
        {
            Turnierform.Vierer => $"{datum} · 4er-Spieltag · Ausrichter: {ausrichter}",
            Turnierform.Dreier => $"{datum} · 3er-Spieltag · Ausrichter: {ausrichter}",
            Turnierform.Einzel => $"{datum} · Einzelbegegnung · Heim: {ausrichter}",
            _ => $"{datum} · Spieltag · Ausrichter: {ausrichter}",
        };
    }

    /// <summary>Die Halle eines Spieltags: die des Ausrichters an diesem Tag, sonst seine Standardhalle.</summary>
    /// <param name="a">Die Auswertung.</param>
    /// <param name="tag">Der Spieltag.</param>
    /// <param name="halle">Adresse zu Mannschaft und Spiellokal.</param>
    /// <returns>Die Adresse oder leer.</returns>
    public static string Halle(Kompaktauswertung a, Kompaktspieltag tag, Func<string, string, string> halle)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(halle);
        if (tag.Ausrichter is not string ausrichter)
        {
            return string.Empty;
        }

        string lokal = tag.Spiele.Where(s => s.Heim == ausrichter).Select(s => s.Spiellokal).FirstOrDefault(l => l.Length > 0) ?? string.Empty;
        string adresse = halle(ausrichter, lokal);
        return adresse.Length > 0 ? adresse : a.Mannschaften.FirstOrDefault(m => m.Name == ausrichter)?.Halle ?? string.Empty;
    }

    private static void RasterZeichnen(IZeichenflaeche f, Kompaktauswertung a, List<Kompaktrasterzeile> zeilen, (double Links, double Oben) lage, double kopf)
    {
        (double links, double oben) = lage;
        for (int j = 0; j < a.Mannschaften.Count; j++)
        {
            // Eine Zeile mittig in der Spalte; zwei Zeilen nebeneinander (die erste links), zusammen mittig.
            IReadOnlyList<string> kopfzeilen = Kopfzeilen(a.Mannschaften[j].Name);
            double mitte = links + Datumsbreite + (j * Spaltenbreite) + (Spaltenbreite / 2);
            double x = mitte - 8 - ((kopfzeilen.Count - 1) * 7);
            foreach (string zeile in kopfzeilen)
            {
                f.TextSenkrecht(zeile, x, oben + kopf - 4, 12, Farbe.Tinte2);
                x += 14;
            }
        }

        for (int i = 0; i < zeilen.Count; i++)
        {
            double y = oben + kopf + (i * Zeilenhoehe);
            f.Text(zeilen[i].Datum, links + Datumsbreite - 8 - f.TextBreite(zeilen[i].Datum, 11, false), y + 4, 11, Farbe.Tinte2, false);
            for (int j = 0; j < zeilen[i].Zellen.Count; j++)
            {
                Feld(f, zeilen[i].Zellen[j], links + Datumsbreite + (j * Spaltenbreite) + 1, y + 1);
            }
        }

        // Die Legende nennt nur die Farben, die im Plan vorkommen.
        List<Kompaktzelle> alle = [.. zeilen.SelectMany(z => z.Zellen)];
        Legende(f, oben + kopf + (zeilen.Count * Zeilenhoehe) + 12, (alle.Exists(z => z.ZweiterSpieltag), alle.Exists(z => z.DritterSpieltag), alle.Exists(z => z.Einzel)));
    }

    /// <summary>Ein Feld des Rasters: Meldung als Farbe, Ring für Spitzen- und Abstiegsspieltag, ★ bzw. ● bzw. ?.</summary>
    private static void Feld(IZeichenflaeche f, Kompaktzelle z, double x, double y)
    {
        const double B = 30;
        const double H = 22;
        if (z.IstHeim || z.IstAuswaerts)
        {
            f.Rechteck(x, y, B, H, z.IstHeim ? HeimFarbe : AuswaertsFarbe);
        }
        else if (z.IstBeides)
        {
            f.Vieleck([(x, y), (x + B, y), (x, y + H)], HeimFarbe, null, 0, geschlossen: true);
            f.Vieleck([(x + B, y), (x + B, y + H), (x, y + H)], AuswaertsFarbe, null, 0, geschlossen: true);
        }

        if (z.IstUnbekannt || z.Verstoss)
        {
            f.Vieleck([(x, y), (x + B, y), (x + B, y + H), (x, y + H)], null, z.Verstoss ? Farbe.Schlecht : Randfarbe, z.Verstoss ? 2 : 1, geschlossen: true);
        }

        double mx = x + (B / 2);
        double my = y + (H / 2);
        if (z.Spitzenspieltag || z.Abstiegsspieltag)
        {
            f.Kreis(mx, my, 8.25, null, z.Spitzenspieltag ? SpitzeRing : KellerRing, 1.5);
        }

        Farbe zeichen = Zeichenfarbe(z);
        if (z.IstAusrichter)
        {
            Stern(f, mx, my, 5.5, zeichen);
        }
        else if (z.IstGast)
        {
            f.Kreis(mx, my, 4, zeichen, null, 0);
        }
        else if (z.IstUnbekanntZeichen)
        {
            f.Text("?", mx - (f.TextBreite("?", 13, false) / 2), my - 8.5, 13, Farbe.Tinte, false);
        }
    }

    private static Farbe Zeichenfarbe(Kompaktzelle z)
    {
        if (z.Einzel)
        {
            return EinzelGelb;
        }

        if (z.DritterSpieltag)
        {
            return Dritter;
        }

        return z.ZweiterSpieltag ? Zweiter : Farbe.Tinte;
    }

    /// <summary>Fünfzackiger Stern um einen Mittelpunkt (wie ★).</summary>
    private static void Stern(IZeichenflaeche f, double mx, double my, double r, Farbe farbe)
    {
        var punkte = new List<(double X, double Y)>();
        for (int i = 0; i < 10; i++)
        {
            double radius = i % 2 == 0 ? r : r * 0.42;
            double winkel = (-90 + (i * 36)) * Math.PI / 180;
            punkte.Add((mx + (radius * Math.Cos(winkel)), my + 0.6 + (radius * Math.Sin(winkel))));
        }

        f.Vieleck(punkte, farbe, null, 0, geschlossen: true);
    }

    /// <summary>Legende unter dem Raster, wie in der Ansicht – mit den Farben, die im Plan vorkommen.</summary>
    private static void Legende(IZeichenflaeche f, double y, (bool Zweiter, bool Dritter, bool Einzel) farben)
    {
        const double G = 10;
        double x = 0;
        void Eintrag(Action<double, double> zeichen, string text, double breite)
        {
            zeichen(x, y);
            f.Text(text, x + 22, y - 1, G, Farbe.Tinte2, false);
            x += 22 + f.TextBreite(text, G, false) + breite;
        }

        Eintrag((ex, ey) => f.Rechteck(ex, ey, 16, 12, HeimFarbe), "Heim möglich", 14);
        Eintrag((ex, ey) => f.Rechteck(ex, ey, 16, 12, AuswaertsFarbe), "auswärts möglich", 14);
        Eintrag(
            (ex, ey) =>
            {
                f.Vieleck([(ex, ey), (ex + 16, ey), (ex, ey + 12)], HeimFarbe, null, 0, geschlossen: true);
                f.Vieleck([(ex + 16, ey), (ex + 16, ey + 12), (ex, ey + 12)], AuswaertsFarbe, null, 0, geschlossen: true);
            },
            "beides",
            14);
        Eintrag((ex, ey) => f.Vieleck([(ex, ey), (ex + 16, ey), (ex + 16, ey + 12), (ex, ey + 12)], null, Randfarbe, 1, geschlossen: true), "? keine Meldung", 14);
        f.Text("leer: kann nicht", x, y - 1, G, Farbe.Tinte2, false);

        x = 0;
        y += 20;
        Eintrag((ex, ey) => Stern(f, ex + 8, ey + 6, 5.5, Farbe.Tinte), "richtet aus", 14);
        Eintrag((ex, ey) => f.Kreis(ex + 8, ey + 6, 4, Farbe.Tinte, null, 0), "Gast", 14);
        foreach ((_, Farbe farbe, string text) in new[]
        {
            (farben.Zweiter, Zweiter, "zweiter Spieltag am selben Tag"),
            (farben.Dritter, Dritter, "dritter Spieltag am selben Tag"),
            (farben.Einzel, EinzelGelb, "Einzelbegegnung"),
        }.Where(e => e.Item1))
        {
            Eintrag(
                (ex, ey) =>
                {
                    Stern(f, ex + 4, ey + 6, 5, farbe);
                    f.Kreis(ex + 14, ey + 6, 4, farbe, null, 0);
                },
                text,
                14);
        }

        x = 0;
        y += 20;
        Eintrag((ex, ey) => f.Vieleck([(ex, ey), (ex + 16, ey), (ex + 16, ey + 12), (ex, ey + 12)], null, Farbe.Schlecht, 2, geschlossen: true), "gegen die Meldung", 14);
        Eintrag(
            (ex, ey) =>
            {
                f.Kreis(ex + 8, ey + 6, 7, null, SpitzeRing, 1.5);
                Stern(f, ex + 8, ey + 6, 4, Farbe.Tinte);
            },
            "Spitzenspieltag",
            14);
        Eintrag(
            (ex, ey) =>
            {
                f.Kreis(ex + 8, ey + 6, 7, null, KellerRing, 1.5);
                Stern(f, ex + 8, ey + 6, 4, Farbe.Tinte);
            },
            "Abstiegsspieltag",
            14);
    }

    private static Abschnitt Spieltage(Kompaktauswertung a, Func<string, string, string> halle)
    {
        var zeilen = new List<Tabellenzeile>();
        foreach (Kompaktspieltag tag in a.Spieltage)
        {
            if (zeilen.Count > 0)
            {
                zeilen.Add(new Tabellenzeile([new(string.Empty)], Farbe.Tinte, Art: Zeilenart.Abstand));
            }

            zeilen.Add(new Tabellenzeile([new(Kopf(tag))], Farbe.Tinte, Fett: true, Art: Zeilenart.Ueberschrift));
            string adresse = Halle(a, tag, halle);
            zeilen.Add(new Tabellenzeile([new(string.Empty), new("Halle: " + (adresse.Length > 0 ? adresse : "unbekannt"))], Farbe.Tinte2));
            zeilen.AddRange(tag.Spiele.Select(s => new Tabellenzeile([new(Uhrzeit(s)), new(s.Heim + " – " + s.Gast)], Farbe.Tinte)));
        }

        List<Baustein> bausteine = zeilen.Count == 0
            ? [new Textzeile("Der Plan hat keine Kompaktspieltage.", Druckbericht.Groesse, Farbe.Grau)]
            : [new Tabelle(["Beginn", "Begegnung"], zeilen, Druckbericht.Groesse, LetzteSpalteUmbrechen: true)];
        return new Abschnitt("Kompaktspieltage", bausteine);
    }

    /// <summary>
    /// Je Mannschaft eine Seite: Kopfzeile, Karte ihrer Fahrten und ihre Spieltage – je Spieltag eine Überschrift (Datum,
    /// Form, Heim bzw. bei wem), darunter die Halle und je Spiel eine Zeile mit Beginn und Gegner, mit Luft zur Karte und
    /// zwischen den Spieltagen (Peter, 14.10.2026). Die Karte ist höchstens <see cref="Mannschaftskartenhoehe"/> hoch, damit
    /// auch bei quadratischen Karten (Stadtklassen) alles auf eine Seite passt.
    /// </summary>
    private static IEnumerable<Abschnitt> Mannschaften(Kompaktauswertung a, Func<string, string, string> halle)
    {
        foreach (Kompaktmannschaft m in a.Mannschaften)
        {
            List<Kompaktspieltag> tage = a.Spieltage.Where(t => t.Mannschaften.Contains(m.Name)).ToList();
            if (tage.Count == 0)
            {
                continue;
            }

            string kopf = string.Create(Deutsch, $"{Anzahl(m.Ausgerichtet, "Heimspieltag", "Heimspieltage")} · {m.Km:N0} km Luftlinie (hin und zurück)");
            var bausteine = new List<Baustein> { new Textzeile(kopf, Druckbericht.Groesse + 1, Farbe.Tinte2) };
            if (m.Lage is not null && tage.Exists(t => t.Ausrichter != m.Name))
            {
                Kartenbild bild = Kompaktfahrtenkarte.Mannschaft(a, m.Name);
                double breite = bild.Hoehe > 0 ? Math.Min(Mannschaftskarte, Mannschaftskartenhoehe * bild.Breite / bild.Hoehe) : Mannschaftskarte;
                bausteine.Add(Karte(bild, breite, Luft) with { Zentriert = true });
            }

            var zeilen = new List<Tabellenzeile>();
            foreach (Kompaktspieltag tag in tage)
            {
                if (zeilen.Count > 0)
                {
                    zeilen.Add(new Tabellenzeile([new(string.Empty)], Farbe.Tinte, Art: Zeilenart.Abstand));
                }

                string ort = tag.Ausrichter == m.Name ? "Heim (Ausrichter)" : "bei " + (tag.Ausrichter ?? "?");
                zeilen.Add(new Tabellenzeile([new($"{tag.Tag.ToString("ddd dd.MM.yyyy", Deutsch)} · {Form(tag)} · {ort}")], Farbe.Tinte, Fett: true, Art: Zeilenart.Ueberschrift));
                string adresse = Halle(a, tag, halle);
                zeilen.Add(new Tabellenzeile([new(string.Empty), new("Halle: " + (adresse.Length > 0 ? adresse : "unbekannt"))], Farbe.Tinte2));
                zeilen.AddRange(tag.Spiele
                    .Where(s => s.Heim == m.Name || s.Gast == m.Name)
                    .Select(s => new Tabellenzeile([new(Uhrzeit(s)), new("gegen " + (s.Heim == m.Name ? s.Gast : s.Heim))], Farbe.Tinte)));
            }

            bausteine.Add(new Tabelle(["Beginn", "Spiel"], zeilen, Druckbericht.Groesse, LetzteSpalteUmbrechen: true, AbstandDavor: Luft * 1.6));
            yield return new Abschnitt(m.Name, bausteine);
        }
    }

    private static string Form(Kompaktspieltag tag) => tag.Form switch
    {
        Turnierform.Vierer => "4er-Spieltag",
        Turnierform.Dreier => "3er-Spieltag",
        Turnierform.Einzel => "Einzelbegegnung",
        _ => "Spieltag",
    };

    private static string Uhrzeit(Spiel s) => s.Zeitpunkt?.ToString("HH:mm", Deutsch) ?? string.Empty;

    private static string Anzahl(int n, string einzahl, string mehrzahl) =>
        string.Create(Deutsch, $"{n} {(n == 1 ? einzahl : mehrzahl)}");
}
