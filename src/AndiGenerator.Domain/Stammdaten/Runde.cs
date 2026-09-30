// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Stammdaten;

/// <summary>Hin- oder Rückrunde.</summary>
public enum Runde
{
    /// <summary>Hinrunde (Original <c>hrRound1</c>).</summary>
    Hinrunde = 1,

    /// <summary>Rückrunde (Original <c>hrRound2</c>).</summary>
    Rueckrunde = 2,
}
