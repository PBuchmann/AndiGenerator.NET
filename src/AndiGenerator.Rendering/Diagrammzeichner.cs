// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Rendering;

/// <summary>
/// Diagramme wie im Original (<c>PaintVerteilung</c>): Spieltage, Wechsel Heim/Auswärts, Spielverteilung, Abstand
/// Heimspiel zu Auswärtsspiel, Anzahl Spiele pro Woche und Setzliste. Inhalt und Koordinaten wie im Original, Farben und
/// Linien in der Gestaltung von AndiGenerator.NET (ruhige Bänder, Heimspiel gefüllt im Akzentblau, Auswärtsspiel hohl).
/// Dieselbe Routine zeichnet auf den Bildschirm und in den Ausdruck und misst ohne Zeichenfläche.
/// </summary>
public sealed class Diagrammzeichner
{
    private const double Links = 300;
    private const double Amplitude = 10;
    private const double Normal = 12;
    private const double Gross = 16;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    // Farben der Gestaltung (Stil.axaml): Tinte, Grau, Linie, Band, Akzent, Gut, Schlecht.
    private static readonly Farbe Tinte = new(0x1B, 0x1F, 0x24);
    private static readonly Farbe Beschriftung = new(0x5B, 0x64, 0x70);
    private static readonly Farbe Band = new(0xF7, 0xF6, 0xF2);
    private static readonly Farbe LinieGrau = new(0xE2, 0xE0, 0xDA);
    private static readonly Farbe Silber = new(0xC9, 0xC5, 0xBB);
    private static readonly Farbe Spieltaglinie1 = new(0xA9, 0xC4, 0xE6);
    private static readonly Farbe Spieltaglinie2 = new(0xE3, 0xC9, 0x9A);
    private static readonly Farbe Heimspiel = new(0x1D, 0x5F, 0xA8);
    private static readonly Farbe Auswaertsspiel = new(0x4A, 0x53, 0x60);
    private static readonly Farbe Weiss = new(0xFF, 0xFF, 0xFF);
    private static readonly Farbe Gut = new(0x1D, 0x6B, 0x3A);
    private static readonly Farbe Ocker = new(0xD4, 0xA0, 0x3C);
    private static readonly Farbe Orange = new(0xD9, 0x70, 0x5F);
    private static readonly Farbe Schlecht = new(0xB4, 0x23, 0x18);
    private static readonly Farbe Ueberlappung = new(0xE8, 0xAA, 0x9E);

    private readonly Diagrammdaten d;
    private readonly IZeichenflaeche? f;
    private readonly bool mitTiteln;

    /// <summary>Initialisiert den Zeichner mit Titeln über jedem Diagramm (Ausdruck).</summary>
    /// <param name="daten">Die Diagrammdaten.</param>
    /// <param name="flaeche">Zeichenfläche oder <c>null</c> zum reinen Messen.</param>
    public Diagrammzeichner(Diagrammdaten daten, IZeichenflaeche? flaeche)
        : this(daten, flaeche, mitTiteln: true)
    {
    }

    /// <summary>Initialisiert den Zeichner.</summary>
    /// <param name="daten">Die Diagrammdaten.</param>
    /// <param name="flaeche">Zeichenfläche oder <c>null</c> zum reinen Messen.</param>
    /// <param name="mitTiteln">Titel über jedem Diagramm zeichnen; die Ansicht zeigt ihn selbst an.</param>
    public Diagrammzeichner(Diagrammdaten daten, IZeichenflaeche? flaeche, bool mitTiteln)
    {
        ArgumentNullException.ThrowIfNull(daten);
        d = daten;
        f = flaeche;
        this.mitTiteln = mitTiteln;
    }

    private int Anzahl => d.Mannschaften.Count;

    /// <summary>Die Diagramme, die für diese Daten gezeichnet werden, in der Reihenfolge des Originals.</summary>
    /// <param name="daten">Die Diagrammdaten.</param>
    /// <returns>Die Diagramme; leer, wenn keine Spiele terminiert sind.</returns>
    public static IReadOnlyList<Diagrammteil> Teile(Diagrammdaten daten)
    {
        ArgumentNullException.ThrowIfNull(daten);
        if (!daten.HatSpiele)
        {
            return [];
        }

        var teile = new List<Diagrammteil> { Diagrammteil.Spieltage, Diagrammteil.WechselHeimAuswaerts, Diagrammteil.Spielverteilung };
        if (daten.MitRueckrunde)
        {
            teile.Add(Diagrammteil.AbstandHeimAuswaerts);
        }

        teile.Add(Diagrammteil.SpieleProWoche);
        if (daten.HatSetzliste)
        {
            teile.Add(Diagrammteil.Setzliste);
        }

        return teile;
    }

    /// <summary>Original <c>PaintVerteilung</c>: alle Diagramme untereinander.</summary>
    /// <returns>Benötigte Größe.</returns>
    public Zeichengroesse Alles()
    {
        if (!d.HatSpiele)
        {
            Text("Keine Spiele terminiert", 0, 0);
            return new Zeichengroesse(600, 40);
        }

        double xMax = 500;
        double y = -40;
        foreach (Diagrammteil teil in Teile(d))
        {
            Zeichengroesse s = Zeichnen(teil, y + 40);
            xMax = Math.Max(xMax, s.Breite);
            y = s.Hoehe;
        }

        return new Zeichengroesse(xMax + 100, y);
    }

    /// <summary>Zeichnet ein Diagramm ab einer Höhe.</summary>
    /// <param name="teil">Das Diagramm.</param>
    /// <param name="y">Oberkante.</param>
    /// <returns>Benötigte Breite und die Unterkante.</returns>
    public Zeichengroesse Zeichnen(Diagrammteil teil, double y) => teil switch
    {
        Diagrammteil.Spieltage => Spieltage(y),
        Diagrammteil.WechselHeimAuswaerts => WechselHeimAuswaerts(y),
        Diagrammteil.Spielverteilung => Spielverteilung(y),
        Diagrammteil.AbstandHeimAuswaerts => AbstandHinRueck(y),
        Diagrammteil.SpieleProWoche => SpieleProWoche(y),
        Diagrammteil.Setzliste => Setzliste(y),
        _ => throw new ArgumentOutOfRangeException(nameof(teil), teil, "Unbekanntes Diagramm."),
    };

    private static List<DateTime> Termine(Diagrammmannschaft m) =>
        m.Spiele.Where(s => s.Zeitpunkt is not null).Select(s => s.Zeitpunkt!.Value).ToList();

    /// <summary>Farbe eines Setzlistenbalkens: grün bis Zielwert, dann gelb, orange, rot.</summary>
    private static Farbe Balkenfarbe(int wert, int ziel)
    {
        if (wert > ziel * 5)
        {
            return Schlecht;
        }

        if (wert > ziel * 2)
        {
            return Orange;
        }

        return wert > ziel ? Ocker : Gut;
    }

    private static Farbe Mischen(Farbe von, Farbe bis, double anteil) => new(
        (byte)Math.Round(von.R + ((bis.R - von.R) * anteil)),
        (byte)Math.Round(von.G + ((bis.G - von.G) * anteil)),
        (byte)Math.Round(von.B + ((bis.B - von.B) * anteil)));

    /// <summary>Original <c>xPosByDate</c>.</summary>
    private double X(DateTime zeitpunkt) => Links + Math.Truncate((zeitpunkt - d.Erster).TotalDays * 4);

    private DateTime ErsterMontag()
    {
        DateTime tag = d.Erster.Date;
        return tag.AddDays(-(((int)tag.DayOfWeek + 6) % 7));
    }

    /// <summary>Original <c>PaintSpielPlanVerteilung</c> mit <c>PaintSpielPlanOverlap</c> und <c>PaintVerteilungMannschaft</c>.</summary>
    private Zeichengroesse Spieltage(double y)
    {
        Titel("Spieltage", y);
        y += 30;
        Datumszeile(y);
        y += 5;

        double oben = y + 25;
        double hoehe = ((Anzahl - 1) * 30) + 20;
        foreach (Spieltagsueberlappung u in d.Ueberlappungen)
        {
            double x1 = X(u.Von);
            double x2 = Math.Max(X(u.Bis), x1 + 1);

            // Je dunkler das Grau im Original, desto stärker die Überlappung: hier als zarter bis kräftiger Rotton.
            double staerke = Math.Clamp((255 - u.Helligkeit) / 128.0, 0, 1);
            Rechteck(Mischen(Weiss, Ueberlappung, staerke), x1, oben, x2, oben + hoehe);
        }

        double xMax = 0;
        for (int i = 0; i + 1 < Anzahl; i++)
        {
            double yM = y + ((i + 1) * 30);
            List<DateTime> termine = Termine(d.Mannschaften[i]);
            List<DateTime> naechste = Termine(d.Mannschaften[i + 1]);
            for (int k = 0; k < Math.Min(termine.Count, naechste.Count); k++)
            {
                Linie(k % 2 == 0 ? Spieltaglinie1 : Spieltaglinie2, X(termine[k]), yM + 5, X(naechste[k]), yM + 35);
            }
        }

        for (int i = 0; i < Anzahl; i++)
        {
            double yM = y + ((i + 1) * 30);
            Text(d.Mannschaften[i].Name, 0, yM);
            foreach (Diagrammspiel spiel in d.Mannschaften[i].Spiele.Where(s => s.Zeitpunkt is not null))
            {
                double x = X(spiel.Zeitpunkt!.Value);
                xMax = Math.Max(xMax, x);
                Rechteck(spiel.Heimspiel ? Heimspiel : Auswaertsspiel, x - 1, yM, x + 1, yM + 10);
            }
        }

        y += (Anzahl * 30) + 35;
        Rechteck(Heimspiel, 4, y, 6, y + 10);
        Text("Heimspiel", 10, y, Normal, Beschriftung);
        Rechteck(Auswaertsspiel, 104, y, 106, y + 10);
        Text("Auswärtsspiel", 110, y, Normal, Beschriftung);
        Rechteck(Ueberlappung, 204, y, 214, y + 10);
        Text("Überlappungen der Spieltage", 220, y, Normal, Beschriftung);
        return new Zeichengroesse(xMax + 100, y + 20);
    }

    /// <summary>Original <c>PaintSpielWechselHeimAuswaerts</c>.</summary>
    private Zeichengroesse WechselHeimAuswaerts(double y)
    {
        Titel("Wechsel Heim/Auswärts", y);
        y += 10;
        Tabellenhintergrund(y + 22, 30, Links + (Amplitude * d.MaxSpiele) + 10);
        double xMax = 0;
        foreach (Diagrammmannschaft m in d.Mannschaften)
        {
            y += 30;
            Text(m.Name, 0, y);
            double x = Links;
            double alt = -1;
            foreach (Diagrammspiel spiel in m.Spiele.Where(s => s.Zeitpunkt is not null))
            {
                double neu = spiel.Heimspiel ? y + Amplitude : y;
                if (alt > 0)
                {
                    Linie(Heimspiel, x, alt, x + Amplitude, neu, 1.5);
                }

                alt = neu;
                x += Amplitude;
                xMax = Math.Max(xMax, x);
            }
        }

        return new Zeichengroesse(xMax + 100, y + 20);
    }

    /// <summary>Original <c>PaintSpielAbstaende</c> mit <c>PaintDateRaster</c> und <c>PaintGameBubble</c>.</summary>
    private Zeichengroesse Spielverteilung(double y)
    {
        Titel("Spielverteilung", y);
        y += 30;
        Datumszeile(y);
        Datumsraster(y + 20, 80);
        double xMax = 0;
        foreach (Diagrammmannschaft m in d.Mannschaften)
        {
            y += 80;
            Text(m.Name, 0, y);
            foreach (Diagrammspiel spiel in m.Spiele.Where(s => s.Zeitpunkt is not null))
            {
                double x = X(spiel.Zeitpunkt!.Value);
                xMax = Math.Max(xMax, x);
                Blase(x, y, spiel);
            }
        }

        if (d.HatKoppeltermine)
        {
            y += 30;
            Text("Quadrat = Koppeltermin/Doppelspieltag · gefüllt = Heimspiel, hohl = Auswärtsspiel", 0, y, Normal, Beschriftung);
        }

        return new Zeichengroesse(xMax + 100, y + 20);
    }

    /// <summary>Original <c>PaintSpielAbstandHinRueck</c> mit <c>PaintSpielAbstandMannschaft</c>.</summary>
    private Zeichengroesse AbstandHinRueck(double y)
    {
        Titel("Abstand Heimspiel zu Auswärtsspiel", y);
        y += 30;
        Datumszeile(y);
        y += 25;
        double xMax = 0;
        foreach (Diagrammmannschaft m in d.Mannschaften)
        {
            Text(m.Name, 0, y);
            double zeile = y;
            foreach (IReadOnlyList<Abstandslinie> linien in m.Abstaende)
            {
                foreach (Abstandslinie l in linien)
                {
                    double x1 = X(l.Von);
                    double x2 = X(l.Bis);
                    xMax = Math.Max(xMax, x2 + 100);
                    Linie(new Farbe((byte)l.Rot, (byte)l.Gruen, 0), x1, zeile, x2, zeile);
                    Linie(Beschriftung, x1, zeile - 2, x1, zeile + 3);
                    Linie(Beschriftung, x2, zeile - 2, x2, zeile + 3);
                }

                zeile += 5;
            }

            y = Math.Max(20, zeile - 5) + 10;
        }

        return new Zeichengroesse(xMax + 100, y - 10);
    }

    /// <summary>Original <c>PaintGamesPerWeek</c>.</summary>
    private Zeichengroesse SpieleProWoche(double y)
    {
        y += 50;
        Titel("Anzahl Spiele pro Woche", y);
        y += 50;
        double xMax = 0;
        for (int i = 0; i < d.Wochen.Count; i++)
        {
            if (i > 0)
            {
                y += 30;
            }

            Text(d.Wochen[i].Titel, 0, y, Gross);
            Zeichengroesse s = Woche(y, d.Wochen[i]);
            y = s.Hoehe;
            xMax = Math.Max(xMax, s.Breite);
        }

        return new Zeichengroesse(xMax + 100, y + 40);
    }

    /// <summary>Original <c>PaintGamesPerWeekOneRound</c> mit <c>PaintGamesPerWeekHeader</c> und <c>PaintWeekValues</c>.</summary>
    private Zeichengroesse Woche(double y, Wochenstatistik w)
    {
        y += 30;
        Text("Spieltag", 0, y + 5, Normal, Beschriftung);
        Text("(Spiele die Woche / Spiele Gesamt)", 0, y + 45, Normal, Beschriftung);
        for (int i = 0; i < w.Montage.Count; i++)
        {
            double x = Links + (i * 70);
            Text((i + 1).ToString(Deutsch), x, y, Gross);
            Text(w.Montage[i].ToString("dd.MM", Deutsch), x, y + 25, Normal, Beschriftung);
            Text("- " + w.Montage[i].AddDays(6).ToString("dd.MM", Deutsch), x, y + 40, Normal, Beschriftung);
        }

        y += 75;
        double breite = Links + (w.Montage.Count * 70);
        Tabellenhintergrund(y - 8, 30, breite);
        double xMax = 0;
        for (int m = 0; m < Anzahl; m++)
        {
            int gesamt = 0;
            var werte = new List<string>();
            foreach (int anzahl in w.Spiele[m])
            {
                gesamt += anzahl;
                werte.Add(string.Create(Deutsch, $"{anzahl} / {gesamt}"));
            }

            xMax = Math.Max(xMax, Wochenwerte(y, d.Mannschaften[m].Name, werte, null));
            y += 30;
        }

        Linie(LinieGrau, 0, y, breite, y);
        y += 10;
        List<string> differenzen = w.MaxDifferenz.Select(x => x.ToString(Deutsch)).ToList();
        List<bool> rot = w.MaxDifferenz.Select(x => x >= 3).ToList();
        xMax = Math.Max(xMax, Wochenwerte(y, "maximale Differenz absolvierter Spiele", differenzen, rot));
        y += 30;
        return new Zeichengroesse(xMax + 100, y);
    }

    private double Wochenwerte(double y, string titel, List<string> werte, List<bool>? rot)
    {
        Text(titel, 0, y);
        double xMax = 100;
        for (int i = 0; i < werte.Count; i++)
        {
            double x = Links + (i * 70);
            Text(werte[i], x, y, Normal, rot is not null && rot[i] ? Schlecht : Tinte);
            xMax = Math.Max(xMax, x);
        }

        return xMax + 100;
    }

    /// <summary>Original <c>PaintSpielRanking</c> mit <c>PaintSpielRankingMannschaft</c>.</summary>
    private Zeichengroesse Setzliste(double y)
    {
        Titel("Setzliste", y);
        y += 10;
        Tabellenhintergrund(y + 22, 60, Links + (Amplitude * d.MaxSpiele) + (50 * d.Rundenanzahl) + 10);
        double xMax = 0;
        foreach (Diagrammmannschaft m in d.Mannschaften)
        {
            y += 60;
            Text(m.Name, 0, y);
            xMax = Math.Max(xMax, Setzlistenzeile(y, m, balken: false));
            Setzlistenzeile(y, m, balken: true);
        }

        return new Zeichengroesse(xMax + 100, y + 20);
    }

    private double Setzlistenzeile(double y, Diagrammmannschaft m, bool balken)
    {
        int zielAlt = 0;
        int ziel = Anzahl - 1;
        double x = Links;
        int runde = 0;
        double xMax = 0;
        foreach (Diagrammspiel spiel in m.Spiele)
        {
            if (runde < spiel.Runde)
            {
                runde = spiel.Runde;
                ziel = Anzahl - 1;
                x += 50;
            }

            if (!balken && zielAlt > ziel)
            {
                Linie(Silber, x - Amplitude - 3, y + 15 - (zielAlt * 5), x + Amplitude - 3, y + 15 - ((zielAlt - 2) * 5));
            }

            if (balken)
            {
                int wert = spiel.Setzlistenabstand;
                Rechteck(Balkenfarbe(wert, ziel), x, y + 20 - (wert * 5), x + (Amplitude / 2), y + 20);
            }

            zielAlt = ziel;
            x += Amplitude;
            xMax = Math.Max(xMax, x);
            ziel--;
        }

        return xMax;
    }

    /// <summary>Original <c>PaintDateLine</c>: Kalenderwochen.</summary>
    private void Datumszeile(double y)
    {
        Text("KW", 0, y, Normal, Beschriftung);
        for (DateTime tag = ErsterMontag(); tag < d.Letzter; tag = tag.AddDays(7))
        {
            double x1 = X(tag);
            double x2 = X(tag.AddDays(7));
            Linie(LinieGrau, x1, y, x1, y + 15);
            Linie(LinieGrau, x2, y, x2, y + 15);
            Text(ISOWeek.GetWeekOfYear(tag).ToString(Deutsch), x1 + 10, y, Normal, Beschriftung);
        }
    }

    /// <summary>Original <c>PaintDateRaster</c>: Wochenraster über die Zeilen.</summary>
    private void Datumsraster(double y, double hoehe)
    {
        double ende = 0;
        for (DateTime tag = ErsterMontag(); tag < d.Letzter; tag = tag.AddDays(7))
        {
            ende = X(tag.AddDays(7));
        }

        Tabellenhintergrund(y, hoehe, ende);
        for (int zeile = 0; zeile < Anzahl; zeile++)
        {
            double yZeile = y + (zeile * hoehe);
            for (DateTime tag = ErsterMontag(); tag < d.Letzter; tag = tag.AddDays(7))
            {
                Linie(LinieGrau, X(tag.AddDays(7)), yZeile - 10, X(tag.AddDays(7)), yZeile + hoehe);
                Linie(LinieGrau, X(tag), yZeile - 10, X(tag), yZeile + hoehe);
            }
        }
    }

    /// <summary>Original <c>PaintTableBackground</c>: jede zweite Zeile grau.</summary>
    private void Tabellenhintergrund(double y, double hoehe, double breite)
    {
        for (int zeile = 0; zeile < Anzahl; zeile += 2)
        {
            double yZeile = y + (zeile * hoehe);
            Rechteck(Band, 0, yZeile, breite, yZeile + hoehe);
        }
    }

    /// <summary>Original <c>PaintGameBubble</c>: Datum senkrecht, Kreis (Quadrat bei Koppeltermin).</summary>
    private void Blase(double x, double y, Diagrammspiel spiel)
    {
        Linie(Silber, x, y, x, y - 10);
        if (f is null)
        {
            return;
        }

        double schrifthoehe = Normal * Seitenumbruch.Zeilenfaktor;
        f.TextSenkrecht(spiel.Zeitpunkt!.Value.ToString("dd.MM", Deutsch), x - (schrifthoehe / 2) - 2, y - 12, Normal, Beschriftung);
        Farbe farbe = spiel.Heimspiel ? Heimspiel : Weiss;
        if (spiel.Koppel)
        {
            f.Rechteck(x - 5, y - 5, 10, 10, farbe);
            Linie(Heimspiel, x - 5, y - 5, x + 5, y - 5, 1.5);
            Linie(Heimspiel, x + 5, y - 5, x + 5, y + 5, 1.5);
            Linie(Heimspiel, x + 5, y + 5, x - 5, y + 5, 1.5);
            Linie(Heimspiel, x - 5, y + 5, x - 5, y - 5, 1.5);
        }
        else
        {
            f.Kreis(x, y, 5, farbe, Heimspiel, 1.5);
        }
    }

    private void Titel(string text, double y)
    {
        if (mitTiteln)
        {
            f?.Text(text, 0, y, Gross, Tinte, true);
        }
    }

    private void Text(string text, double x, double y, double groesse = Normal, Farbe? farbe = null) =>
        f?.Text(text, x, y, groesse, farbe ?? Tinte, false);

    private void Linie(Farbe farbe, double x1, double y1, double x2, double y2, double staerke = 1) => f?.Linie(x1, y1, x2, y2, farbe, staerke);

    private void Rechteck(Farbe farbe, double x1, double y1, double x2, double y2) => f?.Rechteck(x1, y1, x2 - x1, y2 - y1, farbe);
}
