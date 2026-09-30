// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Tabellen;

/// <summary>Zahlenformat einer Zelle.</summary>
public enum Zellformat
{
    /// <summary>Text wie er ist, Zahlen ohne besonderes Format.</summary>
    Standard,

    /// <summary>Datum <c>TT.MM.JJJJ</c>.</summary>
    Datum,

    /// <summary>Uhrzeit <c>hh:mm</c>.</summary>
    Uhrzeit,

    /// <summary>Ganze Zahl mit Tausenderpunkt.</summary>
    Ganzzahl,
}
