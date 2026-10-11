// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Turniere;

/// <summary>Ein Turniertag: Mannschaften, die sich an einem Tag in einer Halle treffen, und ihre Spiele.</summary>
/// <param name="Tag">Der Tag.</param>
/// <param name="Form">Form des Turniers.</param>
/// <param name="Mannschaften">Beteiligte Mannschaften.</param>
/// <param name="Spiele">Spiele nach Uhrzeit.</param>
/// <param name="MoeglicheAusrichter">
/// Mannschaften, die nach dem Heimrecht der Spiele Ausrichter sein können: beim Dreier die mit zwei Heimspielen, beim
/// Vierer die beiden mit je zwei Heimspielen (eine davon spielt in Wahrheit auswärts), bei der Einzelbegegnung die
/// Heimmannschaft.
/// </param>
public sealed record Turnier(
    DateOnly Tag,
    Turnierform Form,
    IReadOnlyList<string> Mannschaften,
    IReadOnlyList<Spiel> Spiele,
    IReadOnlyList<string> MoeglicheAusrichter);
