// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Monat im Kalender der spielfreien Tage.</summary>
/// <param name="Titel">Monat und Jahr, z. B. „September 2026“.</param>
/// <param name="Tage">Die Felder ab Montag der ersten Woche: leere Felder vor dem Ersten, dann jeder Tag des Monats.</param>
public sealed record Kalendermonat(string Titel, IReadOnlyList<Kalendertag> Tage);
