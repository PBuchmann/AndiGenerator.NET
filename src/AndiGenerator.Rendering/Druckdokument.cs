// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Ein Ausdruck: Kopfangaben für jede Seite und die Abschnitte.</summary>
/// <param name="Staffel">Name der Staffel.</param>
/// <param name="Plan">Welcher Plan gedruckt wurde, z. B. <c>In click-TT vorhandener Plan</c>.</param>
/// <param name="Erstellt">Zeitpunkt des Ausdrucks.</param>
/// <param name="Abschnitte">Die Abschnitte.</param>
public sealed record Druckdokument(string Staffel, string Plan, DateTime Erstellt, IReadOnlyList<Abschnitt> Abschnitte);
