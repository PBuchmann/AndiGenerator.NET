// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.IO.Compression;
using AndiGenerator.Domain.Geo;

namespace AndiGenerator.Persistence.Geo;

/// <summary>
/// Lage deutscher Postleitzahlen (je Postleitzahl und Ort) aus GeoNames (www.geonames.org, CC BY 4.0), eingebettet als
/// <c>plz-de.csv.gz</c>. Ohne Netz und ohne Annahme über eine Region innerhalb Deutschlands.
/// </summary>
public static class Postleitzahlen
{
    private static readonly Lazy<Dictionary<string, List<(string Ort, Koordinate Lage)>>> Tabelle = new(Laden);

    /// <summary>Lage zu Postleitzahl und Ort; gibt es zur Postleitzahl mehrere Orte, zählt der passende, sonst ihr Mittel.</summary>
    /// <param name="postleitzahl">Fünfstellige Postleitzahl.</param>
    /// <param name="ort">Ort, z. B. „Neuwied-Engers“ (darf leer sein).</param>
    /// <returns>Die Lage oder <c>null</c>, wenn die Postleitzahl unbekannt ist.</returns>
    public static Koordinate? Lage(string postleitzahl, string ort)
    {
        ArgumentNullException.ThrowIfNull(postleitzahl);
        ArgumentNullException.ThrowIfNull(ort);
        if (!Tabelle.Value.TryGetValue(postleitzahl.Trim(), out List<(string Ort, Koordinate Lage)>? orte))
        {
            return null;
        }

        string gesucht = Kern(ort);
        List<(string Ort, Koordinate Lage)> passend = gesucht.Length == 0
            ? []
            : orte.Where(o => o.Ort.Length > 0 && (gesucht.Contains(o.Ort, StringComparison.Ordinal) || o.Ort.Contains(gesucht, StringComparison.Ordinal))).ToList();
        List<(string Ort, Koordinate Lage)> auswahl = passend.Count > 0 ? passend : orte;
        return new Koordinate(auswahl.Average(o => o.Lage.Breite), auswahl.Average(o => o.Lage.Laenge));
    }

    private static Dictionary<string, List<(string Ort, Koordinate Lage)>> Laden()
    {
        var tabelle = new Dictionary<string, List<(string Ort, Koordinate Lage)>>(StringComparer.Ordinal);
        using Stream quelle = typeof(Postleitzahlen).Assembly.GetManifestResourceStream("plz-de.csv.gz")
            ?? throw new InvalidOperationException("Die Postleitzahlen-Tabelle fehlt im Programm.");
        using var entpackt = new GZipStream(quelle, CompressionMode.Decompress);
        using var leser = new StreamReader(entpackt, System.Text.Encoding.UTF8);
        while (leser.ReadLine() is string zeile)
        {
            string[] teile = zeile.Split(';');
            if (zeile.StartsWith('#') || teile.Length != 4)
            {
                continue;
            }

            if (!tabelle.TryGetValue(teile[0], out List<(string Ort, Koordinate Lage)>? orte))
            {
                orte = [];
                tabelle.Add(teile[0], orte);
            }

            orte.Add((Kern(teile[1]), new Koordinate(
                double.Parse(teile[2], CultureInfo.InvariantCulture),
                double.Parse(teile[3], CultureInfo.InvariantCulture))));
        }

        return tabelle;
    }

    /// <summary>Nur Buchstaben und Ziffern, klein: „Mülheim - Kärlich“ und „Mülheim-Kärlich“ sind gleich.</summary>
    private static string Kern(string text) => string.Concat(text.Where(char.IsLetterOrDigit)).ToLowerInvariant();
}
