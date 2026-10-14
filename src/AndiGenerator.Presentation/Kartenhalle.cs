// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Spielort im Kartenbild.</summary>
/// <param name="Ort">Der Spielort.</param>
/// <param name="Punkt">Lage im Bild.</param>
/// <param name="Schrift">Linke obere Ecke der Beschriftung.</param>
public sealed record Kartenhalle(Kartenort Ort, Kartenpunkt Punkt, Kartenpunkt Schrift);
