// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Zeile der Prüfung „Wochenspiele“ (60-km-Regel, nicht im Original).</summary>
/// <param name="Gast">Mannschaft, die anreist.</param>
/// <param name="Heim">Gastgeber.</param>
/// <param name="Entfernung">Luftlinie als Text, z. B. „72 km“.</param>
public sealed record Wochenspielzeile(string Gast, string Heim, string Entfernung);
