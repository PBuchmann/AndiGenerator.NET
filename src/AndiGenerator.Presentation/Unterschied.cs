// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eine Meldung im Vergleich mit dem aktuellen Plan.</summary>
/// <param name="Text">Die Meldung, bei Unterschieden mit <c>+</c> bzw. <c>−</c> davor.</param>
/// <param name="Richtung">+1 = kommt in der Variante dazu (rot), −1 = entfällt dort (grün), 0 = Verstoß des aktuellen Plans.</param>
public sealed record Unterschied(string Text, int Richtung)
{
    /// <summary>Holt einen Wert, der angibt, ob der Verstoß in der Variante dazukommt.</summary>
    public bool IstNeu => Richtung > 0;

    /// <summary>Holt einen Wert, der angibt, ob der Verstoß in der Variante entfällt.</summary>
    public bool Entfaellt => Richtung < 0;
}
