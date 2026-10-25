// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Eine Zeile der Qualitätsansicht.</summary>
/// <param name="Stufe">Stufe, z. B. „B2“.</param>
/// <param name="Kriterium">Name des Kriteriums.</param>
/// <param name="Verstoesse">Verstöße des Plans.</param>
/// <param name="Bestmoeglich">So wenige hat jeder Plan mindestens (Automodus, HiGHS), sonst leer.</param>
/// <param name="BestmoeglichErreicht">Der Plan erreicht diesen Wert.</param>
/// <param name="Festgelegt">Obergrenze aus einer übernommenen B-Variante, sonst leer.</param>
/// <param name="NurC">Grund, warum das Kriterium nur in C stehen kann, sonst leer.</param>
public sealed record Qualitaetseintrag(string Stufe, string Kriterium, string Verstoesse, string Bestmoeglich, bool BestmoeglichErreicht, string Festgelegt, string NurC);
