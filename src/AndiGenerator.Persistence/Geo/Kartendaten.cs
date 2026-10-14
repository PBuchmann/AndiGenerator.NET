// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text.Json;
using AndiGenerator.Domain.Geo;

namespace AndiGenerator.Persistence.Geo;

/// <summary>
/// Eingebettete Kartendaten (nicht im Original): Umrisse der Bundesländer aus Natural Earth (gemeinfrei, vereinfacht mit
/// <c>tools/karte/umrisse.py</c>) und einige Städte zur Orientierung (Lage aus der Postleitzahlen-Tabelle).
/// </summary>
public static class Kartendaten
{
    private static readonly Lazy<IReadOnlyList<Landesumriss>> UmrisseDaten = new(UmrisseLaden);
    private static readonly Lazy<IReadOnlyList<Stadt>> StaedteDaten = new(StaedteLaden);

    /// <summary>Holt die 16 Bundesländer.</summary>
    public static IReadOnlyList<Landesumriss> Laender => UmrisseDaten.Value;

    /// <summary>Holt die Städte, die wichtigsten zuerst.</summary>
    public static IReadOnlyList<Stadt> Staedte => StaedteDaten.Value;

    private static IReadOnlyList<Landesumriss> UmrisseLaden()
    {
        using JsonDocument dokument = Dokument("umrisse-de.json");
        var laender = new List<Landesumriss>();
        foreach (JsonElement land in dokument.RootElement.GetProperty("laender").EnumerateArray())
        {
            var ringe = new List<IReadOnlyList<Koordinate>>();
            foreach (JsonElement ring in land.GetProperty("ringe").EnumerateArray())
            {
                // Je Punkt [Länge, Breite] wie in GeoJSON.
                ringe.Add(ring.EnumerateArray().Select(p => new Koordinate(p[1].GetDouble(), p[0].GetDouble())).ToList());
            }

            laender.Add(new Landesumriss(land.GetProperty("name").GetString() ?? string.Empty, ringe));
        }

        return laender;
    }

    private static IReadOnlyList<Stadt> StaedteLaden()
    {
        using JsonDocument dokument = Dokument("staedte-de.json");
        return dokument.RootElement.EnumerateArray()
            .Select(s => new Stadt(
                s.GetProperty("name").GetString() ?? string.Empty,
                s.GetProperty("rang").GetInt32(),
                new Koordinate(s.GetProperty("lat").GetDouble(), s.GetProperty("lon").GetDouble())))
            .OrderBy(s => s.Rang)
            .ThenBy(s => s.Name, StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), ignoreCase: false))
            .ToList();
    }

    private static JsonDocument Dokument(string name)
    {
        using Stream quelle = typeof(Kartendaten).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Die Kartendaten „{name}“ fehlen im Programm.");
        return JsonDocument.Parse(quelle);
    }
}
