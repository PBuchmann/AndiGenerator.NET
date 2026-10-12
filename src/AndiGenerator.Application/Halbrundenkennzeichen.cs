// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Kennzeichen einer Klasse, die nur eine Halbrunde spielt, im Klassennamen (nicht im Original).</summary>
public enum Halbrundenkennzeichen
{
    /// <summary>Kein Kennzeichen: Vor- und Rückrunde.</summary>
    Keins,

    /// <summary>„(HR)“: nur Hinrunde bzw. Herbstrunde.</summary>
    Hinrunde,

    /// <summary>„(FR)“: nur Finalrunde bzw. Frühlingsrunde, also die Rückrunde.</summary>
    Rueckrunde,
}
