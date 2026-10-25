// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Rendering;

/// <summary>Das Diagramm „Spiele pro Woche“ als Raster mit einem Punkt je Spiel (nicht im Original).</summary>
public sealed partial class Diagrammzeichner
{
    private const double Wochenbreite = 54;
    private const double Wochenhoehe = 32;

    private static readonly Farbe Wochenflaeche = new(0xE8, 0xF0, 0xFA);
    private static readonly Farbe Vieleflaeche = new(0xFB, 0xEA, 0xE8);

    /// <summary>Farbe des Abstands zwischen erster und letzter Mannschaft: grün bis 2, orange bis 4, sonst rot.</summary>
    private static Farbe Abstandsfarbe(int abstand)
    {
        if (abstand <= 2)
        {
            return Gut;
        }

        return abstand <= 4 ? Pausenorange : Schlecht;
    }

    private static double Wochenx(int woche) => Links + (woche * Wochenbreite);

    /// <summary>Spiele der Woche, die auf ein Spiel derselben Woche folgen (außer am Koppeltermin): die Verstöße dieser Woche.</summary>
    private static int Wochenpaare(Diagrammmannschaft m, DateOnly montag)
    {
        List<Diagrammspiel> spiele = m.Spiele
            .Where(s => s.Zeitpunkt is not null && DateOnly.FromDateTime(s.Zeitpunkt.Value) >= montag && DateOnly.FromDateTime(s.Zeitpunkt.Value) < montag.AddDays(7))
            .OrderBy(s => s.Zeitpunkt)
            .ToList();
        int paare = 0;
        for (int i = 1; i < spiele.Count; i++)
        {
            paare += spiele[i - 1].Koppel && spiele[i].Koppel ? 0 : 1;
        }

        return paare;
    }

    private static string Spielzahl(int anzahl) => anzahl == 1 ? "1 Spiel" : string.Create(CultureInfo.InvariantCulture, $"{anzahl} Spiele");

    /// <summary>
    /// Je Runde ein Raster: eine Zeile je Mannschaft, eine Spalte je Woche (Montag bis Sonntag). In jeder Zelle ein Punkt je
    /// Spiel; rosa, wo die Bewertung „2 Spiele die Woche“ zählt. Rechts die Spiele der Runde, unten je Woche als Säule, wie weit die
    /// Mannschaft mit den meisten und die mit den wenigsten Spielen auseinanderliegen. Der Hinweis einer Zelle nennt die
    /// Spiele der Woche und den Stand danach.
    /// </summary>
    private Zeichengroesse SpieleProWoche(double y)
    {
        Titel("Spiele pro Woche", y);
        y += 30;
        double breite = 500;
        foreach (Wochenstatistik w in d.Wochen)
        {
            Zeichengroesse s = Wochenraster(w, y);
            breite = Math.Max(breite, s.Breite);
            y = s.Hoehe + 20;
        }

        double x = 0;
        f?.Kreis(x + 5, y + 8, 4.5, Heimspiel, null, 0);
        x = Legendentext("ein Spiel", x + 14, y);
        f?.Vieleck(Rundrechteck.Punkte(x, y, x + 30, y + 16, 5), Vieleflaeche, null, 0, geschlossen: true);
        x = Legendentext("zählt als „2 Spiele die Woche“ (wie in der Qualitätsansicht)", x + 36, y);
        foreach ((string text, int wert) in new[] { ("bis 2", 2), ("3 bis 4", 4), ("ab 5", 5) })
        {
            f?.Vieleck(Rundrechteck.Punkte(x, y + 2, x + 12, y + 14, 3), Abstandsfarbe(wert), null, 0, geschlossen: true);
            x = Legendentext(text, x + 18, y) - 12;
        }

        x = Legendentext("Abstand zwischen der Mannschaft mit den meisten und der mit den wenigsten Spielen", x + 12, y);
        return new Zeichengroesse(Math.Max(breite, x), y + 26);
    }

    private Zeichengroesse Wochenraster(Wochenstatistik w, double y)
    {
        f?.Text(w.Titel, 0, y, Gross, Tinte, false);
        y += 30;
        int wochen = w.Montage.Count;
        for (int k = 0; k < wochen; k++)
        {
            double mitte = Wochenx(k) + (Wochenbreite / 2);
            if (f is not null)
            {
                Mittig((k + 1).ToString(CultureInfo.InvariantCulture), mitte, y + 6, Normal, Tinte, true);
                Mittig(w.Montage[k].ToString("dd.MM.", Deutsch), mitte, y + 21, Klein, Beschriftung, false);
            }
        }

        double rechts = Wochenx(wochen) + 12;
        Text("Woche ab Montag", 0, y + 14, Klein, Beschriftung);
        Text("Spiele", rechts, y + 14, Klein, Beschriftung);
        Text("2 je Woche", rechts + 50, y + 14, Klein, Beschriftung);
        double breite = rechts + 120;
        y += 34;
        for (int i = 0; i < d.Mannschaften.Count && i < w.Spiele.Count; i++)
        {
            double oben = y + (i * Wochenhoehe);
            if (i % 2 == 1)
            {
                Rechteck(Band, 0, oben, breite, oben + Wochenhoehe);
            }

            Wochenzeile(d.Mannschaften[i], w, i, oben, rechts);
        }

        y += Math.Min(d.Mannschaften.Count, w.Spiele.Count) * Wochenhoehe;
        y += 12;
        Text("Abstand erste – letzte", 0, y + 18, Normal, Beschriftung);
        Text("(absolvierte Spiele)", 0, y + 34, Normal, Beschriftung);
        int groesster = Math.Max(1, w.MaxDifferenz.DefaultIfEmpty(0).Max());
        double einheit = Math.Min(8, 48.0 / groesster);
        double grund = y + 62;
        for (int k = 0; k < w.MaxDifferenz.Count && k < wochen; k++)
        {
            int v = w.MaxDifferenz[k];
            double mitte = Wochenx(k) + (Wochenbreite / 2);
            double h = v * einheit;
            Farbe farbe = Abstandsfarbe(v);
            if (v > 0)
            {
                f?.Vieleck(Rundrechteck.Punkte(mitte - 9, grund - h, mitte + 9, grund, 3), farbe, null, 0, geschlossen: true);
            }

            if (f is not null)
            {
                Mittig(v.ToString(CultureInfo.InvariantCulture), mitte, grund - h - 10, Klein + 1, farbe, true);
            }
        }

        Linie(Spannengrau, Links, grund, Wochenx(wochen), grund);
        return new Zeichengroesse(breite, grund + 8);
    }

    /// <summary>Eine Mannschaft: je Woche mit Spielen eine Fläche mit einem Punkt je Spiel und dem Hinweis dazu.</summary>
    private void Wochenzeile(Diagrammmannschaft m, Wochenstatistik w, int index, double oben, double rechts)
    {
        double mitte = oben + (Wochenhoehe / 2);
        Name(m.Name, mitte - 8);
        IReadOnlyList<int> spiele = w.Spiele[index];
        Mannschaftsverstoesse? v = d.Verstoesse?.Von(m.Name);
        bool zaehlt = d.Verstoesse is null || v?.ZweiSpieleProWoche is not null;
        int stand = 0;
        int eigene = 0;
        for (int k = 0; k < spiele.Count && k < w.Montage.Count; k++)
        {
            int anzahl = spiele[k];
            stand += anzahl;
            if (anzahl <= 0)
            {
                continue;
            }

            double x = Wochenx(k);
            int paare = Wochenpaare(m, w.Montage[k]);
            eigene += paare;
            bool viele = zaehlt && paare > 0;
            f?.Vieleck(Rundrechteck.Punkte(x + 3, oben + 4, x + Wochenbreite - 3, oben + Wochenhoehe - 4, 6), viele ? Vieleflaeche : Wochenflaeche, null, 0, geschlossen: true);
            double radius = anzahl <= 2 ? 4.5 : 3.5;
            double schritt = anzahl <= 2 ? 13 : Math.Min(10, (Wochenbreite - 14) / anzahl);
            double start = x + (Wochenbreite / 2) - (((anzahl - 1) * schritt) / 2);
            for (int q = 0; q < anzahl; q++)
            {
                f?.Kreis(start + (q * schritt), mitte, radius, viele ? Schlecht : Heimspiel, null, 0);
            }

            hinweise.Add(new Diagrammhinweis(
                x + (Wochenbreite / 2), mitte, Wochenhoehe / 2, Wochenhinweis(m, w.Montage[k], anzahl, stand, viele ? paare : 0), Wochenbreite - 6, Wochenhoehe - 8));
        }

        f?.Text(stand.ToString(CultureInfo.InvariantCulture), rechts + 8, mitte - 8, Normal, Tinte, true);

        // Wie in der Qualitätsansicht: je Spiel, das in derselben Woche wie das vorige liegt (außer am Koppeltermin), ein Verstoß.
        // Bei mehreren Runden zählt jedes Raster nur seine Wochen; die Zahl der Bewertung gilt für die ganze Saison.
        string verstoesse = d.Verstoesse is null || (d.Wochen.Count > 1 && zaehlt)
            ? eigene.ToString(CultureInfo.InvariantCulture)
            : v?.ZweiSpieleProWoche?.ToString(CultureInfo.InvariantCulture) ?? "–";
        bool rot = verstoesse is not ("0" or "–");
        f?.Text(verstoesse, rechts + 58, mitte - 8, Normal, rot ? Schlecht : Tinte, rot);
    }

    /// <summary>Der Text zu einer Woche: Anzahl, die Spiele mit Tag und Gegner (bei Kompaktspieltagen mit Halle), Stand danach.</summary>
    private string Wochenhinweis(Diagrammmannschaft m, DateOnly montag, int anzahl, int stand, int verstoesse)
    {
        var zeilen = new List<string>
        {
            m.Name + ": " + Spielzahl(anzahl) + " in der Woche ab " + montag.ToString("dd.MM.yyyy", Deutsch),
        };
        foreach (Diagrammspiel s in m.Spiele.Where(s => s.Zeitpunkt is not null && DateOnly.FromDateTime(s.Zeitpunkt.Value) >= montag && DateOnly.FromDateTime(s.Zeitpunkt.Value) < montag.AddDays(7)).OrderBy(s => s.Zeitpunkt))
        {
            DateTime zeit = s.Zeitpunkt!.Value;
            Ausrichtertag? turnier = Kompakttag(m.Name, DateOnly.FromDateTime(zeit));
            string gegen = s.Gegner.Length == 0 ? string.Empty : " gegen " + s.Gegner;
            string ort = turnier switch
            {
                null => s.Heimspiel ? " (Heim)" : " (auswärts)",
                { Ausrichter: null } => string.Empty,
                { Ausrichter: string a } when a == m.Name => " (eigene Halle)",
                { Ausrichter: string a } => " (in der Halle von " + a + ")",
            };
            zeilen.Add("   " + zeit.ToString("ddd dd.MM.", Deutsch) + gegen + ort);
        }

        zeilen.Add("danach " + Spielzahl(stand) + " in dieser Runde");
        if (verstoesse > 0)
        {
            zeilen.Add(string.Create(CultureInfo.InvariantCulture, $"zählt {verstoesse}× als „2 Spiele die Woche“"));
        }

        return string.Join(Environment.NewLine, zeilen);
    }
}
