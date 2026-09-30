// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Presentation;

/// <summary>Anteil einer Kostenart an den Gesamtkosten („Wo stecken die Kosten?“).</summary>
/// <param name="Kostenart">Die Kostenart.</param>
/// <param name="Name">Anzeigename.</param>
/// <param name="Anteil">Anteil an den Mannschaftskosten (0–1).</param>
/// <param name="Prozent">Anteil als Text, z. B. „38 %“.</param>
public sealed record Kostenanteil(MannschaftsKostenart Kostenart, string Name, double Anteil, string Prozent);
