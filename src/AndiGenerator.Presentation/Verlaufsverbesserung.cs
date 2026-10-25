// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eine Verbesserung des besten Plans im Verlauf: wann, woher und was sich geändert hat.</summary>
/// <param name="Sekunden">Rechenzeit seit dem Start.</param>
/// <param name="Nummer">Nummer der Verbesserung (wie in der Kachel „Verbesserungen“).</param>
/// <param name="Herkunft">„Inseln“, „HiGHS“ oder „Teilneuplanung“.</param>
/// <param name="A">Verstöße der Stufe A danach (Automodus), sonst <c>null</c>.</param>
/// <param name="B">Ebenso Stufe B.</param>
/// <param name="C">Ebenso Stufe C.</param>
/// <param name="Kosten">Kosten des besten Plans danach.</param>
/// <param name="Aenderungen">Was sich geändert hat (höchstens vier Kriterien, wichtigste zuerst).</param>
public sealed record Verlaufsverbesserung(double Sekunden, int Nummer, string Herkunft, int? A, int? B, int? C, double Kosten, IReadOnlyList<Planaenderung> Aenderungen);
