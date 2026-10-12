// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Spielstärke einer Mannschaft aus der Mannschaftsmeldung von click-TT.</summary>
/// <param name="Mannschaft">Name der Mannschaft in der Staffel.</param>
/// <param name="Schnitt">Schnitt der TTR-Werte der gewerteten Stammspieler; 0 ohne Aufstellung.</param>
/// <param name="Stammspieler">Zahl der gemeldeten Stammspieler.</param>
public sealed record Mannschaftsstaerke(string Mannschaft, double Schnitt, int Stammspieler)
{
    /// <summary>Holt einen Wert, der angibt, ob für die Mannschaft eine Aufstellung gefunden wurde.</summary>
    public bool HatAufstellung => Stammspieler > 0;
}
