// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Presentation;

/// <summary>Bearbeitung einer Zeile der Qualitätsansicht.</summary>
/// <param name="Einstufen">Ordnet das Kriterium einer anderen Stufe zu.</param>
/// <param name="Hoch">Verschiebt nach oben; <c>null</c> = schon oben in der Stufe.</param>
/// <param name="Runter">Verschiebt nach unten; <c>null</c> = schon unten in der Stufe.</param>
internal sealed record Zeilenbearbeitung(Action<Kostenkriterium, Stufe> Einstufen, Action? Hoch, Action? Runter);
