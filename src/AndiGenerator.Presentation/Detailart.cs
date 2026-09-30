// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Art einer Zeile in der Aufzählung eines Einrichtungsschritts.</summary>
public enum Detailart
{
    /// <summary>Gewöhnlicher Aufzählungspunkt.</summary>
    Punkt,

    /// <summary>Überschrift einer Gruppe (ohne Aufzählungszeichen).</summary>
    Ueberschrift,

    /// <summary>Eingerückter Punkt einer Gruppe.</summary>
    Unterpunkt,
}
