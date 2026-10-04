// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Stammdaten;

/// <summary>
/// Stammdaten einer Staffel: alles, was der Benutzer in den Dialogen „Spielplandaten“ pflegt (Original: Datenbaum
/// <c>TPlanData</c>, gelesen von <c>TPlan.load</c>). Abgeleitete Laufzeitdaten wie erzeugte Spiele, Koppel-Zweittermine
/// oder Vereinsgeschwister entstehen erst in der Engine.
/// </summary>
/// <param name="Name">Name der Staffel (<c>name</c>).</param>
/// <param name="Id">click-TT-Id der Staffel (<c>id</c>).</param>
/// <param name="Geschlecht">Altersklasse/Geschlecht, z. B. <c>Herren</c> (<c>gender</c>).</param>
/// <param name="Beginn">Erster Tag der Staffel (<c>from</c>); <c>null</c>, wenn nicht angegeben.</param>
/// <param name="Ende">Letzter Tag der Staffel (<c>until</c>); <c>null</c>, wenn nicht angegeben.</param>
/// <param name="Rueckrundenbeginn">Beginn der Rückrunde (<c>mid</c>); <c>null</c>, wenn nicht angegeben.</param>
/// <param name="Mannschaften">Mannschaften in Dateireihenfolge.</param>
/// <param name="VorgegebeneSpiele">Fest vorgegebene Spiele (<c>predefinedgames</c>).</param>
/// <param name="SpielfreieTage">Tage, an denen in der ganzen Staffel nicht gespielt wird (<c>nogameday</c> der Staffel).</param>
/// <param name="Pflichtspielzeitraeume">Zeiträume mit Pflichtspielen (<c>mandatorygames</c>).</param>
/// <param name="Setzliste">Setzliste (<c>ranking</c>); <c>null</c>, wenn keine angelegt ist.</param>
/// <param name="BestehenderSpielplan">Bereits in click-TT vorhandener Spielplan (<c>existingschedule</c>).</param>
public sealed record Staffel(
    string Name,
    string Id,
    string Geschlecht,
    DateOnly? Beginn,
    DateOnly? Ende,
    DateOnly? Rueckrundenbeginn,
    IReadOnlyList<Mannschaft> Mannschaften,
    IReadOnlyList<Spiel> VorgegebeneSpiele,
    IReadOnlyList<DateOnly> SpielfreieTage,
    IReadOnlyList<Pflichtspielzeitraum> Pflichtspielzeitraeume,
    Setzliste? Setzliste,
    IReadOnlyList<Spiel> BestehenderSpielplan)
{
    /// <summary>
    /// Holt die vom Staffelleiter gewählte Einteilung der Kriterien in die Stufen A, B und C mit ihrer Reihenfolge
    /// (Attribut <c>kriterienstufen</c> am Knoten <c>plan</c>, z. B. <c>A:Hallenbelegung,ParalleleSpiele;B:…;C:…</c>;
    /// nicht im Original, das unbekannte Attribute beim Laden übernimmt und beim Speichern wieder schreibt). Leer = Standard.
    /// </summary>
    public string Kriterienstufen { get; init; } = string.Empty;

    /// <summary>Holt eine leere Staffel (Original: „Neu“ im Menü).</summary>
    public static Staffel Leer { get; } = new(string.Empty, string.Empty, string.Empty, null, null, null, [], [], [], [], null, []);
}
