// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Ein Zeitraum, in dem sich ein Spieltag mit anderen überschneidet (beide Tage eingeschlossen).</summary>
/// <param name="Von">Erster Tag.</param>
/// <param name="Bis">Letzter Tag.</param>
public sealed record Spieltagsabschnitt(DateOnly Von, DateOnly Bis);
