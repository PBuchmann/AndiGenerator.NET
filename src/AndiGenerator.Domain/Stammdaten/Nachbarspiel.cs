// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Stammdaten;

/// <summary>Spiel einer Nachbarmannschaft (Original <c>sistergame</c>).</summary>
/// <param name="Zeitpunkt">Datum und Uhrzeit.</param>
/// <param name="Heim">Name der Heimmannschaft.</param>
/// <param name="Gast">Name der Gastmannschaft.</param>
/// <param name="Spiellokal">Abweichendes Spiellokal „1“ bis „5“ oder leer.</param>
public sealed record Nachbarspiel(DateTime Zeitpunkt, string Heim, string Gast, string Spiellokal);
