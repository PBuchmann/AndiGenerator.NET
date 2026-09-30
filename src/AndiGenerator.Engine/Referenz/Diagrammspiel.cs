// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Spiel einer Mannschaft in den Diagrammen (Reihenfolge wie <c>gamesRef</c>, nach Termin sortiert).</summary>
/// <param name="Zeitpunkt">Termin oder <c>null</c>, wenn nicht terminiert.</param>
/// <param name="Heimspiel">Heimspiel der Mannschaft.</param>
/// <param name="Koppel">Gekoppelt mit dem Spiel davor oder danach (Original <c>getKoppeledGame</c>).</param>
/// <param name="Runde">Index der Runde (Original <c>GetRoundNumberByDate</c>), -1 ohne Termin.</param>
/// <param name="Setzlistenabstand">Abstand der Setzlistenplätze zum Gegner (Original <c>getRankingDiffs</c>), 0 ohne Setzliste.</param>
public sealed record Diagrammspiel(DateTime? Zeitpunkt, bool Heimspiel, bool Koppel, int Runde, int Setzlistenabstand);
