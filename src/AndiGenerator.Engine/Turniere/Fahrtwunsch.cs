// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Fahrtwunsch für die Kompaktspieltage (nicht im Original): Die Gastmannschaft spielt gegen den Gastgeber in dessen
/// Halle, z. B. „Sinzig spielt dieses Jahr in Trier“.
/// </summary>
/// <param name="Gast">Die anreisende Mannschaft.</param>
/// <param name="Gastgeber">Die ausrichtende Mannschaft.</param>
public sealed record Fahrtwunsch(string Gast, string Gastgeber);
