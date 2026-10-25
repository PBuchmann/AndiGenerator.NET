// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Bewertung des Abstands zwischen Hin- und Rückspiel im Diagramm.</summary>
public enum Abstandsstufe
{
    /// <summary>Höchstens zwei Spiele vom idealen Abstand entfernt.</summary>
    ImSoll,

    /// <summary>Außerhalb des Solls, aber weniger als die Hälfte des idealen Abstands daneben (knapp oder zu lang).</summary>
    Auffaellig,

    /// <summary>Mindestens die Hälfte des idealen Abstands daneben: viel zu kurz oder viel zu lang.</summary>
    WeitDaneben,
}
