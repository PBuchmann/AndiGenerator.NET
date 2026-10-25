// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Import;

/// <summary>Einstellungen eines Laufs von HiGHS für Vergleiche (siehe <see cref="HighsLoeser.LoesenMitSchranke"/>).</summary>
/// <param name="Startwert">Startwert der Zufallszahlen von HiGHS (verschiedene Läufe suchen verschieden).</param>
/// <param name="Heuristik">Aufwand der Heuristiken (<c>mip_heuristic_effort</c>, HiGHS-Standard 0,05), oder <c>null</c>.</param>
/// <param name="Zwischenstand">
/// Bekommt während der Suche Laufzeit (s), Wert der besten Lösung und untere Schranke (aus dem Rückruf, sehr oft), oder
/// <c>null</c>.
/// </param>
/// <param name="Loesung">Bekommt jede bessere Lösung schon während der Suche (Werte der Spalten), oder <c>null</c>.</param>
/// <param name="OhneNeustart">
/// Kein Neustart der Suche mit neuer Vorverarbeitung (<c>mip_allow_restart</c>): Darin reagierte HiGHS in langen Läufen
/// minutenlang weder auf die Zeitgrenze noch auf den Abbruch (Verbandsoberliga: 5 statt 92 Minuten).
/// </param>
public sealed record Highsvorgabe(int Startwert = 0, double? Heuristik = null, Action<double, double, double>? Zwischenstand = null, Action<double[]>? Loesung = null, bool OhneNeustart = false);
