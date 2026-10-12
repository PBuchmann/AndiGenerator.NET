// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Ein Hinweis zur Liste „keine Auswärtsspiele unter der Woche“ (60-km-Regel) für ein Paar.</summary>
/// <param name="Gast">Mannschaft, die anreist.</param>
/// <param name="Heim">Gastgeber (hat Heimtermine unter der Woche).</param>
/// <param name="Luftlinie">Entfernung der Hallen in Luftlinie (km).</param>
/// <param name="Art">Art des Hinweises.</param>
public sealed record Wochenspielbefund(string Gast, string Heim, double Luftlinie, Wochenspielhinweis Art);
