// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Wie viel besser ein Plan in einer Stufe noch werden kann (Kopfzeile der Stufe in der Qualitätsansicht).</summary>
public enum Spielraumzustand
{
    /// <summary>Keine Aussage möglich (keine Grenze, HiGHS rechnet, oder sie gilt erst bei bestmöglicher Stufe darüber).</summary>
    Unbekannt,

    /// <summary>Der Plan erreicht die Grenze: besser geht es nicht.</summary>
    Bestmoeglich,

    /// <summary>Zwischen Plan und Grenze ist noch Platz.</summary>
    Offen,
}
