// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>
/// Kopfzeile einer Stufe in der Qualitätsansicht (Automodus): die Verstöße des Plans in der Stufe zusammen, die Grenze aus
/// HiGHS (so wenige hat jeder Plan mindestens) und in Klartext, wie viel besser der Plan dort noch werden kann.
/// </summary>
/// <param name="Titel">Z. B. <c>Stufe A · muss</c>.</param>
/// <param name="Plan">Verstöße des Plans in der Stufe, z. B. <c>Plan 10</c>.</param>
/// <param name="Grenze">Z. B. <c>Grenze ≥ 10</c>; leer, wenn es keine gibt.</param>
/// <param name="Spielraum">Z. B. <c>Optimum erreicht – besser geht es nicht</c> oder <c>Optimum zwischen 11 und 45: höchstens 34 Verstöße weniger möglich</c>.</param>
/// <param name="Zustand">Für die Farbe des Spielraums.</param>
/// <param name="Unvermeidbar">
/// Anteil der Verstöße des Plans, die jeder Plan hat (Grenze ÷ Plan, 0 bis 100) für die Leiste; <c>null</c> = keine Leiste.
/// </param>
/// <param name="Knopf">Stufe B: Knopftext, z. B. <c>B-Varianten wählen (4) …</c> oder <c>Festlegung aufheben</c>.</param>
/// <param name="KnopfBefehl">Der Befehl dazu; <c>null</c> = kein Knopf.</param>
public sealed record Stufenkopf(
    string Titel,
    string Plan,
    string Grenze,
    string Spielraum,
    Spielraumzustand Zustand,
    double? Unvermeidbar = null,
    string Knopf = "",
    CommunityToolkit.Mvvm.Input.IRelayCommand? KnopfBefehl = null)
{
    /// <summary>Holt einen Wert, der angibt, ob der Knopf gezeigt wird.</summary>
    public bool HatKnopf => KnopfBefehl is not null;

    /// <summary>Holt einen Wert, der angibt, ob die Leiste (unvermeidbar / noch möglich) gezeigt wird.</summary>
    public bool HatLeiste => Unvermeidbar is not null;

    /// <summary>Holt einen Wert, der angibt, ob der Plan in der Stufe bestmöglich ist (grün).</summary>
    public bool IstBestmoeglich => Zustand == Spielraumzustand.Bestmoeglich;

    /// <summary>Holt einen Wert, der angibt, ob der Plan in der Stufe noch besser werden kann (orange).</summary>
    public bool IstOffen => Zustand == Spielraumzustand.Offen;
}
