// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Die Spielplandaten einer Mannschaft.</summary>
/// <param name="Mannschaft">Name.</param>
/// <param name="Stammdaten">Nummer, Mannschafts-ID, Vereins-ID.</param>
/// <param name="Spiellokal">Standard-Spiellokal (Nummer) oder leer.</param>
/// <param name="Termine">Heimtermine und Sperrtermine mit Tag, Uhrzeit, Art und Spiellokal.</param>
/// <param name="Heimkoppeln">Wünsche zu zwei Heimspielen an einem Tag bzw. Doppelspieltagen.</param>
/// <param name="Auswaertskoppeln">Auswärtskoppeln: zwei Gegner auf einer Fahrt.</param>
/// <param name="Nachbarmannschaften">Nachbarmannschaften (andere Staffeln desselben Vereins) mit ihren Begegnungen.</param>
/// <param name="Heimrecht">Heimspiele in der Vorrunde und Gegner mit festem Heimrecht.</param>
/// <param name="NurWochenendeGegen">Gegner, bei denen nur am Wochenende gespielt wird (60-km-Regel).</param>
/// <param name="Bemerkung">Bemerkung des Vereins aus den Terminwünschen.</param>
public sealed record Mannschaftstermine(
    string Mannschaft,
    string Stammdaten,
    string Spiellokal,
    IReadOnlyList<string> Termine,
    IReadOnlyList<string> Heimkoppeln,
    IReadOnlyList<string> Auswaertskoppeln,
    IReadOnlyList<string> Nachbarmannschaften,
    string Heimrecht,
    IReadOnlyList<string> NurWochenendeGegen,
    string Bemerkung);
