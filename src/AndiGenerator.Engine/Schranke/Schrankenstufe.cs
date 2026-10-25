// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Engine.Schranke;

/// <summary>Ergebnis einer Stufe der unteren Schranke (<see cref="Ligaschranke"/>).</summary>
/// <param name="Stufe">Stufe nach MIGRATIONSPLAN E14, z. B. <c>A2</c>.</param>
/// <param name="Name">Name des Kriteriums.</param>
/// <param name="Schranke">
/// So viele Verstöße hat jeder Plan mindestens: bei A1 und den Summen der Stufen A und B in der Kette (B bei bestmöglichem A),
/// bei einem Kriterium der Stufe A für das Kriterium allein, der Stufe B allein bei bestmöglichem A, bei C bei bestmöglichem A
/// und B.
/// </param>
/// <param name="Gefunden">Verstöße im besten Plan des vereinfachten Modells, oder <c>null</c>.</param>
/// <param name="Bewiesen">Die Schranke ist bewiesen (in der Kette: Schranke = Gefunden; ein Kriterium: die Summe seiner Stufe).</param>
/// <param name="Sekunden">Rechenzeit der Stufe.</param>
/// <param name="Kriterium">Das Kriterium; <c>null</c> bei den Spielen ohne Termin (harte Fehler).</param>
/// <param name="Einzeln">
/// So viele Verstöße hat jeder Plan ohne Spiele ohne Termin mindestens, unabhängig von der Rangfolge (das Kriterium allein
/// minimiert); höchstens <paramref name="Schranke"/>.
/// </param>
public sealed record Schrankenstufe(string Stufe, string Name, int Schranke, int? Gefunden, bool Bewiesen, double Sekunden, Kostenkriterium? Kriterium = null, int Einzeln = 0);
