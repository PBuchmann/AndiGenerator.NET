// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Spaltenkopf im Raster der Kompaktspieltage: eine Mannschaft.</summary>
/// <param name="Name">Name der Mannschaft.</param>
/// <param name="Kurzname">Beschriftung.</param>
/// <param name="Gewaehlt">Mannschaft ist ausgewählt.</param>
public sealed record Kompaktspalte(string Name, string Kurzname, bool Gewaehlt);
