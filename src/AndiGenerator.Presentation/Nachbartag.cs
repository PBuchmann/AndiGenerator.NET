// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Tag mit Spielen von Nachbarmannschaften.</summary>
/// <param name="Datum">Z. B. „Sa 19.09.2026“.</param>
/// <param name="Spiele">Die Spiele des Tages.</param>
public sealed record Nachbartag(string Datum, IReadOnlyList<Nachbarspiel> Spiele);
