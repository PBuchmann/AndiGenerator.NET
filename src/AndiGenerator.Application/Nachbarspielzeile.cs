// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Zeile der Begegnungen einer Nachbarmannschaft (Original <c>DataToGrid</c>).</summary>
/// <param name="Index">Position des Spiels in den Daten (für Bearbeiten und Löschen).</param>
/// <param name="Spiel">Spiel mit den angezeigten Namen und dem angezeigten Spiellokal.</param>
public sealed record Nachbarspielzeile(int Index, Nachbarspieldaten Spiel);
