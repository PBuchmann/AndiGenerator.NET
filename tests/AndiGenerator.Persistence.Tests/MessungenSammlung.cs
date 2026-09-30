// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Tests;

/// <summary>Messungen laufen allein, nicht parallel zu anderen Tests (sonst teilen sie sich die Kerne).</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class MessungenSammlung
{
    public const string Name = "Messungen";

    protected MessungenSammlung()
    {
    }
}
