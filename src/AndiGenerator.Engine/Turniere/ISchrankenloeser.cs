// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Löst ein <see cref="Ganzzahlmodell"/> und meldet neben der besten Lösung die beweisbare untere Schranke des Lösers (nicht
/// im Original; für <see cref="Schranke.Ligaschranke"/>).
/// </summary>
public interface ISchrankenloeser
{
    /// <summary>Löst das Modell bis zur Zeitgrenze.</summary>
    /// <param name="modell">Das Modell.</param>
    /// <param name="zeitgrenze">Zeitgrenze.</param>
    /// <param name="start">Eine bekannte Lösung als Startpunkt, oder <c>null</c>.</param>
    /// <param name="aufhoeren">
    /// Wird während der Suche gefragt; <c>true</c> beendet sie mit der bis dahin besten Lösung (Abbruch, Pause), oder <c>null</c>.
    /// </param>
    /// <param name="regel">
    /// Zeitgrenze nach Fortschritt statt der festen <paramref name="zeitgrenze"/>, oder <c>null</c>.
    /// </param>
    /// <param name="zwischenloesung">Bekommt jede bessere Lösung schon während der Suche (Werte der Spalten), oder <c>null</c>.</param>
    /// <returns>Das Ergebnis.</returns>
    Schrankenloesung Loesen(Ganzzahlmodell modell, TimeSpan zeitgrenze, double[]? start = null, Func<bool>? aufhoeren = null, Laufzeitregel? regel = null, Action<double[]>? zwischenloesung = null);
}
