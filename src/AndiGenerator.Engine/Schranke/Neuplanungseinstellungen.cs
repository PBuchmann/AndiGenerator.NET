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
/// <param name="MitAbstand">Abstand Heim/Auswärts im Modell (genähert oder genau, siehe <paramref name="AbstandGenau"/>).</param>
/// <param name="MitSetzliste">
/// Setzliste im Modell, wenn es eine gibt (genau oder genähert, siehe <paramref name="SetzlisteGenau"/>; zusammen mit Abstand
/// Heim/Auswärts am besten, MIGRATIONSPLAN O7).
/// </param>
/// <param name="Startwert">Startwert des Zufalls für die Wahl der Ausschnitte.</param>
/// <param name="Mannschaften">
/// So viele Mannschaften je Ausschnitt mindestens (mit <paramref name="JeSchritt"/> als Zeitgrenze, größere entsprechend
/// länger). Gezielt werden Art und Größe gewählt, die zuletzt je Sekunde die meisten C-Verstöße eingespart haben (bis zur
/// Hälfte der Staffel); sonst wird der Ausschnitt eine Mannschaft größer, wenn eine Größe eine Weile nichts bringt.
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
/// <param name="Verstoesse">
/// Je Mannschaft (Name) die Verstöße in C eines Plans – für gezielte Ausschnitte mit den Mannschaften, die die meisten
/// haben; oder <c>null</c> (dann nur zufällige Ausschnitte und Zeitfenster).
/// </param>
/// <param name="Gezielt">
/// Neben zufälligen Mannschaften auch Zeitfenster und (mit <paramref name="Verstoesse"/>) die Mannschaften mit den meisten
/// C-Verstößen als Ausschnitt; <c>false</c> = nur zufällige Mannschaften (wie früher, zum Vergleich).
/// </param>
/// <param name="Belegung">
/// Gemeinsam für mehrere gleichzeitige Teilneuplanungen: Welche Mannschaften die anderen gerade neu planen – sie werden
/// nicht gewählt (solange genug andere übrig sind); oder <c>null</c>.
/// </param>
/// <param name="Platz">Die Nummer dieser Teilneuplanung in der <paramref name="Belegung"/>.</param>
/// <param name="MitSpieltagen">Zu lange und überlappende Spieltage im Modell (genau, solange alle Spiele einen Termin haben).</param>
/// <param name="SetzlisteGenau">
/// Die Setzliste genau nach der Reihenfolge der eigenen Spiele (die Ausschnitte werden für HiGHS schwerer – zum Vergleich);
/// <c>false</c> = genähert (nach der Lage des Termins in der Runde).
/// </param>
/// <param name="AbstandGenau">
/// Abstand Heim/Auswärts genau nach der Reihenfolge der eigenen Spiele (die Ausschnitte werden für HiGHS viel schwerer – zum
/// Vergleich); <c>false</c> = genähert (Abstand zum gespiegelten Plan).
/// </param>
/// <param name="Unterbrechen">
/// Beendet den laufenden Lauf von HiGHS vorzeitig (Pause, Ende der Generierung), oder <c>null</c>; abgebrochen wird über das
/// Abbruch-Token.
/// </param>
/// <param name="Abgelehnt">
/// Bekommt jeden Plan, den HiGHS im Modell für besser hielt, die Bewertung aber nicht (A und B gleich gut, anders verteilt) –
/// z. B. als Startplan einer Insel –, oder <c>null</c>.
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
    Func<bool>? Unterbrechen = null,
    Func<IReadOnlyList<Spiel>, IReadOnlyDictionary<string, int>>? Verstoesse = null,
    bool Gezielt = true,
    Ausschnittsbelegung? Belegung = null,
    int Platz = 0,
    bool MitSpieltagen = true,
    bool SetzlisteGenau = false,
    bool AbstandGenau = false,
    Action<IReadOnlyList<Spiel>>? Abgelehnt = null);
