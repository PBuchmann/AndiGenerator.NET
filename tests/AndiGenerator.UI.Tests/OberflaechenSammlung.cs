// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.UI.Tests;

/// <summary>Alle Oberflächentests teilen sich eine <see cref="Oberflaeche"/> und laufen nacheinander.</summary>
[CollectionDefinition(Name)]
public sealed class OberflaechenSammlung : ICollectionFixture<Oberflaeche>
{
    /// <summary>Name der Sammlung.</summary>
    public const string Name = "Oberfläche";
}
