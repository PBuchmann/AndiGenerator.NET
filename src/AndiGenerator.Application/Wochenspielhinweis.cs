// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Art eines Hinweises der Prüfung „Spiele unter der Woche“.</summary>
public enum Wochenspielhinweis
{
    /// <summary>Weiter als die Grenze, aber Spiele unter der Woche nicht gesperrt.</summary>
    SperreFehlt,

    /// <summary>Näher als die Grenze, aber gesperrt (und nicht pauschal alle Gegner).</summary>
    SperreUnnoetig,
}
