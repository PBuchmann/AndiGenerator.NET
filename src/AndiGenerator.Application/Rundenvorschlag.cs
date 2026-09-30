// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Vorschlag zur Rundenplanung beim Öffnen einer Staffel (Original <c>OpenFile</c>, Heuristiken zur Runde).</summary>
public enum Rundenvorschlag
{
    /// <summary>Kein Vorschlag.</summary>
    Keiner,

    /// <summary>Die Runde ist kürzer als 6 Monate: Vorschlag, nur eine Halbrunde zu generieren.</summary>
    Halbrunde,

    /// <summary>Der Rückrundenbeginn liegt weniger als 60 Tage entfernt: Vorschlag, nur die Rückrunde zu generieren.</summary>
    NurRueckrunde,
}
