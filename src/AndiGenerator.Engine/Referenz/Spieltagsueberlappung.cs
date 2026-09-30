// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Grau hinterlegter Bereich, in dem sich Spieltage überlappen (Original <c>PaintSpielPlanOverlap</c>).</summary>
/// <param name="Von">Frühester Termin des späteren Spieltags.</param>
/// <param name="Bis">Spätester Termin des früheren Spieltags.</param>
/// <param name="Helligkeit">Grauwert 128 … 255 (je weiter die Spieltage auseinander liegen, desto dunkler).</param>
public sealed record Spieltagsueberlappung(DateTime Von, DateTime Bis, int Helligkeit);
