// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Inseln;

/// <summary>
/// Kriterium des Automodus (MIGRATIONSPLAN Abschnitt 11): die 16 Mannschafts-Kostenarten in derselben Reihenfolge
/// wie <see cref="Domain.Optionen.MannschaftsKostenart"/>, dazu die Kostenarten des ganzen Plans.
/// </summary>
public enum Kostenkriterium
{
    /// <summary>Hallenbelegung.</summary>
    Hallenbelegung,

    /// <summary>Parallele Spiele mit Nachbarmannschaften.</summary>
    ParalleleSpiele,

    /// <summary>Gleiche Heimtermine erzwingen.</summary>
    GleicheHeimtermine,

    /// <summary>Sperrtermine.</summary>
    Sperrtermine,

    /// <summary>Ausweichtermine.</summary>
    Ausweichtermine,

    /// <summary>60-km-Regel.</summary>
    SechzigKilometerRegel,

    /// <summary>3-Tage-Abstand.</summary>
    DreiTageAbstand,

    /// <summary>2 Spiele pro Woche.</summary>
    ZweiSpieleProWoche,

    /// <summary>Spielverteilung.</summary>
    Spielverteilung,

    /// <summary>Heim-Koppeltermine.</summary>
    Heimkoppel,

    /// <summary>Auswärtskoppel.</summary>
    Auswaertskoppel,

    /// <summary>Ungleich Heim/Auswärts.</summary>
    UngleichHeimAuswaerts,

    /// <summary>Wechsel Heim/Auswärts.</summary>
    WechselHeimAuswaerts,

    /// <summary>Abstand Heim/Auswärts.</summary>
    AbstandHeimAuswaerts,

    /// <summary>Setzliste.</summary>
    Setzliste,

    /// <summary>Pflichtspieltage.</summary>
    Pflichtspieltage,

    /// <summary>Vereinsinterne Spiele am Anfang (ganzer Plan; Anzahl 1, wenn verletzt).</summary>
    VereinsinterneSpieleAmAnfang,

    /// <summary>Länge der Spieltage (ganzer Plan, nur Kosten).</summary>
    Spieltaglaenge,

    /// <summary>Überlappung der Spieltage (ganzer Plan, nur Kosten).</summary>
    Spieltagueberlappung,

    /// <summary>Länge des letzten Spieltags (ganzer Plan, nur Kosten).</summary>
    LetzterSpieltagLaenge,

    /// <summary>Überlappung des letzten Spieltags (ganzer Plan, nur Kosten).</summary>
    LetzterSpieltagUeberlappung,
}
