// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text.RegularExpressions;
using AndiGenerator.Domain.Geo;

namespace AndiGenerator.Persistence.Terminwuensche;

/// <summary>Spiellokal eines Vereins aus dem Bericht „Terminwünsche“, z. B. „Haybachhalle, Am Bandweidenweg, 55270 Klein-Winternheim“.</summary>
/// <param name="Name">Name der Halle wie in click-TT (so steht er auch an den Heimterminen der XML-Datei).</param>
/// <param name="Strasse">Straße, leer wenn nicht angegeben.</param>
/// <param name="Postleitzahl">Postleitzahl, leer wenn nicht erkennbar.</param>
/// <param name="Ort">Ort.</param>
public sealed partial record Spiellokaladresse(string Name, string Strasse, string Postleitzahl, string Ort)
{
    /// <summary>
    /// Holt die GPS-Lage, wenn der Verein sie zur Adresse angegeben hat (z. B. „GPS:50.63599°N 7.35227 °E“), sonst
    /// <c>null</c>. Sie ist genauer als die Lage aus der Postleitzahl.
    /// </summary>
    public Koordinate? Lage { get; init; }

    /// <summary>Holt die Adresse als eine Zeile (mit GPS-Lage, falls bekannt).</summary>
    public string Text
    {
        get
        {
            List<string> teile = new[] { Name, Strasse, (Postleitzahl + " " + Ort).Trim() }.Where(t => t.Length > 0).ToList();
            if (Lage is Koordinate k)
            {
                teile.Add(string.Create(CultureInfo.InvariantCulture, $"GPS:{k.Breite:0.#####}°N {k.Laenge:0.#####}°E"));
            }

            return string.Join(", ", teile);
        }
    }

    /// <summary>
    /// Zerlegt eine Adresszeile: Name, Straße, „PLZ Ort“ durch Kommas getrennt. Angaben nach „PLZ Ort“ (etwa „Fax:“)
    /// entfallen; eine GPS-Angabe wird als <see cref="Lage"/> gelesen.
    /// </summary>
    /// <param name="zeile">Die Zeile.</param>
    /// <returns>Die Adresse.</returns>
    public static Spiellokaladresse Aus(string zeile)
    {
        ArgumentNullException.ThrowIfNull(zeile);
        Koordinate? lage = null;
        string rest = zeile;
        Match gps = GpsAngabe().Match(zeile);
        if (gps.Success)
        {
            lage = GpsLage(gps);
            rest = zeile.Remove(gps.Index, gps.Length);
        }

        List<string> teile = rest.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        if (teile.Count == 0)
        {
            return new Spiellokaladresse(string.Empty, string.Empty, string.Empty, string.Empty) { Lage = lage };
        }

        int stelle = teile.FindLastIndex(t => PlzOrt().IsMatch(t));
        if (stelle < 1)
        {
            return new Spiellokaladresse(teile[0], string.Join(", ", teile.Skip(1)), string.Empty, string.Empty) { Lage = lage };
        }

        Match ort = PlzOrt().Match(teile[stelle]);
        return new Spiellokaladresse(teile[0], string.Join(", ", teile.Skip(1).Take(stelle - 1)), ort.Groups[1].Value, ort.Groups[2].Value) { Lage = lage };
    }

    /// <summary>Breite und Länge der GPS-Angabe; nur nördliche Breite und östliche Länge in plausiblen Grenzen zählen.</summary>
    private static Koordinate? GpsLage(Match gps)
    {
        static double Zahl(Group g) => double.Parse(g.Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        double breite = Zahl(gps.Groups[1]);
        double laenge = Zahl(gps.Groups[2]);
        return breite is > 0 and < 90 && laenge is > 0 and < 180 ? new Koordinate(breite, laenge) : null;
    }

    [GeneratedRegex(@"^(\d{5})\s+(.+)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PlzOrt();

    [GeneratedRegex(@"GPS:\s*(\d{1,2}[.,]\d+)\s*°?\s*N\s*,?\s*(\d{1,3}[.,]\d+)\s*°?\s*[EO]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GpsAngabe();
}
