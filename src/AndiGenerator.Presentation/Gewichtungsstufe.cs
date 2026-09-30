// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Presentation;

/// <summary>Eine Taste der Gewichtung in den Details der Kostenansicht.</summary>
/// <param name="Wert">Die Stufe.</param>
/// <param name="Name">Kurzname („aus“, „sehr wenig“ … „extrem hoch“).</param>
/// <param name="Aktiv">Die Stufe ist eingestellt.</param>
public sealed record Gewichtungsstufe(Gewichtung Wert, string Name, bool Aktiv);
