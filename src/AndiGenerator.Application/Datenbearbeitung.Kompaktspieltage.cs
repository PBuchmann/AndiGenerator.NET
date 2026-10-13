// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Geo;

namespace AndiGenerator.Application;

/// <summary>Kompaktspieltage (nicht im Original): Schalter der Staffel, Bemerkungen und Lage der Hallen.</summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>Attribut am Knoten <c>plan</c>: <c>ein</c>, <c>bestaetigt</c> oder <c>aus</c> (fehlt = noch nicht gefragt).</summary>
    private const string KompaktAttribut = "kompaktspieltage";

    /// <summary>Holt, ob die Staffel mit Kompaktspieltagen geplant wird.</summary>
    public Kompaktmodus Kompaktspieltage => Arbeitsstand.Lesen(KompaktAttribut) switch
    {
        "ein" => Kompaktmodus.Ein,
        "bestaetigt" => Kompaktmodus.Bestaetigt,
        "aus" => Kompaktmodus.Aus,
        _ => Kompaktmodus.Offen,
    };

    /// <summary>Setzt, ob die Staffel mit Kompaktspieltagen geplant wird.</summary>
    /// <param name="modus">Der neue Zustand.</param>
    public void KompaktspieltageSetzen(Kompaktmodus modus)
    {
        string wert = modus switch
        {
            Kompaktmodus.Ein => "ein",
            Kompaktmodus.Bestaetigt => "bestaetigt",
            Kompaktmodus.Aus => "aus",
            _ => string.Empty,
        };
        Arbeitsstand.Setzen(KompaktAttribut, wert);
    }

    /// <summary>Liest die Bemerkungen der Vereine aus dem übernommenen Bericht „Terminwünsche“.</summary>
    /// <returns>Mannschaftsname → Bemerkung; Mannschaften ohne Bemerkung fehlen.</returns>
    public IReadOnlyDictionary<string, string> Bemerkungen() =>
        Teams(Arbeitsstand)
            .Select(t => (Name: t.Lesen("teamname"), Text: t.Lesen(BemerkungAttribut)))
            .Where(x => x.Text.Length > 0)
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Text, StringComparer.Ordinal);

    /// <summary>Liest die Lage der Hallen aus den gespeicherten Adressen (Postleitzahl bzw. GPS-Angabe).</summary>
    /// <returns>Mannschaftsname → Lage; Mannschaften ohne bekannte Halle fehlen.</returns>
    public IReadOnlyDictionary<string, Koordinate> Hallenkoordinaten() => HallenLagen([]);

    /// <summary>
    /// Liest die Adresse der Halle jeder Mannschaft, wie sie im Bericht „Terminwünsche“ steht (Name, Straße, PLZ und Ort),
    /// z. B. zum Eintragen beim Verlegen in click-TT.
    /// </summary>
    /// <returns>Mannschaftsname → Adresse; Mannschaften ohne gespeicherte Adresse fehlen.</returns>
    public IReadOnlyDictionary<string, string> Hallenadressen() =>
        Teams(Arbeitsstand)
            .Select(t => (Name: t.Lesen("teamname"), Text: GespeicherteAdresse(t, Hallennummer(t))?.Text ?? string.Empty))
            .Where(x => x.Text.Length > 0)
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Text, StringComparer.Ordinal);
}
