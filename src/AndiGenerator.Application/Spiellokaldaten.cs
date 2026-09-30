// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Spiellokale einer Mannschaft (Original <c>TDialogPanelLocationsCompactDetail</c>).</summary>
/// <param name="Standardlokal">Standardspiellokal der Mannschaft.</param>
/// <param name="Heimtage">Spiellokal je Heimspieltermin (Termin, Spiellokal), nach Termin sortiert.</param>
/// <param name="Nachbarn">Spiellokal je Nachbarmannschaft (Bezeichnung „(Art) Name“, Spiellokal), nach Bezeichnung sortiert.</param>
public sealed record Spiellokaldaten(
    string Standardlokal,
    IReadOnlyList<(DateTime Termin, string Spiellokal)> Heimtage,
    IReadOnlyList<(string Bezeichnung, string Spiellokal)> Nachbarn)
{
    /// <summary>
    /// Holt einen Wert, der angibt, ob die Werte dem Standard entsprechen (Original <c>isDefault</c>): kein Standardlokal,
    /// keine Lokale bei den Nachbarn und bei Heimspielen kein anderes als das Standardlokal.
    /// </summary>
    public bool IstStandard =>
        Standardlokal.Length == 0
        && Nachbarn.All(n => n.Spiellokal.Trim().Length == 0)
        && Heimtage.All(h => h.Spiellokal.Trim().Length == 0 || h.Spiellokal.Trim() == Standardlokal);
}
