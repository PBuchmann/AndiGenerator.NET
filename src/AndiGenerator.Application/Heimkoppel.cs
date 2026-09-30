// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>
/// Heimkoppel- oder Doppelspieltagswunsch (Original <c>TCalculateOptionsKoppelDate</c>): ein einzelner Koppeltermin oder
/// zwei aufeinanderfolgende Heimspieltage mit der gewählten Kombination.
/// </summary>
/// <param name="Tag1">Erster Tag.</param>
/// <param name="Tag2">Zweiter Tag oder <c>null</c> bei einem einzelnen Tag.</param>
/// <param name="Option1">Wunsch am ersten Tag.</param>
/// <param name="Option2">Wunsch am zweiten Tag.</param>
public sealed record Heimkoppel(DateOnly Tag1, DateOnly? Tag2, Koppelwunsch Option1, Koppelwunsch Option2)
{
    /// <summary>Kombinationen für zwei Tage (Original <c>getHomeKoppelOptionsByIndexAndTwoDates</c>).</summary>
    private static readonly (Koppelwunsch Erster, Koppelwunsch Zweiter)[] Paare =
    [
        (Koppelwunsch.Keiner, Koppelwunsch.Keiner),
        (Koppelwunsch.DoppelMoeglich, Koppelwunsch.DoppelMoeglich),
        (Koppelwunsch.DoppelGewuenscht, Koppelwunsch.DoppelGewuenscht),
        (Koppelwunsch.DoppelHoch, Koppelwunsch.DoppelHoch),
        (Koppelwunsch.Moeglich, Koppelwunsch.Keiner),
        (Koppelwunsch.Gewuenscht, Koppelwunsch.Keiner),
        (Koppelwunsch.Hoch, Koppelwunsch.Keiner),
        (Koppelwunsch.Keiner, Koppelwunsch.Moeglich),
        (Koppelwunsch.Keiner, Koppelwunsch.Gewuenscht),
        (Koppelwunsch.Keiner, Koppelwunsch.Hoch),
        (Koppelwunsch.Moeglich, Koppelwunsch.Moeglich),
        (Koppelwunsch.Gewuenscht, Koppelwunsch.Gewuenscht),
        (Koppelwunsch.Hoch, Koppelwunsch.Hoch),
    ];

    /// <summary>Wahlmöglichkeiten für einen einzelnen Tag.</summary>
    private static readonly Koppelwunsch[] Einzeln = [Koppelwunsch.Keiner, Koppelwunsch.Moeglich, Koppelwunsch.Gewuenscht, Koppelwunsch.Hoch];

    /// <summary>Holt die Beschriftung, z. B. <c>Sa 05.09.2026 und So 06.09.2026</c>.</summary>
    public string Beschriftung => Tag2 is DateOnly zweiter ? $"{Text(Tag1)} und {Text(zweiter)}" : Text(Tag1);

    /// <summary>Holt den Index der aktuellen Wahl in <see cref="Auswahl()"/> (Original <c>getComboIndexFromOptions</c>, sonst 0).</summary>
    public int Index => Tag2 is null
        ? Math.Max(0, Array.IndexOf(Einzeln, Option1))
        : Math.Max(0, Array.FindLastIndex(Paare, p => p.Erster == Option1 && p.Zweiter == Option2));

    /// <summary>Name eines Koppelwunschs wie im Original (<c>cWunschterminOptionName</c>).</summary>
    /// <param name="wunsch">Der Wunsch.</param>
    /// <returns>Der Anzeigename.</returns>
    public static string Name(Koppelwunsch wunsch) => wunsch switch
    {
        Koppelwunsch.Moeglich => "Zwei Heimspiele an diesem Tag möglich",
        Koppelwunsch.Gewuenscht => "Zwei Heimspiele an diesem Tag gewünscht",
        Koppelwunsch.Hoch => "Zwei Heimspiele an diesem Tag gewünscht (hohe Prio)",
        Koppelwunsch.DoppelMoeglich => "Doppelspieltag möglich",
        Koppelwunsch.DoppelGewuenscht => "Doppelspieltag gewünscht",
        Koppelwunsch.DoppelHoch => "Doppelspieltag gewünscht (hohe Prio)",
        _ => "kein Koppeltermin/Doppelspieltag",
    };

    /// <summary>Liefert die Wahlmöglichkeiten wie im Original (<c>getComboElemsForHeimKoppelWuensche</c>).</summary>
    /// <returns>Die Beschriftungen, passend zu <see cref="Index"/>.</returns>
    public IReadOnlyList<string> Auswahl()
    {
        if (Tag2 is not DateOnly zweiter)
        {
            return Einzeln.Select(Name).ToList();
        }

        var liste = new List<string>
        {
            Name(Koppelwunsch.Keiner),
            Name(Koppelwunsch.DoppelMoeglich),
            Name(Koppelwunsch.DoppelGewuenscht),
            Name(Koppelwunsch.DoppelHoch),
        };
        foreach (string praefix in new[] { Text(Tag1) + ":", Text(zweiter) + ":", $"{Text(Tag1)} und {Text(zweiter)}:" })
        {
            liste.Add(praefix + " " + Name(Koppelwunsch.Moeglich));
            liste.Add(praefix + " " + Name(Koppelwunsch.Gewuenscht));
            liste.Add(praefix + " " + Name(Koppelwunsch.Hoch));
        }

        return liste;
    }

    /// <summary>Setzt die Wahl über den Index (Original <c>setHomeKoppelOptionsByIndex</c>).</summary>
    /// <param name="index">Index in <see cref="Auswahl()"/>.</param>
    /// <returns>Der geänderte Wunsch.</returns>
    public Heimkoppel MitIndex(int index)
    {
        if (Tag2 is null)
        {
            return this with { Option1 = Einzeln[Math.Clamp(index, 0, Einzeln.Length - 1)], Option2 = Koppelwunsch.Keiner };
        }

        (Koppelwunsch erster, Koppelwunsch zweiter) = Paare[Math.Clamp(index, 0, Paare.Length - 1)];
        return this with { Option1 = erster, Option2 = zweiter };
    }

    private static string Text(DateOnly tag) => Datumsanzeige.Tag(tag);
}
