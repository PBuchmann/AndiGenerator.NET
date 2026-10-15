// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Engine.Schranke;

/// <summary>Einstellungen der Teilneuplanung mit HiGHS (siehe <see cref="Ligaschranke.Neuplanen"/>).</summary>
/// <param name="Gesamt">Zeit für alle Schritte zusammen.</param>
/// <param name="JeSchritt">Zeitgrenze für HiGHS je Ausschnitt (mit der Startgröße).</param>
/// <param name="MitWechsel">Wechsel Heim/Auswärts im Modell (im Ausschnitt meist lösbar).</param>
/// <param name="MitAbstand">Abstand Heim/Auswärts genähert im Modell (Abstand zum gespiegelten Plan).</param>
/// <param name="MitSetzliste">
/// Setzliste genähert im Modell (wenn es eine gibt; zusammen mit Abstand Heim/Auswärts am besten, MIGRATIONSPLAN O7).
/// </param>
/// <param name="Startwert">Startwert des Zufalls für die Wahl der Ausschnitte.</param>
/// <param name="Mannschaften">
/// So viele Mannschaften je Ausschnitt zu Beginn; bringt eine Größe eine Weile nichts, eine mehr (bis zur Hälfte der Staffel),
/// mit entsprechend längerer Zeitgrenze.
/// </param>
/// <param name="Bewerten">
/// Bewertet einen Plan (z. B. wie der Automodus, mit C-Ausreißern); <c>null</c> = nach der Einteilung über die Kostenfunktion.
/// </param>
/// <param name="Besser">
/// Vergleicht zwei Bewertungen (z. B. wie der Automodus); <c>null</c> = <see cref="Stufenvergleich"/>.
/// </param>
/// <param name="Obergrenzen">
/// Festlegung des Staffelleiters: je Kriterium höchstens so viele Verstöße (im Modell als Bedingung), oder <c>null</c>.
/// </param>
/// <param name="Ausschnitt">Meldet Beginn und Ausgang jedes Ausschnitts, oder <c>null</c>.</param>
/// <param name="Verbessert">Bekommt jeden angenommenen Plan sofort (z. B. für den Automodus), oder <c>null</c>.</param>
/// <param name="Ausgangsplan">
/// Vor jedem Ausschnitt gefragt (mit dem Wert des eigenen besten Plans): ein besserer Plan von außen, der neuer Ausgangsplan
/// wird, oder <c>null</c> (weiter mit dem eigenen).
/// </param>
/// <param name="Unterbrechen">
/// Beendet den laufenden Lauf von HiGHS vorzeitig (Pause, Ende der Generierung), oder <c>null</c>; abgebrochen wird über das
/// Abbruch-Token.
/// </param>
public sealed record Neuplanungseinstellungen(
    TimeSpan Gesamt,
    TimeSpan JeSchritt,
    bool MitWechsel = true,
    bool MitAbstand = true,
    bool MitSetzliste = true,
    int Startwert = 1,
    int Mannschaften = 2,
    Func<IReadOnlyList<Spiel>, Stufenwert?>? Bewerten = null,
    Func<Stufenwert, Stufenwert, bool>? Besser = null,
    IReadOnlyDictionary<Kostenkriterium, int>? Obergrenzen = null,
    Action<Ausschnittsmeldung>? Ausschnitt = null,
    Action<IReadOnlyList<Spiel>, Stufenwert>? Verbessert = null,
    Func<Stufenwert, IReadOnlyList<Spiel>?>? Ausgangsplan = null,
    Func<bool>? Unterbrechen = null);
