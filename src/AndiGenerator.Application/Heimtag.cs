// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>
/// Heimspieltermin oder Sperrtermin einer Mannschaft, wie ihn die Datenseiten bearbeiten (Original <c>THomeDay</c>).
/// </summary>
/// <param name="Datum">Termin (bei Sperrterminen nur das Datum).</param>
/// <param name="Sperrtermin">Sperrtermin statt Heimspieltermin.</param>
/// <param name="Koppeltermin">Koppeltermin (zwei Heimspiele am selben Tag).</param>
/// <param name="Doppeltermin">Doppelspieltag.</param>
/// <param name="KoppelPrio">Priorität bei Koppel- und Doppelterminen.</param>
/// <param name="AuswaertsKoppelZweitzeit">Zweite Uhrzeit für eine Auswärtskoppel.</param>
/// <param name="KoppelZweitzeit">Zweite Uhrzeit.</param>
/// <param name="MaxParallel">Höchstzahl paralleler Spiele.</param>
/// <param name="Ausweichtermin">Ausweichtermin.</param>
/// <param name="Spiellokal">Spiellokal.</param>
public sealed record Heimtag(
    DateTime Datum,
    bool Sperrtermin,
    bool Koppeltermin,
    bool Doppeltermin,
    int KoppelPrio,
    bool AuswaertsKoppelZweitzeit,
    TimeOnly KoppelZweitzeit,
    int MaxParallel,
    bool Ausweichtermin,
    string Spiellokal)
{
    /// <summary>Holt einen Wert, der angibt, ob es ein Heimspieltermin ist (Original <c>IsHomeDay</c>).</summary>
    public bool IstHeimtag => !Sperrtermin;
}
