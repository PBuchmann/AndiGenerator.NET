// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eine Fahrt zu einem Kompaktspieltag im Kartenbild.</summary>
/// <param name="Von">Halle des Gastes.</param>
/// <param name="Nach">Halle des Ausrichters.</param>
/// <param name="Kraeftig">Teil der Auswahl (sonst blass, wenn alle Fahrten gezeigt werden).</param>
/// <param name="Einzel">Fahrt zu einer Einzelbegegnung (gelb wie in der Planmatrix).</param>
public sealed record Kartenfahrt(Kartenpunkt Von, Kartenpunkt Nach, bool Kraeftig, bool Einzel)
{
    /// <summary>
    /// Holt den seitlichen Versatz der Linie in Linienbreiten (quer zur Fahrtrichtung, positiv = links): Mehrere Fahrten
    /// zwischen denselben beiden Hallen liegen so sichtbar nebeneinander; 0 = einzige Fahrt.
    /// </summary>
    public double Versatz { get; init; }

    /// <summary>Holt einen Wert, der angibt, ob es zwischen denselben Hallen mehrere Fahrten gibt (dann eigener Zielpunkt je Linie).</summary>
    public bool Mehrfach { get; init; }
}
