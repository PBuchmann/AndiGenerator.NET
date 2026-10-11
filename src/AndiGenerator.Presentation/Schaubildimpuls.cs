// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Ereignis im Schaubild mit dem Zeitpunkt, zu dem es aufleuchtet.</summary>
/// <param name="Art">Was geschah.</param>
/// <param name="Insel">Index der Insel (0 …); −1, wenn keine bestimmte.</param>
/// <param name="Zeitpunkt">Zeitstempel (<see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>).</param>
public sealed record Schaubildimpuls(Schaubildereignis Art, int Insel, long Zeitpunkt);
