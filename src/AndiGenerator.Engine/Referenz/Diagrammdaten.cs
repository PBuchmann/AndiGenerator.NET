// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Inhalt des Tabs „Diagramme“ im Original (<c>PaintVerteilung</c>).</summary>
/// <param name="HatSpiele"><c>false</c>, wenn kein Spiel terminiert ist (Original <c>NoGames</c>).</param>
/// <param name="Erster">Frühester Wunschtermin (Original <c>GetFirstDate</c>), Nullpunkt der Zeitachse.</param>
/// <param name="Letzter">Spätester Wunschtermin (Original <c>GetLastDate</c>).</param>
/// <param name="Mannschaften">Die Mannschaften in der Reihenfolge der Staffel.</param>
/// <param name="Ueberlappungen">Überlappende Spieltage in Zeichenreihenfolge (helle zuerst).</param>
/// <param name="HatKoppeltermine">Koppel- oder Auswärtskoppeltermine vorhanden (Legende der Spielverteilung).</param>
/// <param name="MitRueckrunde">Rückrunde wird geplant (Diagramm „Abstand Heimspiel zu Auswärtsspiel“).</param>
/// <param name="Wochen">Spiele pro Woche für Hinrunde und ggf. Rückrunde.</param>
/// <param name="HatSetzliste">Setzliste vorhanden (Diagramm „Setzliste“).</param>
/// <param name="Rundenanzahl">Anzahl der Runden.</param>
/// <param name="MaxSpiele">Größte Anzahl Spiele einer Mannschaft.</param>
public sealed record Diagrammdaten(
    bool HatSpiele,
    DateTime Erster,
    DateTime Letzter,
    IReadOnlyList<Diagrammmannschaft> Mannschaften,
    IReadOnlyList<Spieltagsueberlappung> Ueberlappungen,
    bool HatKoppeltermine,
    bool MitRueckrunde,
    IReadOnlyList<Wochenstatistik> Wochen,
    bool HatSetzliste,
    int Rundenanzahl,
    int MaxSpiele);
