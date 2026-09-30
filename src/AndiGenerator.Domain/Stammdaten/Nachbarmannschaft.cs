// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Stammdaten;

/// <summary>Mannschaft desselben Vereins in einer anderen Staffel (Original <c>sisterteam</c>, gelesen von <c>TMannschaft.loadSisterTeam</c>).</summary>
/// <param name="Name">Name der Nachbarmannschaft (<c>teamname</c>).</param>
/// <param name="Geschlecht">Altersklasse/Geschlecht (<c>gender</c>).</param>
/// <param name="Nummer">Mannschaftsnummer im Verein (<c>teamnumber</c>).</param>
/// <param name="Spiellokal">Standard-Spiellokal ihrer Heimspiele „1“ bis „5“ oder leer (<c>location</c>).</param>
/// <param name="KeineParallelenSpiele">Spiele beider Mannschaften sollen nicht gleichzeitig stattfinden (<c>noparallelgames</c>).</param>
/// <param name="ParalleleHeimspiele">Heimspiele beider Mannschaften sollen gleichzeitig stattfinden (<c>parallelhomegames</c>).</param>
/// <param name="Spiele">Spiele der Nachbarmannschaft (<c>sistergame</c>).</param>
public sealed record Nachbarmannschaft(
    string Name,
    string Geschlecht,
    int Nummer,
    string Spiellokal,
    bool KeineParallelenSpiele,
    bool ParalleleHeimspiele,
    IReadOnlyList<Nachbarspiel> Spiele);
