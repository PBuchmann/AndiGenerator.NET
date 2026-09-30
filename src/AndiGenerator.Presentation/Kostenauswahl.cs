// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Presentation;

/// <summary>
/// Auswahl in der Kostenansicht; über Namen und Kostenart statt über Positionen, damit sie erhalten bleibt, wenn sich die
/// Tabelle während der Generierung neu sortiert.
/// </summary>
/// <param name="Art">Was ausgewählt ist.</param>
/// <param name="Mannschaft">Name der Mannschaft (Zelle, Mannschaft).</param>
/// <param name="Kostenart">Die Kostenart (Zelle, Kostenart).</param>
/// <param name="Kennzahl">Name der Kennzahl (Kennzahl).</param>
public sealed record Kostenauswahl(Kostenauswahlart Art, string Mannschaft, MannschaftsKostenart Kostenart, string Kennzahl);
