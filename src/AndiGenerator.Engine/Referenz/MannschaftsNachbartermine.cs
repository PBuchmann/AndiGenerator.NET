// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Gemeldete Spiele der Nachbarmannschaften einer Mannschaft, je Tag gruppiert.</summary>
/// <param name="Name">Mannschaft der Staffel.</param>
/// <param name="Tage">Je Spieltag die Spiele der Nachbarmannschaften, nach Termin sortiert.</param>
public sealed record MannschaftsNachbartermine(string Name, IReadOnlyList<IReadOnlyList<Nachbartermin>> Tage);
