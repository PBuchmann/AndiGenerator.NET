// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Die Führungen „Schritt für Schritt“, je Art der Optimierung eine.</summary>
public enum Fuehrungsart
{
    /// <summary>Normale Runden mit der Kostenoptimierung.</summary>
    Kostenoptimierung,

    /// <summary>Normale Runden im Automodus.</summary>
    Automodus,

    /// <summary>Klassen mit Kompaktspieltagen.</summary>
    Kompaktspieltage,
}
