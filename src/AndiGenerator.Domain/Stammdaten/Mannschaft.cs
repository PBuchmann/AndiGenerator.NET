// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Stammdaten;

/// <summary>Mannschaft der Staffel mit ihren Wünschen (Original <c>team</c>, gelesen von <c>TMannschaft.load</c>).</summary>
/// <param name="Name">Name der Mannschaft (<c>teamname</c>, Schlüssel im Datenbaum).</param>
/// <param name="UrspruenglicherName">Name aus click-TT, wenn die Mannschaft umbenannt wurde (<c>orgteamname</c>), sonst leer.</param>
/// <param name="Id">click-TT-Id der Mannschaft (<c>teamid</c>).</param>
/// <param name="VereinsId">click-TT-Id des Vereins (<c>clubid</c>).</param>
/// <param name="Nummer">Mannschaftsnummer im Verein (<c>teamnumber</c>).</param>
/// <param name="Spiellokal">Standard-Spiellokal „1“ bis „5“ oder leer (<c>location</c>).</param>
/// <param name="HeimspieleHinrunde">Vorgabe für die Zahl der Heimspiele in der Hinrunde, −1 = automatisch (<c>homerights</c>).</param>
/// <param name="Heimspieltermine">Heimspielwünsche (<c>homegameday</c>).</param>
/// <param name="Sperrtermine">Tage ohne Spiel dieser Mannschaft (<c>nogameday</c>).</param>
/// <param name="Auswaertskoppeln">Auswärtskoppelwünsche (<c>roadcouple</c>).</param>
/// <param name="Heimrechte">Heimrecht-Vorgaben gegen einzelne Gegner (<c>homerights</c>-Knoten).</param>
/// <param name="KeinWochenspielGegen">60-km-Regel: Gegner, gegen die nicht unter der Woche gespielt wird (<c>noweekgames</c>).</param>
/// <param name="Nachbarmannschaften">Mannschaften desselben Vereins in anderen Staffeln (<c>sisterteam</c>).</param>
public sealed record Mannschaft(
    string Name,
    string UrspruenglicherName,
    string Id,
    string VereinsId,
    int Nummer,
    string Spiellokal,
    int HeimspieleHinrunde,
    IReadOnlyList<Heimspieltermin> Heimspieltermine,
    IReadOnlyList<DateOnly> Sperrtermine,
    IReadOnlyList<Auswaertskoppel> Auswaertskoppeln,
    IReadOnlyList<Heimrechtvorgabe> Heimrechte,
    IReadOnlyList<string> KeinWochenspielGegen,
    IReadOnlyList<Nachbarmannschaft> Nachbarmannschaften);
