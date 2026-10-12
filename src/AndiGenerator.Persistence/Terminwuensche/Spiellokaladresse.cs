// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.RegularExpressions;

namespace AndiGenerator.Persistence.Terminwuensche;

/// <summary>Spiellokal eines Vereins aus dem Bericht „Terminwünsche“, z. B. „Haybachhalle, Am Bandweidenweg, 55270 Klein-Winternheim“.</summary>
/// <param name="Name">Name der Halle wie in click-TT (so steht er auch an den Heimterminen der XML-Datei).</param>
/// <param name="Strasse">Straße, leer wenn nicht angegeben.</param>
/// <param name="Postleitzahl">Postleitzahl, leer wenn nicht erkennbar.</param>
/// <param name="Ort">Ort.</param>
public sealed partial record Spiellokaladresse(string Name, string Strasse, string Postleitzahl, string Ort)
{
    /// <summary>Holt die Adresse als eine Zeile.</summary>
    public string Text => string.Join(", ", new[] { Name, Strasse, (Postleitzahl + " " + Ort).Trim() }.Where(t => t.Length > 0));

    /// <summary>Zerlegt eine Adresszeile: Name, Straße, „PLZ Ort“ durch Kommas getrennt.</summary>
    /// <param name="zeile">Die Zeile.</param>
    /// <returns>Die Adresse.</returns>
    public static Spiellokaladresse Aus(string zeile)
    {
        ArgumentNullException.ThrowIfNull(zeile);
        List<string> teile = zeile.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        if (teile.Count == 0)
        {
            return new Spiellokaladresse(string.Empty, string.Empty, string.Empty, string.Empty);
        }

        Match ort = PlzOrt().Match(teile[^1]);
        if (teile.Count == 1 || !ort.Success)
        {
            return new Spiellokaladresse(teile[0], string.Join(", ", teile.Skip(1)), string.Empty, string.Empty);
        }

        return new Spiellokaladresse(teile[0], string.Join(", ", teile.Skip(1).Take(teile.Count - 2)), ort.Groups[1].Value, ort.Groups[2].Value);
    }

    [GeneratedRegex(@"^(\d{5})\s+(.+)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PlzOrt();
}
