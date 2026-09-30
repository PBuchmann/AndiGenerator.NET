// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.ClickTt;

/// <summary>Ein Spiel (<c>game</c>) mit Datum, Uhrzeit, Heim- und Gastmannschaft.</summary>
public sealed record ClickTtSpiel(DateTime Zeitpunkt, string Heim, string Gast, string Halle);
