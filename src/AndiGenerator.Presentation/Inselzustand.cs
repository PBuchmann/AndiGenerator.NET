// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Was eine Insel im Schaubild grob zeigt.</summary>
/// <param name="Wert">Wert ihres besten Plans (Kosten bzw. Verstöße A · B · C).</param>
/// <param name="Taetigkeit">Was sie gerade grob tut, z. B. „kommt voran“ oder „steckt fest“.</param>
/// <param name="Stillstand">Weg bis zum Neustart (0 = hat gerade verbessert, 1 = beginnt bald neu).</param>
public sealed record Inselzustand(string Wert, string Taetigkeit, double Stillstand);
