// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Vorschlag für die Setzliste aus den Mannschaftsaufstellungen.</summary>
/// <param name="Reihenfolge">Alle Mannschaften der Staffel, die stärkste zuerst; ohne Aufstellung am Ende.</param>
/// <param name="GewerteteSpieler">Zahl der Stammspieler, deren Schnitt zählt (4 oder 6).</param>
public sealed record Setzlistenvorschlag(IReadOnlyList<Mannschaftsstaerke> Reihenfolge, int GewerteteSpieler)
{
    /// <summary>Holt die Mannschaften der Staffel, für die keine Aufstellung gefunden wurde.</summary>
    public IReadOnlyList<string> OhneAufstellung { get; } = Reihenfolge.Where(m => !m.HatAufstellung).Select(m => m.Mannschaft).ToList();
}
