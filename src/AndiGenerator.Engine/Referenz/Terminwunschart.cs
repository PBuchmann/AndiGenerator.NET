// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Art einer Marke im Terminwunsch-Diagramm (Original <c>PaintMeldungMannschaft</c>).</summary>
public enum Terminwunschart
{
    /// <summary>Heimspielwunsch (schwarz).</summary>
    Wunsch,

    /// <summary>Koppeltermin oder Doppelspieltag (blau).</summary>
    Koppel,

    /// <summary>Sperrtermin (rot).</summary>
    Sperrtermin,
}
