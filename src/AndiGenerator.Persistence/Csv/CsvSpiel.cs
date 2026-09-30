// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Csv;

/// <summary>Ein Spiel für den CSV-Export. <see cref="Zeitpunkt"/> ist <c>null</c> bei nicht terminierten Spielen.</summary>
public sealed record CsvSpiel(DateTime? Zeitpunkt, string Heim, string Gast, string Spiellokal);
