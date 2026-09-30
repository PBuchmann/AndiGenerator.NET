// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Spiel im Mannschaftsplan.</summary>
/// <param name="Spiel">Das Spiel.</param>
/// <param name="Heimspiel">Die Mannschaft spielt zu Hause.</param>
/// <param name="Gegner">Der Gegner.</param>
/// <param name="Nachbarn">Spiele der Vereinsmannschaften am selben Tag (nur mit Nachbarmannschaften).</param>
/// <param name="NeueRunde">Mit diesem Spiel beginnt eine neue Runde (Abstand davor).</param>
public sealed record Mannschaftsspiel(Terminspiel Spiel, bool Heimspiel, string Gegner, IReadOnlyList<Nachbarspiel> Nachbarn, bool NeueRunde)
{
    /// <summary>Holt „H“ oder „A“.</summary>
    public string Marke => Heimspiel ? "H" : "A";

    /// <summary>Holt einen Wert, der angibt, ob Nachbarspiele gezeigt werden.</summary>
    public bool HatNachbarn => Nachbarn.Count > 0;
}
