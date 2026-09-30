// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Optionen;

/// <summary>Gewichtungsstufe einer Kostenart (Original <c>TCalculateOptionsValues</c>, Reihenfolge ist fachlich relevant).</summary>
public enum Gewichtung
{
    /// <summary>„Nicht berücksichtigen“ (<c>coIgnore</c>).</summary>
    NichtBeruecksichtigen,

    /// <summary>„sehr wenig“ (<c>coSehrWenig</c>).</summary>
    SehrWenig,

    /// <summary>„wenig“ (<c>coWenig</c>).</summary>
    Wenig,

    /// <summary>„normal“ (<c>coNormal</c>, Standard).</summary>
    Normal,

    /// <summary>„hoch“ (<c>coHoch</c>).</summary>
    Hoch,

    /// <summary>„sehr hoch“ (<c>coSehrHoch</c>).</summary>
    SehrHoch,

    /// <summary>„extrem hoch“ (<c>coExtremHoch</c>).</summary>
    ExtremHoch,
}

/// <summary>Kostenarten je Mannschaft (Original <c>TMannschaftsKostenType</c>; Reihenfolge wie im Original).</summary>
public enum MannschaftsKostenart
{
    /// <summary>Hallenbelegung (<c>mktHalleBelegt</c>).</summary>
    Hallenbelegung,

    /// <summary>Parallele Spiele mit Nachbarmannschaften (<c>mktSisterGames</c>).</summary>
    ParalleleSpiele,

    /// <summary>Gleiche Heimtermine erzwingen (<c>mktForceSameHomeGames</c>).</summary>
    GleicheHeimtermine,

    /// <summary>Sperrtermine (<c>mktSperrTermine</c>).</summary>
    Sperrtermine,

    /// <summary>Ausweichtermine (<c>mktAusweichTermine</c>).</summary>
    Ausweichtermine,

    /// <summary>60-km-Regel (<c>mkt60Kilometer</c>).</summary>
    SechzigKilometerRegel,

    /// <summary>3-Tage-Abstand (<c>mktEngeTermine</c>).</summary>
    DreiTageAbstand,

    /// <summary>2 Spiele pro Woche (<c>mkt2SpieleProWoche</c>).</summary>
    ZweiSpieleProWoche,

    /// <summary>Spielverteilung (<c>mktSpielverteilung</c>).</summary>
    Spielverteilung,

    /// <summary>Heim-Koppeltermine (<c>mktKoppelTermine</c>).</summary>
    Heimkoppel,

    /// <summary>Auswärtskoppel (<c>mktAuswaertsKoppelTermine</c>).</summary>
    Auswaertskoppel,

    /// <summary>Ungleich Heim/Auswärts (<c>mktZahlHeimSpielTermine</c>).</summary>
    UngleichHeimAuswaerts,

    /// <summary>Wechsel Heim/Auswärts (<c>mktWechselHeimAuswaerts</c>).</summary>
    WechselHeimAuswaerts,

    /// <summary>Abstand Heim/Auswärts (<c>mktAbstandHeimAuswaerts</c>).</summary>
    AbstandHeimAuswaerts,

    /// <summary>Setzliste (<c>mktRanking</c>).</summary>
    Setzliste,

    /// <summary>Pflichtspieltage (<c>mktMandatoryGames</c>).</summary>
    Pflichtspieltage,
}

/// <summary>Welche Runden geplant werden (Original <c>TRoundPlaning</c>).</summary>
public enum Rundenplanung
{
    /// <summary>Hin- und Rückrunde (<c>rpBoth</c>, Standard).</summary>
    Beide,

    /// <summary>Halbrunde (<c>rpHalfRound</c>).</summary>
    Halbrunde,

    /// <summary>Nur Vorrunde (<c>rpFirstOnly</c>).</summary>
    NurVorrunde,

    /// <summary>Nur Rückrunde (<c>rpSecondOnly</c>).</summary>
    NurRueckrunde,

    /// <summary>„Corona“-Rückrunde (<c>rpCorona</c>, bleibt laut E9 erhalten).</summary>
    Corona,
}
