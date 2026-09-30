// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>
/// Tabelle mit gemessenen Spaltenbreiten. Sie bricht zeilenweise über Seiten um und wiederholt dann ihren Kopf; ist sie
/// zu breit, wird sie als Ganzes verkleinert. Die letzte Spalte kann stattdessen umbrechen (lange Hinweise).
/// </summary>
/// <param name="Kopf">Spaltenköpfe oder <c>null</c>.</param>
/// <param name="Zeilen">Die Zeilen.</param>
/// <param name="Groesse">Schriftgröße in Punkt.</param>
/// <param name="LetzteSpalteUmbrechen">Letzte Spalte nimmt den Rest der Breite und bricht um, statt die Tabelle zu verkleinern.</param>
/// <param name="AbstandDavor">Freiraum über der Tabelle in Punkt.</param>
/// <param name="Matrix">
/// Darstellung wie die Kostenmatrix der Oberfläche: Spaltenköpfe ab der zweiten Spalte senkrecht, Werte zentriert, feine
/// Linien zwischen den Zeilen und eine Linie über fetten Zeilen (Summe).
/// </param>
public sealed record Tabelle(IReadOnlyList<string>? Kopf, IReadOnlyList<Tabellenzeile> Zeilen, double Groesse, bool LetzteSpalteUmbrechen = false, double AbstandDavor = 0, bool Matrix = false) : Baustein(AbstandDavor);
