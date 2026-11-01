// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Engine.Einfach;

/// <summary>
/// Wonach der einfache Modus Pläne vergleicht – vom Klassenleiter steuerbar (Peter, 28.10.2026: „nicht der beste Plan, sondern
/// der beste für die gewählten Kriterien“). Fest: erst die Pflicht, dann die Wünsche der Vereine. Danach, wenn gewählt, die
/// <see cref="Fokus"/>-Gruppe (strikt: sie wird besser, auch wenn andere dafür schlechter werden), zuletzt die Güte: die Summe
/// der Gruppen mit ihren <see cref="Gewichte"/>n. Eine Gruppe ist der Mittelwert ihrer Kriterien, jedes bezogen auf seinen
/// Wert im <see cref="Bezug"/> (mindestens die Zahl der Mannschaften) – so beherrscht kein Kriterium seine Gruppe, nur weil
/// es große Zahlen hat (Abstand H/A in der Verbandsoberliga: 94 gegen 14 bei Ungleich H/A).
/// </summary>
/// <param name="Gewichte">Gewicht je Gruppe (Index wie <see cref="Gruppen"/>).</param>
/// <param name="Fokus">Die Gruppe, die strikt vor der Güte zählt, oder <c>null</c>.</param>
/// <param name="Bezug">Verstöße je Kriterium im Bezugsplan (z. B. dem Grundplan), oder <c>null</c> (Bezug: Zahl der Mannschaften).</param>
/// <param name="Faktoren">
/// Wichtigkeit je Kriterium als Faktor auf sein Gewicht (Index wie <see cref="Kostenkriterium"/>; 1 = normal, kleiner =
/// darf steigen, größer = besonders wichtig), oder <c>null</c> (alle 1).
/// </param>
public sealed record Massstab(IReadOnlyList<double> Gewichte, int? Fokus = null, IReadOnlyList<int>? Bezug = null, IReadOnlyList<double>? Faktoren = null)
{
    /// <summary>
    /// Die Gruppen der Güte mit ihrem Standardgewicht nach Auffälligkeit (Peter, 28.10.2026): mehrfach hintereinander zu Hause
    /// fällt mehr auf als eine etwas schlechtere Verteilung. Dazu der Hebel, mit dem der Klassenleiter die Gruppe verbessert.
    /// </summary>
    public static readonly IReadOnlyList<(string Name, IReadOnlyList<Kostenkriterium> Kriterien, double Gewicht, string Hebel)> Gruppen =
    [
        ("Heim/Auswärts", [Kostenkriterium.UngleichHeimAuswaerts, Kostenkriterium.WechselHeimAuswaerts, Kostenkriterium.AbstandHeimAuswaerts], 3, "Heim/Auswärts-Wechsel verbessern"),
        ("Abstände", [Kostenkriterium.DreiTageAbstand, Kostenkriterium.ZweiSpieleProWoche], 2, "Weniger kurze Abstände"),
        ("Spielverteilung", [Kostenkriterium.Spielverteilung], 1, "Spiele gleichmäßiger verteilen"),
        ("Spieltage", [Kostenkriterium.Spieltaglaenge, Kostenkriterium.Spieltagueberlappung, Kostenkriterium.LetzterSpieltagLaenge, Kostenkriterium.LetzterSpieltagUeberlappung], 1, "Spieltage nicht so weit auseinanderfächern"),
        ("60-km-Regel", [Kostenkriterium.SechzigKilometerRegel], 1, "60-km-Regel besser einhalten"),
        ("Setzliste", [Kostenkriterium.Setzliste], 1, "Setzliste besser einhalten"),
        ("Gleiche Heimtermine", [Kostenkriterium.GleicheHeimtermine], 1, "Gleiche Heimtermine besser einhalten"),
    ];

    /// <summary>Gets den Standard: Gewichte nach Auffälligkeit, ohne Fokus, Bezug die Zahl der Mannschaften.</summary>
    public static Massstab Standard { get; } = new([.. Gruppen.Select(g => g.Gewicht)]);

    /// <summary>Die Gruppe eines Kriteriums.</summary>
    /// <param name="k">Das Kriterium.</param>
    /// <returns>Index in <see cref="Gruppen"/>, oder −1 (Pflicht und Wünsche).</returns>
    public static int GruppeVon(Kostenkriterium k)
    {
        for (int g = 0; g < Gruppen.Count; g++)
        {
            if (Gruppen[g].Kriterien.Contains(k))
            {
                return g;
            }
        }

        return -1;
    }

    /// <summary>Der Name eines Kriteriums für Vorschläge und Unterschiede (wie in der Qualitätsansicht).</summary>
    /// <param name="k">Das Kriterium.</param>
    /// <returns>Der Name, z. B. „Wechsel Heim/Auswärts“.</returns>
    public static string Kriteriumsname(Kostenkriterium k) => k switch
    {
        Kostenkriterium.UngleichHeimAuswaerts => "Ungleich Heim/Auswärts",
        Kostenkriterium.WechselHeimAuswaerts => "Wechsel Heim/Auswärts",
        Kostenkriterium.AbstandHeimAuswaerts => "Abstand Heim/Auswärts",
        Kostenkriterium.DreiTageAbstand => "Drei Tage Abstand",
        Kostenkriterium.ZweiSpieleProWoche => "Zwei Spiele pro Woche",
        Kostenkriterium.Spieltaglaenge => "Länge Spieltage",
        Kostenkriterium.Spieltagueberlappung => "Überlappung Spieltage",
        Kostenkriterium.LetzterSpieltagLaenge => "Länge letzter Spieltag",
        Kostenkriterium.LetzterSpieltagUeberlappung => "Überlappung letzter Spieltag",
        Kostenkriterium.SechzigKilometerRegel => "60-km-Regel",
        Kostenkriterium.GleicheHeimtermine => "Gleiche Heimtermine",
        Kostenkriterium.VereinsinterneSpieleAmAnfang => "Vereinsinterne Spiele am Anfang",
        Kostenkriterium.ParalleleSpiele => "Parallele Spiele",
        Kostenkriterium.Auswaertskoppel => "Auswärtskoppel",
        _ => k.ToString(),
    };

    /// <summary>Der Wert einer Gruppe: Mittelwert ihrer Kriterien, je bezogen auf den Bezugsplan (mindestens die Mannschaften).</summary>
    /// <param name="guete">Die Verstöße eines Plans.</param>
    /// <param name="gruppe">Index in <see cref="Gruppen"/>.</param>
    /// <returns>Der Wert (0 = keine Verstöße; 1 = so viele wie im Bezug bzw. einer je Mannschaft).</returns>
    public double Gruppe(Planguete guete, int gruppe)
    {
        ArgumentNullException.ThrowIfNull(guete);
        return Gruppen[gruppe].Kriterien.Average(k => Faktor(k) * guete.Anzahl[(int)k] / Math.Max(Bezug?[(int)k] ?? 0, guete.Mannschaften));
    }

    /// <summary>Der Faktor auf das Gewicht eines Kriteriums (1 = normal).</summary>
    /// <param name="k">Das Kriterium.</param>
    /// <returns>Der Faktor.</returns>
    public double Faktor(Kostenkriterium k) => Faktoren is { } f && (int)k < f.Count ? f[(int)k] : 1;

    /// <summary>Derselbe Maßstab mit anderer Wichtigkeit eines Kriteriums.</summary>
    /// <param name="k">Das Kriterium.</param>
    /// <param name="faktor">Der neue Faktor (≥ 0; 1 = normal).</param>
    /// <returns>Der neue Maßstab.</returns>
    public Massstab MitFaktor(Kostenkriterium k, double faktor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(faktor);
        double[] neu = [.. Enum.GetValues<Kostenkriterium>().Select(Faktor)];
        neu[(int)k] = faktor;
        return this with { Faktoren = neu };
    }

    /// <summary>Die Güte: Summe der gewichteten Gruppen (kleiner ist besser).</summary>
    /// <param name="guete">Die Verstöße eines Plans.</param>
    /// <returns>Die Güte.</returns>
    public double Guete(Planguete guete) => Enumerable.Range(0, Gruppen.Count).Sum(g => Gewichte[g] * Gruppe(guete, g));

    /// <summary>Ob ein Plan nach diesem Maßstab besser ist als ein anderer.</summary>
    /// <param name="x">Der eine.</param>
    /// <param name="y">Der andere.</param>
    /// <returns><c>true</c>, wenn <paramref name="x"/> besser ist.</returns>
    public bool IstBesser(Planguete x, Planguete y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        int vorn = (x.Harte, x.A, x.Wuensche).CompareTo((y.Harte, y.A, y.Wuensche));
        if (vorn != 0)
        {
            return vorn < 0;
        }

        if (Fokus is int f)
        {
            double fx = Gruppe(x, f);
            double fy = Gruppe(y, f);
            if (Math.Abs(fx - fy) > 1e-9)
            {
                return fx < fy;
            }
        }

        return Guete(x) < Guete(y) - 1e-9;
    }

    /// <summary>
    /// Das Gewicht eines Kriteriums im Ziel von HiGHS (Teilneuplanung), passend zur Güte: Gewicht seiner Gruppe, geteilt durch
    /// ihre Größe, mal Mannschaften durch seinen Bezug (mindestens die Mannschaften) – also so, wie ein Verstoß die Güte ändert,
    /// mal die Zahl der Mannschaften (damit die Gewichte um 1 liegen, wie bisher jedes C-Kriterium).
    /// </summary>
    /// <param name="k">Das Kriterium.</param>
    /// <param name="mannschaften">Die Zahl der Mannschaften.</param>
    /// <returns>Das Gewicht (0 für Pflicht und Wünsche, die HiGHS ohnehin festhält).</returns>
    public double Kriteriumsgewicht(Kostenkriterium k, int mannschaften)
    {
        int g = GruppeVon(k);
        if (g < 0)
        {
            return 0;
        }

        int n = Math.Max(1, mannschaften);
        return Faktor(k) * Gewichte[g] / Gruppen[g].Kriterien.Count * n / Math.Max(Bezug?[(int)k] ?? 0, n);
    }

    /// <summary>Derselbe Maßstab mit anderem Gewicht einer Gruppe.</summary>
    /// <param name="gruppe">Index in <see cref="Gruppen"/>.</param>
    /// <param name="gewicht">Das neue Gewicht.</param>
    /// <returns>Der neue Maßstab.</returns>
    public Massstab MitGewicht(int gruppe, double gewicht) =>
        this with { Gewichte = [.. Gewichte.Select((w, i) => i == gruppe ? gewicht : w)] };
}
