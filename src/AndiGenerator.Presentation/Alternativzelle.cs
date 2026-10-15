// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Wert im Dialog der Alternativen: Verstöße eines Kriteriums in einer Spalte, verglichen mit dem aktuellen Plan.</summary>
/// <param name="Text">Z. B. <c>6</c> oder <c>6 (−7)</c>.</param>
/// <param name="Richtung">−1 besser als der aktuelle Plan, +1 schlechter, 0 gleich.</param>
/// <param name="Spalte">Die Spalte (für die Markierung der Auswahl).</param>
public sealed record Alternativzelle(string Text, int Richtung, Alternativspalte Spalte);
