// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Gemeinsam;

namespace AndiGenerator.Persistence.Plandaten;

/// <summary>
/// Spiellokal eines Spiels für den CSV-Export (Original <c>TGame.getLocation</c>, <c>TPlan.getSchedule</c>,
/// <c>TMannschaft.loadHomeGame</c>, <c>TPlan.ModifyTermineFromOptions</c>):
/// <list type="number">
/// <item>Gibt es ein vorgegebenes Spiel mit gleichem Termin und gleichen Mannschaften, gilt dessen Spiellokal (auch wenn leer).</item>
/// <item>Sonst das Spiellokal des Heimspieltermins der Heimmannschaft mit genau diesem Termin; ist dort keins eingetragen,
/// das Spiellokal der Mannschaft. Als Heimspieltermin zählen auch die zweite Uhrzeit eines Koppeltermins und die
/// zweite Uhrzeit für Auswärtskoppel.</item>
/// <item>Sonst leer.</item>
/// </list>
/// Gültige Spiellokale sind nur „1“ bis „5“ (click-TT kennt nur Nummern), alles andere wird leer (<see cref="Korrigieren"/>).
/// Hinweis: Das Original übernimmt beim Laden außerdem ein Spiellokal aus bestehenden Spielplänen (<c>OverrideLocation</c>);
/// click-TT liefert dort immer ein leeres Lokal, daher ist das hier nicht nachgebildet (siehe MIGRATIONSPLAN, Phase 1).
/// </summary>
public sealed class Spiellokale
{
    private static readonly string[] Gueltig = [string.Empty, "1", "2", "3", "4", "5"];

    private readonly Dictionary<(DateTime, string, string), string> vorgegeben = [];
    private readonly Dictionary<string, Dictionary<DateTime, string>> heimtermine = new(StringComparer.Ordinal);

    /// <summary>Wertet den (mit den Änderungen zusammengeführten) Datenbaum einer Staffel aus.</summary>
    /// <param name="plan">Wurzelknoten <c>plan</c>.</param>
    /// <param name="halbrunde">Rundenplanung „Halbrunde“: Heimspieltermine außerhalb von Beginn bis Ende zählen nicht (Original <c>isValidDateForWunschtermin</c>).</param>
    /// <exception cref="PlanDatenFormatException">Ein Termin im Datenbaum ist ungültig (das Original bricht dann das Laden ab).</exception>
    public Spiellokale(DatenKnoten plan, bool halbrunde = false)
    {
        ArgumentNullException.ThrowIfNull(plan);
        try
        {
            DateTime? beginn = DatumOderNull(plan.Lesen("from"));
            DateTime? ende = DatumOderNull(plan.Lesen("until"));
            bool Zulaessig(DateTime termin) =>
                !halbrunde || (termin >= (beginn ?? DelphiKompatibel.DelphiNull) && termin <= (ende ?? DelphiKompatibel.DelphiNull));

            foreach (DatenKnoten liste in plan.KinderMitNamen("predefinedgames"))
            {
                foreach (DatenKnoten spiel in liste.KinderMitNamen("game"))
                {
                    DateTime termin = DelphiKompatibel.DatumZeitStreng(spiel.Lesen("datetime")) ?? DelphiKompatibel.DelphiNull;
                    vorgegeben.TryAdd((termin, spiel.Lesen("hometeamname"), spiel.Lesen("guestteamname")), Korrigieren(spiel.Lesen("location")));
                }
            }

            foreach (DatenKnoten mannschaft in plan.KinderMitNamen("team"))
            {
                string mannschaftsLokal = Korrigieren(mannschaft.Lesen("location"));
                var termine = new Dictionary<DateTime, string>();
                var zweiteZeiten = new List<(DateTime, string)>();

                foreach (DatenKnoten heim in mannschaft.KinderMitNamen("homegameday"))
                {
                    DateTime termin = DelphiKompatibel.DatumZeitStreng(heim.Lesen("datetime")) ?? DelphiKompatibel.DelphiNull;
                    string lokal = Korrigieren(heim.Lesen("location"));
                    if (lokal.Length == 0)
                    {
                        lokal = mannschaftsLokal;
                    }

                    if (Zulaessig(termin))
                    {
                        termine.TryAdd(termin, lokal);
                    }

                    // Koppeltermin an einem Tag: zweite Uhrzeit; sonst ggf. zweite Uhrzeit für Auswärtskoppel.
                    bool koppelAmTag = heim.LesenBool("couplegameday");
                    if (koppelAmTag || heim.LesenBool("coupleauswaertssecondtime"))
                    {
                        string zweite = heim.Lesen("couplesecondtime");
                        TimeOnly zeit = zweite.Length == 0 ? TimeOnly.MinValue : DelphiKompatibel.TimeFromString(zweite);
                        DateTime zweiterTermin = termin.Date + zeit.ToTimeSpan();
                        if (Zulaessig(termin))
                        {
                            zweiteZeiten.Add((zweiterTermin, lokal));
                        }
                    }
                }

                foreach ((DateTime termin, string lokal) in zweiteZeiten)
                {
                    termine.TryAdd(termin, lokal);
                }

                heimtermine.TryAdd(mannschaft.Lesen("teamname"), termine);
            }
        }
        catch (FormatException ex)
        {
            throw new PlanDatenFormatException("Ungültiger Termin im Datenbaum: " + ex.Message, ex);
        }
    }

    /// <summary>Bereinigt ein Spiellokal (Original <c>TPlanData.FixLocation</c>): getrimmt; nur „“ und „1“ bis „5“ sind gültig, sonst leer.</summary>
    /// <param name="wert">Eingetragenes Spiellokal.</param>
    /// <returns>Das gültige Spiellokal oder ein leerer Text.</returns>
    public static string Korrigieren(string? wert)
    {
        string lokal = DelphiKompatibel.Trim(wert);
        return Array.IndexOf(Gueltig, lokal) >= 0 ? lokal : string.Empty;
    }

    /// <summary>Ermittelt das Spiellokal eines Spiels.</summary>
    /// <param name="zeitpunkt">Termin; <c>null</c> bei nicht terminierten Spielen.</param>
    /// <param name="heim">Name der Heimmannschaft.</param>
    /// <param name="gast">Name der Gastmannschaft.</param>
    /// <returns>Das Spiellokal oder ein leerer Text, wenn keins bestimmt werden kann.</returns>
    public string Ermitteln(DateTime? zeitpunkt, string heim, string gast)
    {
        DateTime termin = zeitpunkt ?? DelphiKompatibel.DelphiNull;
        if (vorgegeben.TryGetValue((termin, heim, gast), out string? lokal))
        {
            return lokal;
        }

        return zeitpunkt is not null
            && heimtermine.TryGetValue(heim, out Dictionary<DateTime, string>? termine)
            && termine.TryGetValue(termin, out lokal)
            ? lokal
            : string.Empty;
    }

    private static DateTime? DatumOderNull(string text) =>
        text.Length == 0 ? null : DelphiKompatibel.DateFromString(text).ToDateTime(TimeOnly.MinValue);
}
