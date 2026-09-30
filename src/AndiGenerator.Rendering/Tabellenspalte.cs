// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Lage einer Tabellenspalte.</summary>
/// <param name="X">Linke Kante relativ zum Tabellenanfang.</param>
/// <param name="Breite">Breite einschließlich Abstand zur nächsten Spalte.</param>
public sealed record Tabellenspalte(double X, double Breite);
