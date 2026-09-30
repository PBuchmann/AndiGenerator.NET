// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Engine.Referenz;

/// <summary>Ergebnis der Bewertung eines Plans (Inhalt des Kosten-Tabs im Original).</summary>
/// <param name="Gesamtkosten">Gesamtkosten (Original <c>CalculateKosten(-1)</c>).</param>
/// <param name="NichtTerminiert">Kosten nicht terminierter Spiele.</param>
/// <param name="UngueltigeSpiele">Kosten ungültiger Spiele.</param>
/// <param name="SpieleAnSpielfreienTagen">Kosten für Spiele an spielfreien Tagen.</param>
/// <param name="UeberlappungSpieltage">Überlappung der Spieltage.</param>
/// <param name="LaengeSpieltage">Länge der Spieltage.</param>
/// <param name="UeberlappungLetzterSpieltag">Überlappung des letzten Spieltags.</param>
/// <param name="LaengeLetzterSpieltag">Länge des letzten Spieltags.</param>
/// <param name="VereinsinterneSpieleAmAnfang">Vereinsinterne Spiele am Anfang.</param>
/// <param name="SichtbareKostenarten">Spalten der Kostentabelle (Original <c>HasValuesForType</c>).</param>
/// <param name="Mannschaften">Zeilen der Kostentabelle in der Reihenfolge des Originals.</param>
public sealed record Planbewertung(
    double Gesamtkosten,
    double NichtTerminiert,
    double UngueltigeSpiele,
    double SpieleAnSpielfreienTagen,
    double UeberlappungSpieltage,
    double LaengeSpieltage,
    double UeberlappungLetzterSpieltag,
    double LaengeLetzterSpieltag,
    double VereinsinterneSpieleAmAnfang,
    IReadOnlyList<MannschaftsKostenart> SichtbareKostenarten,
    IReadOnlyList<Mannschaftsbewertung> Mannschaften);
