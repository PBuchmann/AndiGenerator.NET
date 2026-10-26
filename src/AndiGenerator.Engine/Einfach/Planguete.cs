// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Einfach;

/// <summary>
/// Die Verstöße eines Plans für den einfachen Modus (Peter, 28.10.2026): harte Fehler, Pflicht (A), Wünsche der Vereine
/// (Heim-/Auswärtskoppel, Sperr- und Ausweichtermine – sie werden nie für Rhythmus oder Güte geopfert) und je Kriterium die
/// Anzahl. Verglichen wird nach einem <see cref="Massstab"/> (Gruppen, Gewichte, Fokus); <see cref="Guete"/> ist die Güte nach
/// dem Standard.
/// </summary>
/// <param name="Harte">Harte Fehler.</param>
/// <param name="A">Verstöße der Pflichtkriterien.</param>
/// <param name="Wuensche">Verstöße gegen die Wünsche der Vereine.</param>
/// <param name="Guete">Die Güte nach <see cref="Massstab.Standard"/> (kleiner ist besser, 0 = keine Verstöße).</param>
/// <param name="Anzahl">Verstöße je Kriterium (Index = <see cref="Kostenkriterium"/>).</param>
/// <param name="Mannschaften">Die Zahl der Mannschaften.</param>
public sealed record Planguete(int Harte, int A, int Wuensche, double Guete, IReadOnlyList<int> Anzahl, int Mannschaften)
{
    /// <summary>Die Pflichtkriterien (Stufe A des Standards).</summary>
    public static readonly IReadOnlyList<Kostenkriterium> Pflicht =
        [Kostenkriterium.VereinsinterneSpieleAmAnfang, Kostenkriterium.Hallenbelegung, Kostenkriterium.ParalleleSpiele, Kostenkriterium.Pflichtspieltage];

    /// <summary>Die Wünsche der Vereine (Stufe B des Standards).</summary>
    public static readonly IReadOnlyList<Kostenkriterium> Wunschkriterien =
        [Kostenkriterium.Auswaertskoppel, Kostenkriterium.Heimkoppel, Kostenkriterium.Sperrtermine, Kostenkriterium.Ausweichtermine];

    /// <summary>Bewertet einen Plan.</summary>
    /// <param name="bewertung">Die Bewertung des Plans.</param>
    /// <returns>Seine Verstöße.</returns>
    public static Planguete Bewerten(Planbewertung bewertung)
    {
        ArgumentNullException.ThrowIfNull(bewertung);
        (double[] anzahl, _) = Stufeneinteilung.Anzahlen(bewertung);
        int[] je = [.. anzahl.Select(a => (int)Math.Round(a))];
        int harte = (int)Math.Round((bewertung.NichtTerminiert + bewertung.UngueltigeSpiele + bewertung.SpieleAnSpielfreienTagen) / (10.0 * RefPlan.MaxKostenOhneHartenFehler));
        var ohneGuete = new Planguete(harte, Pflicht.Sum(k => je[(int)k]), Wunschkriterien.Sum(k => je[(int)k]), 0, je, Math.Max(1, bewertung.Mannschaften.Count));
        return ohneGuete with { Guete = Massstab.Standard.Guete(ohneGuete) };
    }

    /// <summary>Ob dieser Plan nach dem Standard-Maßstab besser ist als ein anderer.</summary>
    /// <param name="andere">Der andere.</param>
    /// <returns><c>true</c>, wenn besser.</returns>
    public bool IstBesserAls(Planguete andere) => Massstab.Standard.IstBesser(this, andere);

    /// <summary>Der Wert einer Gruppe nach dem Standard-Maßstab.</summary>
    /// <param name="gruppe">Index in <see cref="Massstab.Gruppen"/>.</param>
    /// <returns>Mittelwert ihrer Verstöße je Mannschaft.</returns>
    public double Gruppe(int gruppe) => Massstab.Standard.Gruppe(this, gruppe);

    /// <inheritdoc/>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"Pflicht {Harte}·{A} · Wünsche {Wuensche} · Güte {Guete:0.00}");
}
