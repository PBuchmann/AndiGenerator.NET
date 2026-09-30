// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Spiel einer Nachbarmannschaft (Knoten <c>sistergame</c>, Original <c>TDialogEditOneSisterGame</c>).</summary>
/// <param name="Termin">Datum und Uhrzeit.</param>
/// <param name="Heim">Heimmannschaft.</param>
/// <param name="Gast">Gastmannschaft.</param>
/// <param name="Spiellokal">Spiellokal (leer: Standardspiellokal der Mannschaft).</param>
public sealed record Nachbarspieldaten(DateTime Termin, string Heim, string Gast, string Spiellokal);
