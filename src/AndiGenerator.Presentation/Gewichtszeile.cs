// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein vom Automodus geändertes Gewicht als Stufenleiste: bis zur Gewichtung des Anwenders grau, angehoben farbig.</summary>
/// <param name="Wer">„alle Mannschaften“ oder der Name der Mannschaft.</param>
/// <param name="Segmente">Die Stufen „sehr wenig“ bis „extrem hoch“.</param>
/// <param name="Text">Vorher und jetzt, z. B. „normal → sehr hoch“.</param>
public sealed record Gewichtszeile(string Wer, IReadOnlyList<Gewichtssegment> Segmente, string Text);
