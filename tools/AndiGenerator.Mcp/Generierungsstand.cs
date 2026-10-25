// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Die Kacheln der Generierung.</summary>
/// <param name="Titel">Phase bzw. Verfahren, z. B. „Optimierung Stufe C“.</param>
/// <param name="Kosten">Gesamtkosten des besten Plans.</param>
/// <param name="VerstoesseA">Verstöße Stufe A (Automodus).</param>
/// <param name="VerstoesseB">Verstöße Stufe B (Automodus).</param>
/// <param name="VerstoesseC">Verstöße Stufe C (Automodus).</param>
/// <param name="InArbeit">Kriterien, an denen der Automodus arbeitet.</param>
/// <param name="Verbesserungen">Zahl der Verbesserungen.</param>
/// <param name="LetzteVerbesserung">Wann zuletzt.</param>
/// <param name="BerechnetePlaene">Zahl der berechneten Pläne.</param>
/// <param name="Highs">Woran HiGHS rechnet (Automodus), sonst leer.</param>
/// <param name="BVarianten">Zahl der B-Varianten, die zur Wahl stehen.</param>
public sealed record Generierungsstand(
    string Titel,
    string Kosten,
    string VerstoesseA,
    string VerstoesseB,
    string VerstoesseC,
    string InArbeit,
    string Verbesserungen,
    string LetzteVerbesserung,
    string BerechnetePlaene,
    string Highs,
    int BVarianten);
