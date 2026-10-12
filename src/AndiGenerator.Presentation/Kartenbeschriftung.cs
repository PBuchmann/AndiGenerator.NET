// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Text im Kartenbild (Stadt oder Bundesland).</summary>
/// <param name="Text">Der Text.</param>
/// <param name="Punkt">Bei Städten der Ort (Marke), bei Ländern die Mitte des Textes.</param>
/// <param name="Schrift">Linke obere Ecke des Textes.</param>
public sealed record Kartenbeschriftung(string Text, Kartenpunkt Punkt, Kartenpunkt Schrift);
