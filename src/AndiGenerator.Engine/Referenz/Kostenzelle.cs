// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Kostenwert einer Mannschaft für eine Kostenart (Original <c>TKostenValue</c>).</summary>
/// <param name="Anzahl">Anzahl der Verstöße; −1 = keine Anzahl (Spielverteilung).</param>
/// <param name="Gesamtzahl">Bezugsgröße („von“), −1 = keine.</param>
/// <param name="Kosten">Kosten.</param>
public sealed record Kostenzelle(int Anzahl, int Gesamtzahl, double Kosten)
{
    /// <summary>Text der Zelle wie in der Kostentabelle des Originals.</summary>
    /// <returns>Z. B. <c>5 /178 (35.738)</c>, <c>0 (0)</c> oder <c>1.058</c>.</returns>
    public string Anzeige()
    {
        if (Anzahl < 0)
        {
            return Kostenanzeige.Kurz(Kosten);
        }

        return Gesamtzahl >= 0 ? $"{Anzahl} /{Gesamtzahl} ({Kostenanzeige.Kurz(Kosten)})" : $"{Anzahl} ({Kostenanzeige.Kurz(Kosten)})";
    }
}
