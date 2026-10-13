// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Heimtermin einer Mannschaft an einem Tag im Raster der Kompaktspieltage (nicht im Original).</summary>
/// <param name="Zeit">Beginn, oder <c>null</c> = Standard (3er-Spieltag 10:00, 4er-Spieltag 11:00).</param>
/// <param name="Spiellokal">Nummer des Spiellokals (leer = Standardlokal der Mannschaft).</param>
/// <param name="ZweiTische">Zwei Spiele parallel möglich (4er-Spieltag).</param>
public sealed record Kompakttermin(TimeOnly? Zeit, string Spiellokal, bool ZweiTische);
