// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Auswärtskoppelwunsch einer Mannschaft (Knoten <c>roadcouple</c>, Original <c>TDialogEditOneAuswaertsKoppel</c>).</summary>
/// <param name="MannschaftA">Mannschaft 1.</param>
/// <param name="MannschaftB">Mannschaft 2.</param>
/// <param name="Art">0 = am gleichen Tag, 1 = mit Übernachtung, 2 = am gleichen Tag oder mit Übernachtung.</param>
public sealed record Auswaertskoppeldaten(string MannschaftA, string MannschaftB, int Art)
{
    /// <summary>Holt die Arten in der Reihenfolge des Originals.</summary>
    public static IReadOnlyList<string> Arten { get; } = ["am gleichen Tag", "mit Übernachtung", "am gleichen Tag oder mit Übernachtung"];

    /// <summary>Holt die Bezeichnung wie im Original (<c>KoppelNodeToString</c>), z. B. <c>A, B (am gleichen Tag)</c>.</summary>
    public string Bezeichnung => $"{MannschaftA}, {MannschaftB} ({(Art >= 0 && Art < Arten.Count ? Arten[Art] : string.Empty)})";
}
