// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Einordnung eines Hinweises zu einem Spiel für die Farbe seines Chips.</summary>
public enum Hinweisart
{
    /// <summary>Verstoß (rot): spielfreier Tag, kein Wunschtermin, doppeltes Spiel, ungültiger Termin.</summary>
    Problem,

    /// <summary>Beachten (ocker): Ausweichtermin, geänderte Uhrzeit.</summary>
    Warnung,

    /// <summary>Information (blau): Koppelspiel, manuell festgelegter Termin.</summary>
    Info,

    /// <summary>Neutral (grau): Spiellokal.</summary>
    Neutral,
}
