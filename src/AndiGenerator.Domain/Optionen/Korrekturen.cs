// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Optionen;

/// <summary>
/// Korrekturen an Fehlern des Originals in Suche und Kostenrechnung (Klasse-3-Befunde, MIGRATIONSPLAN E9, Entscheidung
/// 11.10.2026). Jede lässt sich einzeln abschalten; <see cref="Keine"/> rechnet und sucht genau wie das Original
/// (Paritätstests, Vergleichsmessungen).
/// </summary>
/// <param name="WunschterminNurVomEigentuemerFreigeben">
/// Befund #3 (<c>TGame.setDate</c>): Ein Spiel gibt beim Umsetzen nur einen Wunschtermin frei, den es selbst belegt. Im
/// Original kann das Zurücksetzen eines Spiels den Termin eines anderen Spiels als frei markieren.
/// </param>
/// <param name="LetzteUeberlappungSummieren">
/// Befund #4 (<c>LastKostenOverlap</c>): Die Überlappungen des letzten Spieltags werden summiert. Das Original behält nur
/// die des ältesten überlappenden Spieltags (den größten Anteil).
/// </param>
/// <param name="VollstaendigesRaster">
/// Befund #5 (<c>CreateRasterOneRound</c>): Bei ungerader Mannschaftszahl legt das Raster alle n Spieltage des
/// Kreisverfahrens an. Das Original legt einen zu wenig an; dessen Spiele setzt es danach zufällig.
/// </param>
/// <param name="AuswaertskoppelNurOffeneSpiele">
/// Befund #6 (<c>FillTermineIntern</c>): Beim Füllen werden nur Spiele ohne Termin terminiert, auch über den Weg der
/// Auswärtskoppel. Das Original kann dort ein schon terminiertes Spiel neu terminieren (bei der Doppelrunde auch in eine
/// andere Hauptrunde).
/// </param>
public sealed record Korrekturen(
    bool WunschterminNurVomEigentuemerFreigeben,
    bool LetzteUeberlappungSummieren,
    bool VollstaendigesRaster,
    bool AuswaertskoppelNurOffeneSpiele)
{
    /// <summary>Alle Korrekturen (Standard).</summary>
    public static Korrekturen Alle { get; } = new(true, true, true, true);

    /// <summary>Keine Korrektur: wie das Original.</summary>
    public static Korrekturen Keine { get; } = new(false, false, false, false);
}
