// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Zelle einer Tabelle.</summary>
/// <param name="Text">Der Text.</param>
/// <param name="Hintergrund">Hinterlegung oder <c>null</c>.</param>
/// <param name="Farbe">Abweichende Schriftfarbe oder <c>null</c> (Farbe der Zeile).</param>
public sealed record Tabellenzelle(string Text, Farbe? Hintergrund = null, Farbe? Farbe = null);
