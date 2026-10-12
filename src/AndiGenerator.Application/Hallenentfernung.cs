// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Entfernung zur Halle eines Gegners (nicht im Original).</summary>
/// <param name="Gegner">Name des Gegners (Gastgeber).</param>
/// <param name="Luftlinie">Kilometer Luftlinie oder <c>null</c>, wenn eine der Adressen fehlt.</param>
/// <param name="HeimUnterDerWoche">Der Gegner hat Heimtermine unter der Woche gemeldet.</param>
public sealed record Hallenentfernung(string Gegner, double? Luftlinie, bool HeimUnterDerWoche);
