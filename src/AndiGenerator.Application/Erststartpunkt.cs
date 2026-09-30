// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Die Punkte des Erststart-Assistenten in der Reihenfolge des Originals (<c>TMessages</c>).</summary>
public enum Erststartpunkt
{
    /// <summary>Spiellokale (<c>tmLocations</c>).</summary>
    Spiellokale,

    /// <summary>Koppeltermine (<c>tmKoppel</c>).</summary>
    Koppeltermine,

    /// <summary>Auswärtskoppelwünsche (<c>tmAuswaertskoppel</c>).</summary>
    Auswaertskoppeln,

    /// <summary>Setzliste (<c>tmSetzliste</c>).</summary>
    Setzliste,

    /// <summary>Pflichtspieltage (<c>tmMandatoryDays</c>).</summary>
    Pflichtspieltage,
}
