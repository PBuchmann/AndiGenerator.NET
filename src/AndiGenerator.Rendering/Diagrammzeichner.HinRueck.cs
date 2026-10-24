// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Rendering;

/// <summary>Das Diagramm „Abstand Hin-/Rückspiel“ als Streifen je Mannschaft (nicht im Original).</summary>
public sealed partial class Diagrammzeichner
{
    private const double Streifenhoehe = 40;
    private const double Abstandsbreite = 40;
    private const int Sollspanne = 2;

    private static readonly Farbe Sollband = new(0xE6, 0xF2, 0xE9);
    private static readonly Farbe Sollpunkt = new(0x7F, 0xB8, 0x94);

    /// <summary>
    /// Bewertet einen Abstand: im Soll bis zwei Spiele um den idealen Abstand, weit daneben ab der Hälfte des idealen
    /// Abstands nach unten wie nach oben (bei 9: bis 4 und ab 14), sonst auffällig (knapp oder zu lang).
    /// </summary>
    /// <param name="abstand">Abstand in Spielen.</param>
    /// <param name="ideal">Idealer Abstand (Spiele je Runde).</param>
    /// <returns>Die Stufe.</returns>
    public static Abstandsstufe Bewerten(int abstand, double ideal)
    {
        if (Math.Abs(abstand - ideal) <= Sollspanne)
        {
            return Abstandsstufe.ImSoll;
        }

        return Math.Abs(abstand - ideal) >= ideal / 2 ? Abstandsstufe.WeitDaneben : Abstandsstufe.Auffaellig;
    }

    private static string Spieletext(int anzahl) =>
        anzahl == 1 ? "1 Spiel" : string.Create(CultureInfo.InvariantCulture, $"{anzahl} Spiele");

    private static double Abstandx(double abstand) => Links + ((abstand - 1) * Abstandsbreite);

    private static string Idealtext(double ideal) => ideal.ToString("0.#", Deutsch);

    /// <summary>Der Text zu einem Punkt: Abstand mit Bewertung, darunter je Gegner die beiden Termine.</summary>
    private static string Abstandshinweis(int abstand, double ideal, IReadOnlyList<Abstandslinie> linien)
    {
        string stufe = Bewerten(abstand, ideal) switch
        {
            Abstandsstufe.ImSoll => "im Soll",
            Abstandsstufe.WeitDaneben => abstand < ideal ? "viel zu kurz" : "viel zu lang",
            _ => abstand < ideal ? "knapp" : "zu lang",
        };
        var zeilen = new List<string> { $"Abstand {Spieletext(abstand)} – {stufe} (ideal {Idealtext(ideal)})" };
        zeilen.AddRange(linien.Select(l =>
            $"{l.Gegner}: {l.Von.ToString("ddd dd.MM.yyyy", Deutsch)} und {l.Bis.ToString("ddd dd.MM.yyyy", Deutsch)}"));
        return string.Join(Environment.NewLine, zeilen);
    }

    /// <summary>
    /// Je Mannschaft ein Streifen über den Abstand in Spielen: jeder Gegner sitzt beim Abstand zwischen den Spielen gegen ihn.
    /// Das grüne Band ist das Soll (idealer Abstand ± 2 Spiele); Gegner im Soll werden zu einem Punkt mit Anzahl
    /// zusammengefasst, auffällige orange, weit danebenliegende (viel zu kurz oder viel zu lang) rot. Die Gegner nennt der Hinweis des Punkts.
    /// </summary>
    private Zeichengroesse AbstandHinRueck(double y)
    {
        Titel("Abstand Hin-/Rückspiel", y);
        y += 30;
        List<Abstandslinie> alle = d.Mannschaften.SelectMany(m => m.Abstaende.SelectMany(z => z)).ToList();
        double groesstesIdeal = alle.Count == 0 ? 1 : alle.Max(l => l.Ideal);
        int bis = Math.Max(alle.Count == 0 ? 1 : alle.Max(l => l.Abstand), (int)Math.Ceiling(groesstesIdeal) + Sollspanne + 1);
        for (int a = 1; a <= bis; a++)
        {
            bool ideal = alle.Exists(l => Math.Abs(l.Ideal - a) < 0.01);
            string zahl = a.ToString(CultureInfo.InvariantCulture);
            if (f is not null)
            {
                Mittig(zahl, Abstandx(a), y + 8, Klein, ideal ? Gut : Beschriftung, ideal);
            }
        }

        double rechts = Abstandx(bis) + 30;
        Text("Abstand in Spielen", 0, y, Klein, Beschriftung);
        Text("Abweichung", rechts, y, Klein, Beschriftung);
        y += 22;
        double oben = y;
        double breite = rechts + 80;
        for (int i = 0; i < d.Mannschaften.Count; i++)
        {
            double zeile = oben + (i * Streifenhoehe);
            if (i % 2 == 1)
            {
                Rechteck(Band, 0, zeile, breite, zeile + Streifenhoehe);
            }

            Streifen(d.Mannschaften[i], zeile, bis, rechts);
        }

        y = oben + (d.Mannschaften.Count * Streifenhoehe) + 16;
        Rechteck(Sollband, 0, y, 30, y + 16);
        double x = Legendentext("im Soll (ideal ± 2 Spiele)", 36, y);
        f?.Kreis(x + 6, y + 8, 6, Pausenorange, null, 0);
        x = Legendentext("knapp oder zu lang", x + 18, y);
        f?.Kreis(x + 6, y + 8, 6, Schlecht, null, 0);
        x = Legendentext("viel zu kurz oder viel zu lang (die Hälfte daneben)", x + 18, y);
        x = Legendentext("Ideal = Spiele je Runde · Abweichung rechts wie in der Qualitätsansicht", x, y);
        return new Zeichengroesse(Math.Max(breite, x), y + 26);
    }

    private void Streifen(Diagrammmannschaft m, double oben, int bis, double rechts)
    {
        double mitte = oben + (Streifenhoehe / 2);
        Text(m.Name, 0, mitte - 8);
        List<Abstandslinie> linien = m.Abstaende.SelectMany(z => z).ToList();
        if (linien.Count == 0)
        {
            return;
        }

        double ideal = linien[0].Ideal;
        Rechteck(Sollband, Abstandx(ideal - Sollspanne) - (Abstandsbreite / 2), oben + 2, Abstandx(ideal + Sollspanne) + (Abstandsbreite / 2), oben + Streifenhoehe - 2);
        Linie(Gut, Abstandx(ideal), oben + 4, Abstandx(ideal), oben + Streifenhoehe - 4);
        Linie(LinieGrau, Abstandx(1), mitte, Abstandx(bis), mitte, 1.5);
        foreach (IGrouping<int, Abstandslinie> gruppe in linien.GroupBy(l => l.Abstand).OrderBy(g => g.Key))
        {
            int anzahl = gruppe.Count();
            Abstandsstufe stufe = Bewerten(gruppe.Key, ideal);
            Farbe farbe = stufe switch
            {
                Abstandsstufe.WeitDaneben => Schlecht,
                Abstandsstufe.Auffaellig => Pausenorange,
                _ => Sollpunkt,
            };
            double radius = Math.Min(15, (stufe == Abstandsstufe.ImSoll ? 6 : 7) + (2 * (anzahl - 1)));
            double x = Abstandx(gruppe.Key);
            f?.Kreis(x, mitte, radius, farbe, Weiss, 1.5);
            if (anzahl > 1 && f is not null)
            {
                Mittig(anzahl.ToString(CultureInfo.InvariantCulture), x, mitte, Klein, Weiss, true);
            }

            hinweise.Add(new Diagrammhinweis(x, mitte, Math.Max(radius, 7), Abstandshinweis(gruppe.Key, ideal, gruppe.ToList())));
        }

        // Rechts wie in der Qualitätsansicht: die größte Abweichung eines Abstands vom Mittel der Mannschaft (gerundet).
        double mittel = linien.Average(l => l.Abstand);
        int eigene = linien.Max(l => (int)Math.Floor(Math.Abs(l.Abstand - mittel) + 0.5));
        Mannschaftsverstoesse? v = d.Verstoesse?.Von(m.Name);
        string wert = d.Verstoesse is null ? eigene.ToString(CultureInfo.InvariantCulture) : v?.AbstandHinRueck?.ToString(CultureInfo.InvariantCulture) ?? "–";
        bool rot = wert is not ("0" or "–");
        f?.Text(wert, rechts + 8, mitte - 8, Normal, rot ? Schlecht : Tinte, rot);
    }
}
