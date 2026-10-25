// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Die Qualitätsansicht.</summary>
/// <param name="Plan">Der bewertete Plan.</param>
/// <param name="Zusammenfassung">Zusammenfassung, z. B. „Pflichtstufe A erfüllt · …“.</param>
/// <param name="Stufen">Kopf je Stufe (Automodus): Verstöße, Grenze, Optimum.</param>
/// <param name="Kriterien">Die Kriterien.</param>
/// <param name="Hinweis">Hinweis der Ansicht, z. B. „Noch kein Plan – bitte die Generierung starten.“.</param>
public sealed record Qualitaet(string Plan, string Zusammenfassung, IReadOnlyList<string> Stufen, IReadOnlyList<Qualitaetseintrag> Kriterien, string Hinweis);
