// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Gewichtbare Plan-Kostenarten (Original <c>TPlanKostenType</c>, Zeilen oberhalb der Kostentabelle).</summary>
public enum PlanKostenart
{
    /// <summary>Überlappung der Spieltage (<c>pktGameDayOverlap</c>).</summary>
    UeberlappungSpieltage,

    /// <summary>Länge der Spieltage (<c>pktGameDayLength</c>).</summary>
    LaengeSpieltage,

    /// <summary>Überlappung des letzten Spieltags (<c>pktLastGameDayOverlap</c>).</summary>
    UeberlappungLetzterSpieltag,

    /// <summary>Länge des letzten Spieltags (<c>pktLastGameDayLength</c>).</summary>
    LaengeLetzterSpieltag,

    /// <summary>Vereinsinterne Spiele am Anfang (<c>pktSisterGamesAtBegin</c>).</summary>
    VereinsinterneSpieleAmAnfang,
}
