// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Zeile der Qualitätsansicht.</summary>
/// <param name="Stufe">Stufe nach E14, z. B. <c>A2</c>.</param>
/// <param name="Name">Kriterium.</param>
/// <param name="Verstoesse">Verstöße, z. B. <c>3 von 15</c> oder <c>–</c>, wenn nur Kosten bekannt sind.</param>
/// <param name="Kosten">Kosten.</param>
/// <param name="Zustand">Zustand für die Farbe.</param>
public sealed record Qualitaetszeile(string Stufe, string Name, string Verstoesse, string Kosten, Qualitaetszustand Zustand);
