// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Engine.Inseln;

/// <summary>Ein Gewicht, das der Automodus gegenüber den Gewichtungen des Anwenders geändert hat.</summary>
/// <param name="Kriterium">Das Kriterium.</param>
/// <param name="Mannschaft">Die Mannschaft; <c>null</c> = das Gewicht des ganzen Kriteriums.</param>
/// <param name="Vorher">Gewichtung des Anwenders.</param>
/// <param name="Jetzt">Aktuelle Gewichtung des Automodus.</param>
public sealed record Gewichtsanhebung(Kostenkriterium Kriterium, string? Mannschaft, Gewichtung Vorher, Gewichtung Jetzt);
