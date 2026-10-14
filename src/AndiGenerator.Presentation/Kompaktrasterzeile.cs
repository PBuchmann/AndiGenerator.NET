// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Zeile im Raster der Kompaktspieltage: ein Sonntag.</summary>
/// <param name="Tag">Datum.</param>
/// <param name="Datum">Beschriftung, z. B. „06.09.“.</param>
/// <param name="Gewaehlt">Sonntag ist ausgewählt.</param>
/// <param name="Zellen">Je Mannschaft eine Zelle.</param>
public sealed record Kompaktrasterzeile(DateOnly Tag, string Datum, bool Gewaehlt, IReadOnlyList<Kompaktzelle> Zellen);
