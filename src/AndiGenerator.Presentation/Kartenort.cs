// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Geo;

namespace AndiGenerator.Presentation;

/// <summary>Ein Spielort für die Karte.</summary>
/// <param name="Name">Name der Mannschaft (für den Hinweis).</param>
/// <param name="Kurzname">Beschriftung auf der Karte.</param>
/// <param name="Lage">Lage der Halle.</param>
/// <param name="Hervorgehoben">Ausrichter in der Auswahl (größer, grün).</param>
/// <param name="Gedimmt">Nicht Teil der Auswahl (blass).</param>
/// <param name="Hinweis">Text für den Tooltip, z. B. Halle und Ort.</param>
public sealed record Kartenort(string Name, string Kurzname, Koordinate Lage, bool Hervorgehoben, bool Gedimmt, string Hinweis);
