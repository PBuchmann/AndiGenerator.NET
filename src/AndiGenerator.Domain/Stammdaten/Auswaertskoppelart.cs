// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Stammdaten;

/// <summary>Art einer Auswärtskoppel (Original <c>sameday</c>, <c>ktSameDay</c>/<c>ktDiffDay</c>/<c>ktAllDay</c>).</summary>
public enum Auswaertskoppelart
{
    /// <summary>Beide Spiele am selben Tag (<c>sameday</c> = 0).</summary>
    AmSelbenTag = 0,

    /// <summary>Beide Spiele an aufeinanderfolgenden Tagen (<c>sameday</c> = 1).</summary>
    AnVerschiedenenTagen = 1,

    /// <summary>Beides erlaubt (jeder andere Wert, geschrieben wird 2).</summary>
    Beliebig = 2,
}
