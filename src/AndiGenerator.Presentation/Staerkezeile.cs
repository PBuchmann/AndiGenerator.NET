// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eine Zeile des Setzlisten-Vorschlags aus den Mannschaftsaufstellungen.</summary>
/// <param name="PlatzText">Platz, z. B. „3.“.</param>
/// <param name="Mannschaft">Name der Mannschaft.</param>
/// <param name="SchnittText">Schnitt der gewerteten Stammspieler, z. B. „Ø 1983“, oder „keine Aufstellung“.</param>
/// <param name="SpielerText">Zahl der Stammspieler, z. B. „5 Stammspieler“.</param>
public sealed record Staerkezeile(string PlatzText, string Mannschaft, string SchnittText, string SpielerText);
