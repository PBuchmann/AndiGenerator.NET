// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Csv;

/// <summary>Stammdaten einer Mannschaft, soweit der CSV-Export sie braucht.</summary>
public sealed record CsvMannschaft(string Name, string VereinsId, string MannschaftsId);
