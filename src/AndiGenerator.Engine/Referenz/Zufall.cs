// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Zufallszahlen wie Delphi <c>Random(n)</c> (= <c>Trunc(n · x)</c>, x gleichverteilt in [0, 1)), aber mit eigenem Startwert.</summary>
internal sealed class Zufall(int startwert)
{
    private readonly Random quelle = new(startwert);

    /// <summary>Delphi <c>Random(n)</c>; für n ≤ 0 wie im Original 0.</summary>
    public int Zahl(int n) => n <= 0 ? 0 : (int)(n * quelle.NextDouble());

    /// <summary>Original <c>PercentRandom</c>.</summary>
    public bool Prozent(int prozent) => Zahl(100) < prozent;
}
