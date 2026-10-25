// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Was im Schaubild des Leitstands kurz aufleuchtet.</summary>
public enum Schaubildereignis
{
    /// <summary>Der beste Plan ist besser geworden (der Plan kommt von einer Insel über den Koordinator).</summary>
    BesterPlan,

    /// <summary>Eine Insel beginnt neu.</summary>
    Neustart,

    /// <summary>Die Spezial-Insel gibt ihren Plan an eine Insel weiter.</summary>
    Uebergabe,

    /// <summary>Der Automodus hat Gewichte angehoben.</summary>
    Anhebung,

    /// <summary>Ein Plan aus HiGHS (Teilneuplanung oder untere Schranke) ist bester Plan geworden.</summary>
    Teilneuplanung,

    /// <summary>Die Teilneuplanung übernimmt den besseren Plan der Inseln als neuen Ausgangsplan.</summary>
    Ausgangsplan,
}
