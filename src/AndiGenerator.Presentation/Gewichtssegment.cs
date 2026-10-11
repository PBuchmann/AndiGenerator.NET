// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>
/// Eine Stufe der Gewichtsskala: Ring = Gewichtung des Anwenders, Punkt = aktuelle Gewichtung des Automodus, dazwischen
/// eine Linie (je halbe Stufe links und rechts der Mitte).
/// </summary>
/// <param name="Vorher">Hier steht die Gewichtung des Anwenders.</param>
/// <param name="Jetzt">Hier steht die aktuelle Gewichtung des Automodus.</param>
/// <param name="LinksWeg">Die Linie von „vorher“ nach „jetzt“ läuft durch die linke Hälfte.</param>
/// <param name="RechtsWeg">Die Linie von „vorher“ nach „jetzt“ läuft durch die rechte Hälfte.</param>
public sealed record Gewichtssegment(bool Vorher, bool Jetzt, bool LinksWeg, bool RechtsWeg);
