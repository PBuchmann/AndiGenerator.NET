// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Was die <see cref="Kompaktoptimierung"/> suchen soll (nicht im Original).</summary>
/// <param name="KmBezug">Kilometer, auf die sich die Kilometerpunkte beziehen (der kürzeste Plan); <c>null</c> = Schätzung.</param>
/// <param name="NurVierer">Nur 4er-Spieltage (und Einzelbegegnungen).</param>
/// <param name="DreierStrafe">3 Punkte je 3er-Spieltag (wenn ein Plan nur aus 4er-Spieltagen geht).</param>
/// <param name="MitGruppen">Spitzen- und Abstiegsspieltag werten.</param>
/// <param name="GruppenPflicht">Spitzen- und Abstiegsspieltag müssen vorkommen.</param>
internal sealed record Kompaktauftrag(double? KmBezug, bool NurVierer, bool DreierStrafe, bool MitGruppen, bool GruppenPflicht)
{
    /// <summary>Holt Pläne, deren Spieltage (unabhängig vom Tag) nicht noch einmal gewählt werden sollen.</summary>
    public IReadOnlyList<Kompaktloesung> Ausser { get; init; } = [];
}
