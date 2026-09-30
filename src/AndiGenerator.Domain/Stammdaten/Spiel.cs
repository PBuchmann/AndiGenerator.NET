// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Stammdaten;

/// <summary>Ein Spiel mit Termin (vorgegebenes Spiel oder bestehender Spielplan, Original <c>game</c>).</summary>
/// <param name="Zeitpunkt">Datum und Uhrzeit; <c>null</c>, wenn das Spiel keinen Termin hat.</param>
/// <param name="Heim">Name der Heimmannschaft.</param>
/// <param name="Gast">Name der Gastmannschaft.</param>
/// <param name="Spiellokal">Spiellokal „1“ bis „5“ oder leer.</param>
public sealed record Spiel(DateTime? Zeitpunkt, string Heim, string Gast, string Spiellokal);
