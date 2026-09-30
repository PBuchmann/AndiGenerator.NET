// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Die Meldungen einer Mannschaft als Kachel der Meldungsansicht.</summary>
/// <param name="Name">Name der Mannschaft.</param>
/// <param name="Anzahl">Zahl der Meldungen als Text, z. B. „5 Meldungen“.</param>
/// <param name="Meldungen">Die Meldungen.</param>
public sealed record Meldungsgruppe(string Name, string Anzahl, IReadOnlyList<string> Meldungen);
