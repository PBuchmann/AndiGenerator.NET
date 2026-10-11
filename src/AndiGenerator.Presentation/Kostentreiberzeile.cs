// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein großer Verursacher einer Insel: Name, Balken und Anteil an den Kosten bzw. Zahl der Verstöße (Automodus).</summary>
/// <param name="Name">Name des Kriteriums.</param>
/// <param name="Breite">Breite des Balkens in Pixeln (voller Anteil = volle Breite).</param>
/// <param name="Prozent">Anteil an den Kosten, z. B. „43 %“, im Automodus die Zahl der Verstöße.</param>
public sealed record Kostentreiberzeile(string Name, double Breite, string Prozent);
