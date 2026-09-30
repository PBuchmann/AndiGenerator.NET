// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Textzeile im Tab „Terminwünsche“ des Originals.</summary>
/// <param name="Text">Der Text.</param>
/// <param name="Farbe">Hervorhebung.</param>
/// <param name="Ueberschrift">Zwischenüberschrift (z. B. „Sperrtermine (3 Stück):“) statt Eintrag.</param>
public sealed record Terminwunschtext(string Text, Terminwunschfarbe Farbe, bool Ueberschrift);
