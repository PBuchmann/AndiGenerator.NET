// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Benötigte Größe einer Zeichnung.</summary>
/// <param name="Breite">Breite in Zeicheneinheiten.</param>
/// <param name="Hoehe">Höhe in Zeicheneinheiten.</param>
public readonly record struct Zeichengroesse(double Breite, double Hoehe);
