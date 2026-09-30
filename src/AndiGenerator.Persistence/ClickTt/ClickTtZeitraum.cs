// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.ClickTt;

/// <summary>Zeitraum <c>from</c>–<c>until</c> (Sperrtermine bzw. spielfreie Tage), jeweils einschließlich.</summary>
public sealed record ClickTtZeitraum(DateOnly Von, DateOnly Bis);
