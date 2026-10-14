// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Löst ein <see cref="Ganzzahlmodell"/> (nicht im Original). Der Rechenkern bleibt frei von Drittbibliotheken; die
/// Anwendung reicht den Löser HiGHS herein (<see cref="Kompaktplanoptionen.Loeser"/>).
/// </summary>
public interface IGanzzahlloeser
{
    /// <summary>Löst das Modell.</summary>
    /// <param name="modell">Das Modell.</param>
    /// <param name="start">Eine bekannte Lösung als Startpunkt (Werte der Spalten), oder <c>null</c>.</param>
    /// <param name="startwert">Startwert der Zufallszahlen des Lösers (parallele Läufe mit verschiedenen Werten suchen verschieden).</param>
    /// <param name="aufhoeren">
    /// Wird während der Suche gefragt; <c>true</c> beendet sie mit der bis dahin besten Lösung. Darf blockieren (Pause).
    /// </param>
    /// <param name="zeitgrenze">Harte Zeitgrenze, die auch ohne Rückfrage gilt, oder <c>null</c>.</param>
    /// <returns>
    /// Die Werte der Spalten der besten gefundenen Lösung, oder <c>null</c>, wenn keine gefunden wurde; und ob sie beweisbar
    /// die beste ist.
    /// </returns>
    (double[]? Werte, bool Optimal) Loesen(Ganzzahlmodell modell, double[]? start, int startwert, Func<bool> aufhoeren, TimeSpan? zeitgrenze = null);
}
