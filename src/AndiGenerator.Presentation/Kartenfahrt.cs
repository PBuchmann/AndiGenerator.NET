// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eine Fahrt zu einem Kompaktspieltag im Kartenbild.</summary>
/// <param name="Von">Halle des Gastes.</param>
/// <param name="Nach">Halle des Ausrichters.</param>
/// <param name="Kraeftig">Teil der Auswahl (sonst blass, wenn alle Fahrten gezeigt werden).</param>
public sealed record Kartenfahrt(Kartenpunkt Von, Kartenpunkt Nach, bool Kraeftig);
