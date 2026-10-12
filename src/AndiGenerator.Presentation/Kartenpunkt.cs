// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Punkt auf der Karte in Bildpunkten des Kartenbilds (Ursprung oben links).</summary>
/// <param name="X">Abstand vom linken Rand.</param>
/// <param name="Y">Abstand vom oberen Rand.</param>
public readonly record struct Kartenpunkt(double X, double Y);
