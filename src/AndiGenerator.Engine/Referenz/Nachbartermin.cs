// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Spiel einer Nachbar- oder Vereinsmannschaft am selben Tag (Original <c>PaintOneTerminSisterGames</c>).</summary>
/// <param name="Zeitpunkt">Termin.</param>
/// <param name="Geschlecht">Altersklasse/Geschlecht der Staffel des Spiels, z. B. <c>Herren</c>.</param>
/// <param name="Heim">Heimmannschaft.</param>
/// <param name="Gast">Gastmannschaft.</param>
/// <param name="Spiellokal">Spiellokal oder leer.</param>
/// <param name="EchteNachbarmannschaft">Echte Nachbarmannschaft (parallele Spiele vermeiden bzw. Nummer ±1), im Original blau.</param>
public sealed record Nachbartermin(DateTime Zeitpunkt, string Geschlecht, string Heim, string Gast, string Spiellokal, bool EchteNachbarmannschaft);
