// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Gemeinsam;

namespace AndiGenerator.Persistence.Plandaten;

/// <summary>Fachlicher Schlüssel eines Knotens: Name plus Werte der Schlüsselattribute.</summary>
public sealed record KnotenSchluessel(string Name, IReadOnlyList<KeyValuePair<string, string>> Werte)
{
    private static readonly Dictionary<string, string[]> Definitionen = new(StringComparer.Ordinal)
    {
        ["plan"] = [],
        ["team"] = ["teamname"],
        ["homegameday"] = ["datetime"],
        ["nogameday"] = ["date"],
        ["roadcouple"] = ["teamnamea", "teamnameb"],
        ["noweekgames"] = ["teamname"],
        ["homerights"] = ["teamname"],
        ["sisterteam"] = ["teamname", "gender"],
        ["sistergame"] = ["datetime"],
        ["predefinedgames"] = [],
        ["game"] = ["datetime", "hometeamname", "guestteamname"],
        ["existingschedule"] = [],
        ["schedule"] = [],
        ["mandatorygames"] = ["datefrom", "dateto"],
        ["ranking"] = [],
    };

    /// <summary>Ob für den Knotennamen ein Schlüssel definiert ist (Original: sonst Exception „Kein Key für den Typ“).</summary>
    /// <param name="name">Knotenname.</param>
    /// <returns><c>true</c>, wenn der Knotentyp bekannt ist.</returns>
    public static bool IstBekannt(string name) => Definitionen.ContainsKey(name);

    /// <summary>Schlüsselattribute eines Knotentyps (Original <c>getKeysForDataType</c>).</summary>
    /// <param name="name">Knotenname.</param>
    /// <returns>Die Namen der Schlüsselattribute (leer bei Knoten ohne Schlüssel).</returns>
    /// <exception cref="PlanDatenFormatException">Der Knotentyp ist unbekannt.</exception>
    public static IReadOnlyList<string> Attribute(string name) =>
        Definitionen.TryGetValue(name, out string[]? attribute)
            ? attribute
            : throw new PlanDatenFormatException($"Kein Schlüssel für den Knotentyp '{name}' definiert.");

    /// <summary>Erzeugt einen Schlüssel aus Name und Attributwerten (in der Reihenfolge der Definition).</summary>
    /// <param name="name">Knotenname.</param>
    /// <param name="werte">Werte der Schlüsselattribute; fehlende gelten als leer.</param>
    /// <returns>Der Schlüssel.</returns>
    public static KnotenSchluessel Von(string name, params string[] werte)
    {
        IReadOnlyList<string> attribute = Attribute(name);
        return new KnotenSchluessel(name, attribute.Select((a, i) => new KeyValuePair<string, string>(a, i < werte.Length ? DelphiKompatibel.Trim(werte[i]) : string.Empty)).ToList());
    }
}
