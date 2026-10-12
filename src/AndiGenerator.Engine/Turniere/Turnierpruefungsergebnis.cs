// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Ergebnis von <see cref="Turnierpruefung"/>.</summary>
/// <param name="Turniere">Die Turniere mit Ausrichter und Hinweisen.</param>
/// <param name="Befunde">Alle Verstöße und Auffälligkeiten.</param>
/// <param name="Ausgerichtet">Wie oft jede Mannschaft ausrichtet.</param>
public sealed record Turnierpruefungsergebnis(
    IReadOnlyList<Turnierbewertung> Turniere,
    IReadOnlyList<string> Befunde,
    IReadOnlyDictionary<string, int> Ausgerichtet);
