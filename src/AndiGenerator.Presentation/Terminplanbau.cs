// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Presentation;

/// <summary>
/// Baut die Terminplanansicht in der neuen Gestaltung: Spiele ohne Termin gesammelt, der Spielplan als Wochenkarten und die
/// Mannschaftspläne als Kacheln mit Heim/Auswärts-Folge. Inhalt und Reihenfolge wie im Original (<c>PaintTerminPlan</c>,
/// <c>PaintMannschaftsPlaeneIntern</c>).
/// </summary>
internal static class Terminplanbau
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Die Spiele mit Chips und Nachbarspielen, in Terminreihenfolge.</summary>
    /// <param name="plan">Der Terminplan.</param>
    /// <returns>Die Spiele.</returns>
    internal static List<Terminspiel> Spiele(IReadOnlyList<Terminplanzeile> plan) =>
        plan.Select((z, i) => Spiel(z, i)).ToList();

    /// <summary>Die Spiele mit Termin nach Kalenderwochen.</summary>
    /// <param name="plan">Der Terminplan.</param>
    /// <param name="spiele">Die Spiele aus <see cref="Spiele"/> (gleiche Reihenfolge).</param>
    /// <returns>Die Wochen.</returns>
    internal static List<Terminwoche> Wochen(IReadOnlyList<Terminplanzeile> plan, IReadOnlyList<Terminspiel> spiele)
    {
        var wochen = new List<Terminwoche>();
        List<Terminspiel>? aktuelle = null;
        DateTime montag = default;
        int woche = -1;
        int runde = int.MinValue;
        string rundenwechsel = string.Empty;
        for (int i = 0; i < plan.Count; i++)
        {
            if (plan[i].Zeitpunkt is not DateTime zeit)
            {
                continue;
            }

            if (aktuelle is null || plan[i].Kalenderwoche != woche)
            {
                Abschliessen(wochen, aktuelle, woche, montag, rundenwechsel);
                aktuelle = [];
                woche = plan[i].Kalenderwoche;
                montag = zeit.Date.AddDays(-(((int)zeit.DayOfWeek + 6) % 7));
                rundenwechsel = plan[i].Runde != runde ? Rundenname(plan[i].Runde) : string.Empty;
            }

            runde = plan[i].Runde;
            aktuelle.Add(spiele[i]);
        }

        Abschliessen(wochen, aktuelle, woche, montag, rundenwechsel);
        return wochen;
    }

    /// <summary>Je Mannschaft ihre Spiele wie im Original, wahlweise mit den Spielen der Vereinsmannschaften am selben Tag.</summary>
    /// <param name="plan">Der Terminplan.</param>
    /// <param name="spiele">Die Spiele aus <see cref="Spiele"/> (gleiche Reihenfolge).</param>
    /// <param name="staffel">Die Staffel (Reihenfolge der Mannschaften).</param>
    /// <param name="mitNachbarn">Spiele der Vereinsmannschaften mit anzeigen.</param>
    /// <returns>Die Mannschaftspläne.</returns>
    internal static List<Mannschaftsplan> Mannschaftsplaene(IReadOnlyList<Terminplanzeile> plan, IReadOnlyList<Terminspiel> spiele, Staffel staffel, bool mitNachbarn)
    {
        var ergebnis = new List<Mannschaftsplan>();
        foreach (string name in staffel.Mannschaften.Select(m => m.Name))
        {
            var eigene = new List<Mannschaftsspiel>();
            int? letzteRunde = null;
            for (int i = 0; i < plan.Count; i++)
            {
                Terminplanzeile z = plan[i];
                if (z.Heim != name && z.Gast != name)
                {
                    continue;
                }

                bool heim = z.Heim == name;
                List<Nachbarspiel> nachbarn = mitNachbarn
                    ? z.NachbartermineHeim.Concat(z.NachbartermineGast)
                        .Where(n => Terminplanauswertung.GleicherVerein(n.Heim, name) || Terminplanauswertung.GleicherVerein(n.Gast, name))
                        .Select(Nachbar)
                        .ToList()
                    : [];
                eigene.Add(new Mannschaftsspiel(spiele[i], heim, heim ? z.Gast : z.Heim, nachbarn, NeueRunde: letzteRunde is int alt && z.Runde > alt));
                letzteRunde = letzteRunde is int bisher ? Math.Max(bisher, z.Runde) : z.Runde;
            }

            int heimspiele = eigene.Count(s => s.Heimspiel);
            string bilanz = string.Create(Deutsch, $"{heimspiele} Heim · {eigene.Count - heimspiele} Auswärts");
            List<bool> folge = eigene.Where(s => s.Spiel.Zeit.Length > 0).Select(s => s.Heimspiel).ToList();
            ergebnis.Add(new Mannschaftsplan(name, bilanz, folge, eigene));
        }

        return ergebnis;
    }

    /// <summary>Name einer Runde für Zwischenüberschriften.</summary>
    /// <param name="runde">Index der Runde ab 0 (<c>RundeZuDatum</c>); negativ = keiner Runde zugeordnet.</param>
    /// <returns>Z. B. „Vorrunde“; leer ohne Runde.</returns>
    internal static string Rundenname(int runde) => runde switch
    {
        < 0 => string.Empty,
        0 => "Vorrunde",
        1 => "Rückrunde",
        _ => string.Create(Deutsch, $"{runde + 1}. Runde"),
    };

    private static void Abschliessen(List<Terminwoche> wochen, List<Terminspiel>? spiele, int woche, DateTime montag, string rundenwechsel)
    {
        if (spiele is null || spiele.Count == 0)
        {
            return;
        }

        DateTime sonntag = montag.AddDays(6);
        string zeitraum = montag.ToString("dd.MM.", Deutsch) + "–" + sonntag.ToString("dd.MM.yyyy", Deutsch);
        wochen.Add(new Terminwoche(string.Create(Deutsch, $"KW {woche}"), zeitraum, rundenwechsel, spiele));
    }

    private static Terminspiel Spiel(Terminplanzeile z, int nummer)
    {
        List<Nachbarspiel> nachbarn = z.NachbartermineHeim.Concat(z.NachbartermineGast).Select(Nachbar).ToList();
        return z.Zeitpunkt is DateTime t
            ? new Terminspiel(nummer, t.ToString("ddd dd.MM.", Deutsch), t.ToString("dddd, dd.MM.yyyy", Deutsch), t.ToString("HH:mm", Deutsch), z.Heim, z.Gast, Rundenname(z.Runde), Hinweischip.Aus(z.Hinweis), nachbarn)
            : new Terminspiel(nummer, "ohne Termin", "noch ohne Termin", string.Empty, z.Heim, z.Gast, Rundenname(z.Runde), Hinweischip.Aus(z.Hinweis), nachbarn);
    }

    private static Nachbarspiel Nachbar(Nachbartermin n) =>
        new(n.Zeitpunkt.ToString("HH:mm", Deutsch), $"({n.Geschlecht}) {n.Heim} – {n.Gast}", n.Spiellokal, n.EchteNachbarmannschaft);
}
