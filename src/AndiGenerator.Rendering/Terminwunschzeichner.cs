// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Rendering;

/// <summary>
/// Die Terminwünsche als Kalender je Mannschaft (nicht im Original, dort <c>PaintSpielPlanMeldungen</c>): eine Spalte je
/// Kalenderwoche, je Mannschaft sieben Zeilen Mo bis So – jeder Tag hat seinen festen Platz, auch ein freier Dienstag. Dunkel
/// Heimspielwünsche, blau Koppeltermine, rot Sperrtermine („frei“); mehrere Arten an einem Tag teilen sich das Feld. Dieselbe
/// Routine zeichnet auf den Bildschirm und in den Ausdruck und misst ohne Zeichenfläche.
/// </summary>
public sealed class Terminwunschzeichner
{
    private const double NamenBreite = 260;
    private const double Wochenbreite = 30;
    private const double Tageshoehe = 4;
    private const double Tagesluecke = 1.2;
    private const double Blockabstand = 12;
    private const double Kopfhoehe = 24;
    private const double Schrift = 12;
    private const double Klein = 8;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");
    private static readonly string[] Wochentage = ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];

    private static readonly Farbe Leer = new(0xE9, 0xEC, 0xF0);
    private static readonly Farbe Wunsch = new(0x2E, 0x3A, 0x46);
    private static readonly Farbe Koppel = new(0x3B, 0x82, 0xC4);
    private static readonly Farbe Sperre = Farbe.Schlecht;
    private static readonly Farbe Band = new(0xF7, 0xF6, 0xF2);

    private readonly Terminwunschuebersicht u;
    private readonly IZeichenflaeche? f;
    private readonly bool mitLegende;
    private readonly List<Diagrammhinweis> hinweise = [];

    /// <summary>Initialisiert den Zeichner.</summary>
    /// <param name="uebersicht">Die Terminwünsche.</param>
    /// <param name="flaeche">Zeichenfläche oder <c>null</c> zum reinen Messen.</param>
    /// <param name="mitLegende">Legende unter dem Kalender zeichnen (Ausdruck).</param>
    public Terminwunschzeichner(Terminwunschuebersicht uebersicht, IZeichenflaeche? flaeche, bool mitLegende)
    {
        ArgumentNullException.ThrowIfNull(uebersicht);
        u = uebersicht;
        f = flaeche;
        this.mitLegende = mitLegende;
    }

    /// <summary>Holt die Stellen der letzten Zeichnung (je Mannschaft und Woche mit Einträgen) mit ihren Texten.</summary>
    public IReadOnlyList<Diagrammhinweis> Hinweise => hinweise;

    private static double Blockhoehe => (7 * (Tageshoehe + Tagesluecke)) + Blockabstand;

    /// <summary>Zeichnet den Kalender.</summary>
    /// <returns>Benötigte Größe; leer, wenn es keine Termine gibt.</returns>
    public Zeichengroesse Zeichnen()
    {
        hinweise.Clear();
        if (u is not { Erster: DateOnly erster, Letzter: DateOnly letzter })
        {
            return default;
        }

        DateOnly montag = Montag(erster);
        int wochen = ((letzter.DayNumber - montag.DayNumber) / 7) + 1;
        double breite = NamenBreite + (wochen * Wochenbreite) + 10;
        Text("KW", 0, 4, Schrift, Farbe.Grau);
        for (int w = 0; w < wochen; w++)
        {
            DateOnly tag = montag.AddDays(7 * w);
            string kw = ISOWeek.GetWeekOfYear(tag.ToDateTime(TimeOnly.MinValue)).ToString(Deutsch);
            Mittig(kw, NamenBreite + (w * Wochenbreite) + (Wochenbreite / 2), 4, Schrift - 1, Farbe.Grau);
        }

        for (int i = 0; i < u.Mannschaften.Count; i++)
        {
            double oben = Kopfhoehe + (i * Blockhoehe);
            if (i % 2 == 1)
            {
                f?.Rechteck(0, oben - (Blockabstand / 2), breite, Blockhoehe, Band);
            }

            Mannschaft(u.Mannschaften[i], oben, montag, wochen);
        }

        double hoehe = Kopfhoehe + (u.Mannschaften.Count * Blockhoehe);
        if (mitLegende)
        {
            Legende(hoehe + 4);
            hoehe += 30;
        }

        return new Zeichengroesse(breite, hoehe);
    }

    private static DateOnly Montag(DateOnly tag) => tag.AddDays(-(((int)tag.DayOfWeek + 6) % 7));

    private static Farbe Markenfarbe(Terminwunschart art) => art switch
    {
        Terminwunschart.Koppel => Koppel,
        Terminwunschart.Sperrtermin => Sperre,
        _ => Wunsch,
    };

    private static string Artname(Terminwunschart art) => art switch
    {
        Terminwunschart.Koppel => "Koppeltermin",
        Terminwunschart.Sperrtermin => "frei (Sperrtermin)",
        _ => "Heimspielwunsch",
    };

    /// <summary>Der Text zu einer Woche: Mannschaft, Woche und je Tag die Einträge.</summary>
    private static string Wochenhinweis(string name, DateOnly montag, IEnumerable<Terminwunschmarke> marken)
    {
        int kw = ISOWeek.GetWeekOfYear(montag.ToDateTime(TimeOnly.MinValue));
        var zeilen = new List<string>
        {
            string.Create(Deutsch, $"{name}, KW {kw} ({montag:dd.MM.} – {montag.AddDays(6):dd.MM.yyyy})"),
        };
        zeilen.AddRange(marken
            .OrderBy(m => m.Tag)
            .ThenBy(m => m.Art)
            .Select(m => Wochentage[((int)m.Tag.DayOfWeek + 6) % 7] + " " + m.Tag.ToString("dd.MM.", Deutsch) + "  " + Artname(m.Art)));
        return string.Join(Environment.NewLine, zeilen);
    }

    /// <summary>Der Kalender einer Mannschaft: sieben Zeilen Mo bis So, eine Spalte je Woche.</summary>
    private void Mannschaft(MannschaftsTerminwuensche m, double oben, DateOnly montag, int wochen)
    {
        double hoehe = 7 * (Tageshoehe + Tagesluecke);
        Text(m.Name, 0, oben + (hoehe / 2) - 8, Schrift, Farbe.Tinte);
        foreach (int d in new[] { 0, 5, 6 })
        {
            Text(Wochentage[d], NamenBreite - 16, oben + (d * (Tageshoehe + Tagesluecke)) - 3, Klein - 1, Farbe.Grau);
        }

        Dictionary<int, List<Terminwunschmarke>> jeTag = m.Marken
            .GroupBy(x => x.Tag.DayNumber - montag.DayNumber)
            .ToDictionary(g => g.Key, g => g.Select(x => x).Distinct().ToList());
        for (int w = 0; w < wochen; w++)
        {
            double x = NamenBreite + (w * Wochenbreite) + 3;
            double b = Wochenbreite - 6;
            for (int d = 0; d < 7; d++)
            {
                double y = oben + (d * (Tageshoehe + Tagesluecke));
                if (!jeTag.TryGetValue((7 * w) + d, out List<Terminwunschmarke>? marken))
                {
                    f?.Rechteck(x, y, b, Tageshoehe, Leer);
                    continue;
                }

                // Mehrere Arten an einem Tag (etwa Wunsch und Sperre) teilen sich das Feld nebeneinander.
                List<Terminwunschart> arten = marken.Select(z => z.Art).Distinct().Order().ToList();
                double teil = b / arten.Count;
                for (int a = 0; a < arten.Count; a++)
                {
                    f?.Rechteck(x + (a * teil), y, teil - (a < arten.Count - 1 ? 0.6 : 0), Tageshoehe, Markenfarbe(arten[a]));
                }
            }

            List<Terminwunschmarke> woche = Enumerable.Range(7 * w, 7).Where(jeTag.ContainsKey).SelectMany(k => jeTag[k]).ToList();
            if (woche.Count > 0)
            {
                hinweise.Add(new Diagrammhinweis(
                    x + (b / 2), oben + (hoehe / 2), 0, Wochenhinweis(m.Name, montag.AddDays(7 * w), woche), Wochenbreite, hoehe + 4));
            }
        }
    }

    /// <summary>Die Legende unter dem Kalender (Ausdruck).</summary>
    private void Legende(double y)
    {
        double x = 4;
        foreach ((string text, Farbe farbe) in Legendeneintraege())
        {
            f?.Rechteck(x, y + 5, 22, Tageshoehe + 1, farbe);
            Text(text, x + 28, y, Schrift, Farbe.Tinte);
            x += 32 + (text.Length * Schrift * 0.58) + 18;
        }

        Text("je Mannschaft Zeilen Mo bis So, eine Spalte je Woche", x, y, Schrift, Farbe.Grau);
    }

    private List<(string Text, Farbe Farbe)> Legendeneintraege()
    {
        var eintraege = new List<(string Text, Farbe Farbe)> { ("Heimspielwunsch", Wunsch) };
        if (u.HatKoppeltermine)
        {
            eintraege.Add(("Koppeltermin", Koppel));
        }

        eintraege.Add(("frei (Sperrtermin)", Sperre));
        eintraege.Add(("nichts gemeldet", Leer));
        return eintraege;
    }

    private void Text(string text, double x, double y, double groesse, Farbe farbe) => f?.Text(text, x, y, groesse, farbe, false);

    private void Mittig(string text, double x, double y, double groesse, Farbe farbe)
    {
        if (f is null)
        {
            return;
        }

        f.Text(text, x - (f.TextBreite(text, groesse, false) / 2), y, groesse, farbe, false);
    }
}
