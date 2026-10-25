// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Zeitgrenze nach Fortschritt (für die Schritte der Untergrenze): HiGHS rechnet mindestens <paramref name="Mindestens"/> und
/// danach weiter, solange es in den letzten <paramref name="Stillstand"/> einen besseren Plan gefunden oder die Untergrenze
/// angehoben hat – höchstens <paramref name="Hoechstens"/>. Hat es noch gar keinen Plan gefunden, sucht es bis zur Höchstzeit
/// weiter. Ist der Schritt bewiesen, endet er sofort.
/// </summary>
/// <param name="Mindestens">So lange rechnet HiGHS auf jeden Fall (sofern nicht vorher bewiesen).</param>
/// <param name="Hoechstens">So lange rechnet HiGHS höchstens.</param>
/// <param name="Stillstand">Ohne Fortschritt so lange, und HiGHS hört auf (nach <paramref name="Mindestens"/>).</param>
public sealed record Laufzeitregel(TimeSpan Mindestens, TimeSpan Hoechstens, TimeSpan Stillstand);
