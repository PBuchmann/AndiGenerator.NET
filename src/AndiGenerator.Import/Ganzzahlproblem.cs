// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Import;

/// <summary>
/// Ein ganzzahliges lineares Programm für <see cref="HighsLoeser"/> (nicht im Original): Ja/Nein-Spalten mit Kosten und
/// Zeilen als Summen zwischen Grenzen, zeilenweise gespeichert; gesucht ist die Wahl mit den kleinsten Kosten.
/// </summary>
/// <param name="Kosten">Kosten je Spalte.</param>
/// <param name="SpalteOben">Obere Grenze je Spalte (0 oder 1; die untere ist 0).</param>
/// <param name="ZeileUnten">Untere Grenze je Zeile.</param>
/// <param name="ZeileOben">Obere Grenze je Zeile (<see cref="double.PositiveInfinity"/> = keine).</param>
/// <param name="ZeilenAnfang">Wo die Einträge jeder Zeile beginnen (ein Eintrag mehr als Zeilen).</param>
/// <param name="Spalten">Spalte je Eintrag.</param>
/// <param name="Werte">Faktor je Eintrag.</param>
public sealed record Ganzzahlproblem(double[] Kosten, double[] SpalteOben, double[] ZeileUnten, double[] ZeileOben, int[] ZeilenAnfang, int[] Spalten, double[] Werte);
