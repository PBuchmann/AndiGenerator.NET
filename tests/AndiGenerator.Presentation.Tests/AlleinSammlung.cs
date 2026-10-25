// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation.Tests;

/// <summary>Testklassen, die allein laufen müssen (nicht parallel zu anderen), z. B. weil sie unter Last zu langsam würden.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AlleinSammlung
{
    /// <summary>Der Name der Sammlung.</summary>
    public const string Name = "Allein";

    private AlleinSammlung()
    {
    }
}
