// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Presentation;

/// <summary>Zeile der Terminwunschansicht.</summary>
/// <param name="Text">Der Text.</param>
/// <param name="Farbe">Hervorhebung wie im Original.</param>
/// <param name="Ebene">0 = Mannschaft, 1 = Zwischenüberschrift, 2 = Eintrag.</param>
public sealed record Terminwunschzeile(string Text, Terminwunschfarbe Farbe, int Ebene)
{
    /// <summary>Bildet die Zeilen der Terminwunschansicht: Hinweise, dann je Mannschaft ihre Wünsche und die Auswertung.</summary>
    /// <param name="uebersicht">Die Übersicht oder <c>null</c>.</param>
    /// <returns>Die Zeilen.</returns>
    internal static List<Terminwunschzeile> Bilden(Terminwunschuebersicht? uebersicht)
    {
        var liste = new List<Terminwunschzeile>();
        if (uebersicht is null)
        {
            return liste;
        }

        liste.AddRange(uebersicht.Hinweise.Select(h => new Terminwunschzeile(h, Terminwunschfarbe.Normal, 2)));
        foreach (MannschaftsTerminwuensche m in uebersicht.Mannschaften)
        {
            liste.Add(new Terminwunschzeile(m.Name + ":", Terminwunschfarbe.Normal, 0));
            liste.AddRange(m.Zeilen.Select(z => new Terminwunschzeile(z.Text, z.Farbe, z.Ueberschrift ? 1 : 2)));
        }

        return liste;
    }
}
