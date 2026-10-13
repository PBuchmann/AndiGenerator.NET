// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Presentation;

/// <summary>
/// Punkte eines Plans aus Kompaktspieltagen wie beim besten Gesamtpaket (kleiner ist besser), mit Begründung: je ein Punkt
/// für 10 % mehr Kilometer als der kürzeste Plan nur aus 4er-Spieltagen (Fahrten zu Einzelbegegnungen zählen doppelt), einen Spieltag Rückstand im Stand (über 2 Spiele),
/// keinen Sonntag mit zwei 4er-Spieltagen, einen früh liegenden Spitzen- oder Abstiegsspieltag (bis zu einem halben Punkt)
/// und ein Viertel der mittleren Kilometer einer Mannschaft, um das der Unterschied zwischen den
/// Mannschaften über dem kleinsten liegt; Verstöße je 10, Mannschaften ohne 3er- oder 4er-Heimspieltag je 15, ein fehlender Spitzen- oder Abstiegsspieltag je 3, 3er-Spieltage je 3 Punkte, Einzelspiele bei ungerader
/// Mannschaftszahl je 2 Punkte.
/// </summary>
internal static class Kompaktpunkte
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private static readonly string[] Gruppennamen = ["Spitzenspieltag", "Abstiegsspieltag"];

    /// <summary>
    /// Bezug der Punkte: Kilometer des kürzesten Plans und kleinster Unterschied zwischen den Mannschaften – unter den
    /// Varianten nur aus 4er-Spieltagen, wenn es solche gibt, sonst unter allen (z. B. bei 6 bis 8 Mannschaften).
    /// </summary>
    /// <param name="varianten">Die berechneten Varianten.</param>
    /// <returns>Der Bezug oder <c>null</c>, wenn es keine Varianten gibt.</returns>
    public static Kompaktbezug? Bezug(IEnumerable<Kompaktvariante> varianten)
    {
        List<Kompaktkennzahlen> alle = varianten.Select(v => v.Kennzahlen).ToList();
        List<Kompaktkennzahlen> vierer = alle.Where(k => k.Dreier == 0).ToList();
        List<Kompaktkennzahlen> bezug = vierer.Count > 0 ? vierer : alle;
        return bezug.Count == 0 ? null : new Kompaktbezug(bezug.Min(k => k.Bewertungskm), bezug.Min(k => k.KmSpanne), vierer.Count > 0);
    }

    /// <summary>Unterschied der Kilometer zwischen der Mannschaft mit den meisten und der mit den wenigsten.</summary>
    /// <param name="a">Auswertung des Plans.</param>
    /// <returns>Die Kilometer.</returns>
    public static double Spanne(Kompaktauswertung a) =>
        a.Mannschaften.Count == 0 ? 0 : a.Mannschaften.Max(m => m.Km) - a.Mannschaften.Min(m => m.Km);

    /// <summary>Wie viele der Gruppen (Spitze, Keller) einen eigenen 4er-Spieltag haben.</summary>
    /// <param name="a">Auswertung des Plans.</param>
    /// <param name="gruppen">Die Gruppen zu je vier Mannschaften (die beiden Stärksten bzw. Schwächsten zuerst).</param>
    /// <returns>Die Zahl.</returns>
    public static int Gruppentage(Kompaktauswertung a, IReadOnlyList<IReadOnlyList<string>> gruppen) =>
        gruppen.Count(g => Gruppentag(a, g) is not null);

    /// <summary>Die Punkte und ihre Gründe.</summary>
    /// <param name="a">Auswertung des Plans.</param>
    /// <param name="bezug">Kürzester Plan, kleinster Unterschied und ob 3er-Spieltage Punkte kosten.</param>
    /// <param name="gruppen">Spitze und Keller zu je vier Mannschaften, oder leer.</param>
    /// <returns>Summe und Gründe, z. B. „0,6 für 6,0 % mehr Kilometer …“.</returns>
    public static (double Summe, IReadOnlyList<string> Gruende) Berechnen(Kompaktauswertung a, Kompaktbezug bezug, IReadOnlyList<IReadOnlyList<string>> gruppen)
    {
        ArgumentNullException.ThrowIfNull(bezug);
        double kmMin = bezug.Km;
        double spanneMin = bezug.Spanne;
        var gruende = new List<string>();
        double summe = 0;

        void Teil(double punkte, string grund)
        {
            if (punkte >= 0.05)
            {
                summe += punkte;
                gruende.Add(punkte.ToString("N1", Deutsch) + " für " + grund);
            }
        }

        int n = a.Mannschaften.Count;

        // Einzelfahrten zählen doppelt (Peter, 13.10.2026): Eine Mannschaft fährt dafür weit für ein einziges Spiel.
        double einzelKm = a.Spieltage.Where(s => s.Form == Turnierform.Einzel).Sum(s => s.Km);
        double bewertungskm = a.KmGesamt + einzelKm;
        if (kmMin > 0 && bewertungskm > kmMin)
        {
            double anteil = (bewertungskm / kmMin) - 1;
            string vergleich = bezug.MitVierern ? "der kürzeste Plan nur aus 4er-Spieltagen" : "der kürzeste Plan";
            string doppelt = einzelKm > 0.5 ? string.Create(Deutsch, $", Einzelfahrten ({einzelKm:N0} km) doppelt gezählt") : string.Empty;
            Teil(anteil / 0.1, string.Create(Deutsch, $"{100 * anteil:N1} % mehr Kilometer als {vergleich}{doppelt}"));
        }

        if (a.GroessterAbstand > 2)
        {
            Teil((a.GroessterAbstand - 2) / 2.0, string.Create(Deutsch, $"den Stand (bis {a.GroessterAbstand} Spiele auseinander)"));
        }

        if (a.MeisteAmTag < 4 * (n / 4))
        {
            Teil(1, "keinen Sonntag mit zwei 4er-Spieltagen");
        }

        GruppenWerten(a, gruppen, Teil);
        double jeMannschaft = n > 0 ? kmMin / n : 0;
        double mehrUnterschied = Spanne(a) - spanneMin;
        if (jeMannschaft > 0 && mehrUnterschied > 0.5)
        {
            Teil(mehrUnterschied / (0.25 * jeMannschaft), string.Create(Deutsch, $"ungleich weite Fahrten ({mehrUnterschied:N0} km mehr Unterschied als der kleinste)"));
        }

        Teil(10.0 * a.Verstoesse, string.Create(Deutsch, $"{a.Verstoesse} Verstöße gegen die Meldungen"));
        Teil(15.0 * a.OhneHeimspieltag, string.Create(Deutsch, $"{a.OhneHeimspieltag} Mannschaften ohne 3er- oder 4er-Heimspieltag"));
        int ganzOhne = a.Mannschaften.Count(m => m.Ausgerichtet + m.EinzelHeim == 0);
        Teil(2.0 * ganzOhne, string.Create(Deutsch, $"{ganzOhne} Mannschaften ganz ohne Heimspiel (auch kein Einzel zuhause)"));
        int dreier = a.Spieltage.Count(s => s.Form == Turnierform.Dreier);
        if (bezug.MitVierern)
        {
            Teil(3.0 * dreier, string.Create(Deutsch, $"{dreier} 3er-Spieltage"));
        }

        // Bei ungerader Zahl sind Einzelspiele ein Notbehelf (bei gerader gehört je Mannschaft eines dazu).
        int einzel = a.Spieltage.Count(s => s.Form == Turnierform.Einzel);
        if (n % 2 == 1)
        {
            Teil(2.0 * einzel, string.Create(Deutsch, $"{einzel} Einzelspiele"));
        }

        return (summe, gruende);
    }

    /// <summary>
    /// Der Spitzen- bzw. Abstiegsspieltag: ein 4er-Spieltag der vier (die ersten beiden gegeneinander) oder ein 3er-Spieltag
    /// der ersten drei (Peter, 13.10.2026: in Plänen aus 3er-Spieltagen bilden die drei Stärksten bzw. Schwächsten die Gruppe).
    /// </summary>
    /// <param name="a">Auswertung des Plans.</param>
    /// <param name="g">Die Gruppe, die stärkste bzw. schwächste Mannschaft zuerst.</param>
    /// <returns>Der Spieltag oder <c>null</c>.</returns>
    public static Kompaktspieltag? Gruppentag(Kompaktauswertung a, IReadOnlyList<string> g)
    {
        Kompaktspieltag? vierer = g.Count == 4
            ? a.Spieltage.FirstOrDefault(s => s.Form == Turnierform.Vierer && s.Mannschaften.Count == 4 && g.All(s.Mannschaften.Contains)
                && s.Spiele.Any(sp => (sp.Heim == g[0] && sp.Gast == g[1]) || (sp.Heim == g[1] && sp.Gast == g[0])))
            : null;
        return vierer ?? a.Spieltage.FirstOrDefault(s => s.Form == Turnierform.Dreier && g.Take(3).All(s.Mannschaften.Contains));
    }

    private static void GruppenWerten(Kompaktauswertung a, IReadOnlyList<IReadOnlyList<string>> gruppen, Action<double, string> teil)
    {
        if (a.Spieltage.Count == 0)
        {
            return;
        }

        DateOnly erster = a.Spieltage.Min(s => s.Tag);
        DateOnly letzter = a.Spieltage.Max(s => s.Tag);
        for (int i = 0; i < gruppen.Count && i < Gruppennamen.Length; i++)
        {
            if (gruppen[i].Count < 3)
            {
                continue;
            }

            if (Gruppentag(a, gruppen[i]) is not Kompaktspieltag tag)
            {
                teil(3, "den fehlenden " + Gruppennamen[i]);
            }
            else if (letzter > erster)
            {
                double frueh = (letzter.DayNumber - tag.Tag.DayNumber) / (double)(letzter.DayNumber - erster.DayNumber);
                teil(0.5 * frueh, string.Create(Deutsch, $"den frühen {Gruppennamen[i]} ({tag.Tag:dd.MM.})"));
            }
        }
    }
}
