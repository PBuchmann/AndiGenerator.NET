// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Optionen;

/// <summary>
/// Einstellungen einer Staffel (Original <c>TCalculateOptions</c>, Datei <c>AndiGenerator.options</c>).
/// </summary>
/// <param name="FreitagZaehltZumWochenende">Attribut <c>friday-is-part-of-weekend</c> (60-km-Regel), Standard <c>true</c>.</param>
/// <param name="Spieltag">Plan-Kostenart „Länge Spieltage“ (<c>gameday</c>).</param>
/// <param name="SpieltagUeberlappung">Plan-Kostenart „Überlappung Spieltage“ (<c>gameday-overlapp</c>).</param>
/// <param name="LetzterSpieltag">Plan-Kostenart „Länge letzter Spieltag“ (<c>last-gameday</c>).</param>
/// <param name="LetzterSpieltagUeberlappung">Plan-Kostenart „Überlappung letzter Spieltag“ (<c>last-gameday-overlapp</c>).</param>
/// <param name="VereinsinterneSpieleAmAnfang">Plan-Kostenart (<c>sister-game-at-start</c>).</param>
/// <param name="JeKostenart">Gewichtung je <see cref="MannschaftsKostenart"/> für alle Mannschaften (Länge 16).</param>
/// <param name="Mannschaften">Abweichende Gewichtungen einzelner Mannschaften (Reihenfolge wie gelesen).</param>
/// <param name="AutomatischeMitte">Rundenmitte bei Doppelrunde automatisch bestimmen.</param>
/// <param name="Mitte1">Manuelle erste Rundenmitte (nur ohne Automatik).</param>
/// <param name="Mitte2">Manuelle zweite Rundenmitte (nur ohne Automatik).</param>
public sealed record Berechnungsoptionen(
    bool FreitagZaehltZumWochenende,
    Gewichtung Spieltag,
    Gewichtung SpieltagUeberlappung,
    Gewichtung LetzterSpieltag,
    Gewichtung LetzterSpieltagUeberlappung,
    Gewichtung VereinsinterneSpieleAmAnfang,
    IReadOnlyList<Gewichtung> JeKostenart,
    IReadOnlyList<MannschaftsGewichtung> Mannschaften,
    Rundenplanung Rundenplanung,
    bool Doppelrunde,
    bool AutomatischeMitte,
    DateOnly? Mitte1,
    DateOnly? Mitte2)
{
    /// <summary>Anzahl der Mannschafts-Kostenarten.</summary>
    public static readonly int AnzahlKostenarten = Enum.GetValues<MannschaftsKostenart>().Length;

    /// <summary>Standardeinstellungen (Original <c>TCalculateOptions.Clear</c>).</summary>
    public static Berechnungsoptionen Standard { get; } = new(
        FreitagZaehltZumWochenende: true,
        Spieltag: Gewichtung.Normal,
        SpieltagUeberlappung: Gewichtung.Normal,
        LetzterSpieltag: Gewichtung.Normal,
        LetzterSpieltagUeberlappung: Gewichtung.Normal,
        VereinsinterneSpieleAmAnfang: Gewichtung.Normal,
        JeKostenart: Enumerable.Repeat(Gewichtung.Normal, AnzahlKostenarten).ToArray(),
        Mannschaften: [],
        Rundenplanung: Rundenplanung.Beide,
        Doppelrunde: false,
        AutomatischeMitte: true,
        Mitte1: null,
        Mitte2: null);

    /// <summary>Gewichtung einer Kostenart für alle Mannschaften.</summary>
    /// <param name="art">Kostenart.</param>
    /// <returns>Die Gewichtung dieser Kostenart.</returns>
    public Gewichtung Fuer(MannschaftsKostenart art) => JeKostenart[(int)art];
}
