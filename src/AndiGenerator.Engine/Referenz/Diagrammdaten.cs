// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Inhalt des Tabs „Diagramme“ im Original (<c>PaintVerteilung</c>).</summary>
/// <param name="HatSpiele"><c>false</c>, wenn kein Spiel terminiert ist (Original <c>NoGames</c>).</param>
/// <param name="Erster">Frühester Wunschtermin (Original <c>GetFirstDate</c>), Nullpunkt der Zeitachse.</param>
/// <param name="Letzter">Spätester Wunschtermin (Original <c>GetLastDate</c>).</param>
/// <param name="Mannschaften">Die Mannschaften in der Reihenfolge der Staffel.</param>
/// <param name="MitRueckrunde">
/// Die Gegner treffen mehrmals aufeinander: Rückrunde wird geplant oder Doppelrunde (Diagramm „Abstand Hin-/Rückspiel“).
/// </param>
/// <param name="Wochen">Spiele pro Woche für Hinrunde und ggf. Rückrunde.</param>
/// <param name="HatSetzliste">Setzliste vorhanden (Diagramm „Setzliste“).</param>
/// <param name="MaxSpiele">Größte Anzahl Spiele einer Mannschaft.</param>
/// <param name="Kompakttage">
/// Die Kompaktspieltage mit Ausrichter (nur im Kompaktmodus): Statt des Wechsels Heim/Auswärts zeigt das Diagramm dann,
/// wer wie oft ausrichtet und wo jede Mannschaft spielt.
/// </param>
/// <param name="Verstoesse">Die Verstöße, wie die Bewertung sie zählt (rechte Spalten und Markierungen der Diagramme).</param>
/// <param name="Rundennamen">Die Namen der Runden (Index wie <see cref="Diagrammspiel.Runde"/>); <c>null</c> = Vorrunde und Rückrunde.</param>
public sealed record Diagrammdaten(
    bool HatSpiele,
    DateTime Erster,
    DateTime Letzter,
    IReadOnlyList<Diagrammmannschaft> Mannschaften,
    bool MitRueckrunde,
    IReadOnlyList<Wochenstatistik> Wochen,
    bool HatSetzliste,
    int MaxSpiele,
    IReadOnlyList<Ausrichtertag>? Kompakttage = null,
    Diagrammverstoesse? Verstoesse = null,
    IReadOnlyList<string>? Rundennamen = null);
