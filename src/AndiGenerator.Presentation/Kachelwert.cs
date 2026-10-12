// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Wert in einer Kachel, z. B. „261“ mit „Heimtermine erhalten die Spiellokal-Nummer“.</summary>
/// <param name="Wert">Der Wert.</param>
/// <param name="Text">Die Beschriftung.</param>
public sealed record Kachelwert(string Wert, string Text);
