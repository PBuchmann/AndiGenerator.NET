// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Geo;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Application;

/// <summary>Kompaktspieltage (nicht im Original): Schalter der Staffel, Bemerkungen und Lage der Hallen.</summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>Attribut am Knoten <c>plan</c>: <c>ein</c>, <c>bestaetigt</c> oder <c>aus</c> (fehlt = noch nicht gefragt).</summary>
    private const string KompaktAttribut = "kompaktspieltage";

    /// <summary>Attribut am Knoten <c>plan</c>: Fahrtwünsche „Gast|Gastgeber;…“.</summary>
    private const string FahrtenAttribut = "kompaktfahrten";

    /// <summary>Attribut am Knoten <c>plan</c>: festgehaltene Spieltage „JJJJ-MM-TT|Form|Mannschaft|…;…“ (Form 1, 3 oder 4).</summary>
    private const string FestAttribut = "kompaktfest";

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

    /// <summary>Liest die Fahrtwünsche für die Kompaktspieltage.</summary>
    /// <returns>Die Wünsche.</returns>
    public IReadOnlyList<Fahrtwunsch> Fahrtwuensche() =>
        Eintraege(Arbeitsstand.Lesen(FahrtenAttribut)).Where(e => e.Length == 2).Select(e => new Fahrtwunsch(e[0], e[1])).ToList();

    /// <summary>Setzt die Fahrtwünsche für die Kompaktspieltage.</summary>
    /// <param name="wuensche">Die Wünsche.</param>
    public void FahrtwuenscheSetzen(IEnumerable<Fahrtwunsch> wuensche)
    {
        ArgumentNullException.ThrowIfNull(wuensche);
        Arbeitsstand.Setzen(FahrtenAttribut, string.Join(';', wuensche.Select(w => w.Gast + "|" + w.Gastgeber)));
    }

    /// <summary>Liest die festgehaltenen Kompaktspieltage.</summary>
    /// <returns>Die Spieltage.</returns>
    public IReadOnlyList<Planturnier> Festgehalten() =>
        Eintraege(Arbeitsstand.Lesen(FestAttribut)).Select(Festes).OfType<Planturnier>().ToList();

    /// <summary>Setzt die festgehaltenen Kompaktspieltage.</summary>
    /// <param name="turniere">Die Spieltage.</param>
    public void FestgehaltenSetzen(IEnumerable<Planturnier> turniere)
    {
        ArgumentNullException.ThrowIfNull(turniere);
        Arbeitsstand.Setzen(FestAttribut, string.Join(';', turniere.Select(t => string.Join('|', t.Mannschaften.Prepend(Formzeichen(t.Form)).Prepend(t.Tag.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))))));
    }

    private static IEnumerable<string[]> Eintraege(string text) =>
        text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(e => e.Split('|'));

    private static string Formzeichen(Turnierform form) => form switch
    {
        Turnierform.Vierer => "4",
        Turnierform.Dreier => "3",
        _ => "1",
    };

    private static Planturnier? Festes(string[] teile)
    {
        if (teile.Length < 4 || !DateOnly.TryParseExact(teile[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly tag))
        {
            return null;
        }

        Turnierform form = teile[1] switch
        {
            "4" => Turnierform.Vierer,
            "3" => Turnierform.Dreier,
            "1" => Turnierform.Einzel,
            _ => Turnierform.Unbekannt,
        };
        return form == Turnierform.Unbekannt ? null : new Planturnier(tag, form, teile[2..]);
    }
}
