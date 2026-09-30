// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>Seite „Heimkoppel/Doppelspieltage (Kompakt)“ (Original <c>TDialogPanelHomeCoupleCompactDetail</c>).</summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>
    /// Liest die Heimkoppelwünsche einer Mannschaft (Original <c>LoadKoppelDates</c>): je Paar aufeinanderfolgender
    /// Heimspieltage ein Eintrag, dazu einzelne Koppeltermine ohne Nachbartag.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Die Wünsche in der Reihenfolge der Heimspieltermine.</returns>
    public IReadOnlyList<Heimkoppel> Heimkoppeln(string mannschaft) => HeimkoppelnAus(Arbeitsstand, mannschaft);

    /// <summary>Liest die Heimkoppelwünsche des click-TT-Stands.</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Die Wünsche oder <c>null</c> ohne Standard.</returns>
    public IReadOnlyList<Heimkoppel>? HeimkoppelnStandard(string mannschaft) =>
        standard is null ? null : HeimkoppelnAus(standard, mannschaft);

    /// <summary>
    /// Schreibt die Heimkoppelwünsche (Original <c>FormToData</c>): Koppel- und Doppelspieltagsangaben aller Heimspieltage
    /// zurücksetzen, die Wünsche über das Datum eintragen und alles mit <c>SetHomeDays</c> speichern.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="koppeln">Die Wünsche.</param>
    public void HeimkoppelnSpeichern(string mannschaft, IEnumerable<Heimkoppel> koppeln)
    {
        ArgumentNullException.ThrowIfNull(koppeln);
        List<Heimtag> tage = Heimtage(mannschaft)
            .Select(h => h.IstHeimtag ? h with { Koppeltermin = false, Doppeltermin = false, KoppelPrio = 0 } : h)
            .ToList();
        foreach (Heimkoppel k in koppeln)
        {
            for (int i = 0; i < tage.Count; i++)
            {
                if (!tage[i].IstHeimtag)
                {
                    continue;
                }

                DateOnly tag = DateOnly.FromDateTime(tage[i].Datum);
                if (tag == k.Tag1)
                {
                    tage[i] = WunschSetzen(tage[i], k.Option1);
                }

                if (tag == k.Tag2)
                {
                    tage[i] = WunschSetzen(tage[i], k.Option2);
                }
            }
        }

        HeimtageSpeichern(mannschaft, tage);
    }

    private static List<Heimkoppel> HeimkoppelnAus(DatenKnoten plan, string mannschaft)
    {
        List<Heimtag> heim = HeimtageAus(plan, mannschaft).Where(h => h.IstHeimtag).ToList();
        var ergebnis = new List<Heimkoppel>();
        foreach (Heimtag h in heim)
        {
            DateOnly tag = DateOnly.FromDateTime(h.Datum);
            Heimtag? naechster = heim.LastOrDefault(t => DateOnly.FromDateTime(t.Datum) == tag.AddDays(1));
            bool vorher = heim.Exists(t => DateOnly.FromDateTime(t.Datum) == tag.AddDays(-1));
            if (naechster is not null)
            {
                ergebnis.Add(new Heimkoppel(tag, tag.AddDays(1), Wunsch(h), Wunsch(naechster)));
            }
            else if (!vorher && h.Koppeltermin)
            {
                ergebnis.Add(new Heimkoppel(tag, null, Wunsch(h), Koppelwunsch.Keiner));
            }
        }

        return ergebnis;
    }

    /// <summary>Original <c>KoppelValueFromOptions</c>.</summary>
    private static Koppelwunsch Wunsch(Heimtag h)
    {
        Koppelwunsch ergebnis = Koppelwunsch.Keiner;
        if (h.Koppeltermin)
        {
            ergebnis = Stufe(h.KoppelPrio, Koppelwunsch.Moeglich, Koppelwunsch.Gewuenscht, Koppelwunsch.Hoch) ?? ergebnis;
        }

        if (h.Doppeltermin)
        {
            ergebnis = Stufe(h.KoppelPrio, Koppelwunsch.DoppelMoeglich, Koppelwunsch.DoppelGewuenscht, Koppelwunsch.DoppelHoch) ?? ergebnis;
        }

        return ergebnis;
    }

    private static Koppelwunsch? Stufe(int prio, Koppelwunsch moeglich, Koppelwunsch gewuenscht, Koppelwunsch hoch) => prio switch
    {
        0 => moeglich,
        1 => gewuenscht,
        2 => hoch,
        _ => null,
    };

    private static Heimtag WunschSetzen(Heimtag h, Koppelwunsch wunsch) => wunsch switch
    {
        Koppelwunsch.Moeglich => h with { Koppeltermin = true },
        Koppelwunsch.Gewuenscht => h with { Koppeltermin = true, KoppelPrio = 1 },
        Koppelwunsch.Hoch => h with { Koppeltermin = true, KoppelPrio = 2 },
        Koppelwunsch.DoppelMoeglich => h with { Doppeltermin = true },
        Koppelwunsch.DoppelGewuenscht => h with { Doppeltermin = true, KoppelPrio = 1 },
        Koppelwunsch.DoppelHoch => h with { Doppeltermin = true, KoppelPrio = 2 },
        _ => h,
    };
}
