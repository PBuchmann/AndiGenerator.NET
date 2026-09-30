// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Manuell festgelegte Begegnung (Knoten <c>predefinedgames/game</c>, Original <c>TDialogEditOneGame</c>).</summary>
/// <param name="Index">Position in den Daten (für Bearbeiten und Löschen); bei neuen Spielen ohne Bedeutung.</param>
/// <param name="Termin">Datum und Uhrzeit.</param>
/// <param name="Heim">Heimmannschaft.</param>
/// <param name="Gast">Gastmannschaft.</param>
/// <param name="Spiellokal">Spiellokal (leer: Standardspiellokal der Mannschaft).</param>
public sealed record Vorgabespiel(int Index, DateTime Termin, string Heim, string Gast, string Spiellokal);
