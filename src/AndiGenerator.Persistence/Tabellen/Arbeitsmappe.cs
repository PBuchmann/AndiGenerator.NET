// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Tabellen;

/// <summary>Arbeitsmappe (Excel-Datei) aus mehreren Blättern.</summary>
/// <param name="Blaetter">Die Blätter in ihrer Reihenfolge.</param>
public sealed record Arbeitsmappe(IReadOnlyList<Arbeitsblatt> Blaetter);
