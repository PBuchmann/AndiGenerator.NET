// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Terminwuensche;

/// <summary>Ein Wort einer PDF-Seite mit seiner linken Kante (in Punkt).</summary>
/// <param name="Links">Abstand der linken Kante vom linken Seitenrand.</param>
/// <param name="Text">Das Wort.</param>
public sealed record Textwort(double Links, string Text);
