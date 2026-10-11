// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Was eine Verbesserung des besten Plans geändert hat, zum grünen Punkt, der sie zum besten Plan trägt.</summary>
/// <param name="Titel">Überschrift mit der Nummer wie in der Kachel „Verbesserungen“, z. B. „Verbesserung 57“.</param>
/// <param name="Aenderungen">Die Änderungen, größte bzw. wichtigste zuerst.</param>
/// <param name="Zeitpunkt">Zeitstempel der Verbesserung (<see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>), zugleich Start des Punkts.</param>
public sealed record Verbesserungsmeldung(string Titel, IReadOnlyList<Planaenderung> Aenderungen, long Zeitpunkt);
