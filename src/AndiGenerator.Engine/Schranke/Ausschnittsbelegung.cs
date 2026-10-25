// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Schranke;

/// <summary>
/// Welche Mannschaften gleichzeitige Teilneuplanungen gerade neu planen: Jede wählt möglichst andere Mannschaften, damit sie
/// sich nicht gegenseitig die Ergebnisse entwerten (wird eine Verbesserung bester Plan, ist der Ausgangsplan der anderen
/// überholt).
/// </summary>
public sealed class Ausschnittsbelegung
{
    private readonly Lock sperre = new();
    private readonly Dictionary<int, string[]> belegt = [];

    /// <summary>Die Mannschaften, die die anderen Teilneuplanungen gerade neu planen.</summary>
    /// <param name="platz">Die Nummer der fragenden Teilneuplanung.</param>
    /// <returns>Die Namen der Mannschaften.</returns>
    public IReadOnlySet<string> Andere(int platz)
    {
        lock (sperre)
        {
            return belegt.Where(b => b.Key != platz).SelectMany(b => b.Value).ToHashSet();
        }
    }

    /// <summary>Merkt sich die Mannschaften, die eine Teilneuplanung jetzt neu plant (leer: keine).</summary>
    /// <param name="platz">Die Nummer der Teilneuplanung.</param>
    /// <param name="mannschaften">Die Namen der Mannschaften.</param>
    public void Belegen(int platz, IEnumerable<string> mannschaften)
    {
        lock (sperre)
        {
            belegt[platz] = [.. mannschaften];
        }
    }
}
