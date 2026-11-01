// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Engine.Einfach;

/// <summary>Worin sich ein Vorschlag von der Empfehlung unterscheidet: ein Kriterium mit seinen beiden Werten.</summary>
/// <param name="Kriterium">Das Kriterium.</param>
/// <param name="Von">Verstöße in der Empfehlung.</param>
/// <param name="Nach">Verstöße im Vorschlag.</param>
public sealed record Unterschied(Kostenkriterium Kriterium, int Von, int Nach)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Massstab.Kriteriumsname(Kriterium)} {Von} → {Nach}";
}
