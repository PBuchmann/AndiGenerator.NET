// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Geo;

namespace AndiGenerator.Presentation;

/// <summary>Ein Spielort für die Karte.</summary>
/// <param name="Name">Schlüssel des Spielorts (Name der Mannschaft), auf den sich die Fahrten beziehen.</param>
/// <param name="Beschriftung">Beschriftung auf der Karte (voller Mannschaftsname).</param>
/// <param name="Lage">Lage der Halle.</param>
/// <param name="Hervorgehoben">Ausrichter in der Auswahl (größer, grün).</param>
/// <param name="Gedimmt">Nicht Teil der Auswahl (blass).</param>
/// <param name="Hinweis">Text für den Tooltip, z. B. Halle und Ort.</param>
public sealed record Kartenort(string Name, string Beschriftung, Koordinate Lage, bool Hervorgehoben, bool Gedimmt, string Hinweis);
