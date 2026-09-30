// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>Seite „60km Regel“ (Original <c>TDialogPanel60kmDetail</c>).</summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>
    /// Liest die Gegner, gegen die eine Mannschaft nur am Wochenende spielen soll (Original <c>TeamListFromData</c>);
    /// Namen ohne Mannschaft in der Staffel entfallen.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Die Gegner in der Reihenfolge der Daten.</returns>
    public IReadOnlyList<string> NurWochenendeGegen(string mannschaft) => NurWochenendeAus(Arbeitsstand, mannschaft);

    /// <summary>Liest die Gegner des click-TT-Stands.</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Die Gegner oder <c>null</c> ohne Standard.</returns>
    public IReadOnlyList<string>? NurWochenendeStandard(string mannschaft) =>
        standard is null ? null : NurWochenendeAus(standard, mannschaft);

    /// <summary>Schreibt die Gegner (Original <c>FormToData</c>): alle bisherigen Einträge werden ersetzt.</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="gegner">Die Gegner.</param>
    public void NurWochenendeSpeichern(string mannschaft, IEnumerable<string> gegner)
    {
        ArgumentNullException.ThrowIfNull(gegner);
        List<string> liste = gegner.ToList();
        foreach (DatenKnoten team in Teams(Arbeitsstand).Where(t => t.Lesen("teamname") == mannschaft))
        {
            team.KinderEntfernen("noweekgames");
            foreach (string name in liste)
            {
                new DatenKnoten("noweekgames", team).Setzen("teamname", name);
            }
        }
    }

    private static List<string> NurWochenendeAus(DatenKnoten plan, string mannschaft)
    {
        HashSet<string> vorhanden = Teams(plan).Select(t => t.Lesen("teamname")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Teams(plan)
            .Where(t => t.Lesen("teamname") == mannschaft)
            .SelectMany(t => t.KinderMitNamen("noweekgames"))
            .Select(k => k.Lesen("teamname"))
            .Where(vorhanden.Contains)
            .ToList();
    }
}
