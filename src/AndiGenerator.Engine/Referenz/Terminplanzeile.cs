// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Ein Spiel im Terminplan mit den Hinweisen des Originals (<c>PaintOneTermin</c>).</summary>
/// <param name="Zeitpunkt">Termin; <c>null</c> für nicht terminierte Spiele.</param>
/// <param name="Heim">Heimmannschaft.</param>
/// <param name="Gast">Gastmannschaft.</param>
/// <param name="Runde">Index der Runde (Original <c>GetRoundNumberByDate</c>), −1 ohne Termin.</param>
/// <param name="Kalenderwoche">Kalenderwoche nach ISO 8601, 0 ohne Termin.</param>
/// <param name="Hinweis">Hinweise wie „Ausweichtermin“, „kein Wunschtermin“, „ungültiger Termin“, kommagetrennt.</param>
/// <param name="NachbartermineHeim">Spiele der Nachbar-/Vereinsmannschaften der Heimmannschaft am selben Tag.</param>
/// <param name="NachbartermineGast">Spiele der Nachbar-/Vereinsmannschaften der Gastmannschaft am selben Tag.</param>
public sealed record Terminplanzeile(
    DateTime? Zeitpunkt,
    string Heim,
    string Gast,
    int Runde,
    int Kalenderwoche,
    string Hinweis,
    IReadOnlyList<Nachbartermin> NachbartermineHeim,
    IReadOnlyList<Nachbartermin> NachbartermineGast);
