// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>Seite „Wunschtermine“ (Original <c>TDialogPanelHomeDaysDetail</c>).</summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>
    /// Prüft die Terminübersicht wie das Original (<c>CheckData</c>): Zu jedem Doppelspieltag muss es am Vortag oder am
    /// Folgetag einen weiteren Termin geben.
    /// </summary>
    /// <param name="wochen">Die Wochen.</param>
    /// <returns>Die Fehlermeldung oder <c>null</c>.</returns>
    public static string? WunschterminePruefen(IReadOnlyList<Wunschterminwoche> wochen)
    {
        ArgumentNullException.ThrowIfNull(wochen);
        List<Heimtag> tage = Termine(wochen);
        Heimtag? fehler = tage.Find(h => h.Doppeltermin
            && !tage.Exists(n => Math.Abs(DateOnly.FromDateTime(n.Datum).DayNumber - DateOnly.FromDateTime(h.Datum).DayNumber) == 1));
        return fehler is null
            ? null
            : $"Am {Datumsanzeige.Tag(DateOnly.FromDateTime(fehler.Datum))} wurde ein Doppelspieltag angegeben. Das ist nur zulässig, wenn am darauf folgenden Tag auch ein Doppelspieltag ist. Für einen Doppelspieltag sind immer zwei Termine an zwei Tagen hintereinander nötig, welche beide als Doppelspieltag gekennzeichnet sind.";
    }

    /// <summary>
    /// Liest die Terminübersicht einer Mannschaft (Original <c>DataToForm</c>): je Woche ab dem Montag vor dem Saisonbeginn
    /// bis zum Saisonende (höchstens 360 Tage) die Texte der sieben Tage.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Die Wochen.</returns>
    public IReadOnlyList<Wunschterminwoche> Wunschtermine(string mannschaft) => WunschtermineAus(Heimtage(mannschaft));

    /// <summary>Liest die Terminübersicht des click-TT-Stands (Original <c>toDefault</c>).</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Die Wochen oder <c>null</c> ohne Standard.</returns>
    public IReadOnlyList<Wunschterminwoche>? WunschtermineStandard(string mannschaft) =>
        standard is null ? null : WunschtermineAus(HeimtageAus(standard, mannschaft));

    /// <summary>
    /// Schreibt die Terminübersicht (Original <c>FormToData</c> mit <c>SetHomeDays</c>). Abweichend vom Original bleiben
    /// Termine außerhalb der angezeigten Wochen erhalten, statt beim Speichern zu verschwinden.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="wochen">Die Wochen.</param>
    public void WunschtermineSpeichern(string mannschaft, IReadOnlyList<Wunschterminwoche> wochen) =>
        HeimtageSpeichern(mannschaft, MitTerminenAusserhalb(Heimtage(mannschaft), wochen));

    /// <summary>
    /// Holt einen Wert, der angibt, ob die Terminübersicht dem click-TT-Stand entspricht (Original <c>isDefault</c>). Anders
    /// als im Original wird dafür nichts gespeichert (Befund #23); verglichen wird der Stand, der nach dem Speichern gälte.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="wochen">Die angezeigten Wochen.</param>
    /// <returns><c>true</c>, wenn gleich oder ohne Standard.</returns>
    public bool WunschtermineSindStandard(string mannschaft, IReadOnlyList<Wunschterminwoche> wochen)
    {
        List<Heimtag> vorlage = standard is null ? [] : HeimtageAus(standard, mannschaft);
        List<Heimtag> jetzt = MitTerminenAusserhalb(Heimtage(mannschaft), wochen).Select(NachSpeichern).ToList();
        return vorlage.Count == jetzt.Count && vorlage.TrueForAll(jetzt.Contains);
    }

    /// <summary>Wunschtermine und Sperrtermine der Übersicht in Datumsreihenfolge.</summary>
    private static List<Heimtag> Termine(IReadOnlyList<Wunschterminwoche> wochen) =>
        wochen.SelectMany(w => w.Texte.Select((t, i) => Heimtagtext.Lesen(w.Montag.AddDays(i), t)))
            .OfType<Heimtag>()
            .ToList();

    /// <summary>
    /// Wie ein Termin nach <c>SetHomeDays</c> und <c>GetHomeDays</c> aussieht: Spiellokal bereinigt, Priorität nur bei
    /// Koppel/Doppelspieltag, zweite Zeit nur bei Koppel oder Auswärtskoppel, Doppel- und Auswärtsangaben nicht bei Koppel.
    /// </summary>
    private static Heimtag NachSpeichern(Heimtag h)
    {
        if (h.Sperrtermin)
        {
            return h;
        }

        bool doppel = !h.Koppeltermin && h.Doppeltermin;
        bool auswaerts = !h.Koppeltermin && h.AuswaertsKoppelZweitzeit;
        return h with
        {
            Doppeltermin = doppel,
            AuswaertsKoppelZweitzeit = auswaerts,
            KoppelPrio = h.Koppeltermin || doppel ? h.KoppelPrio : 0,
            KoppelZweitzeit = h.Koppeltermin || auswaerts ? h.KoppelZweitzeit : TimeOnly.MinValue,
            Spiellokal = Spiellokale.Korrigieren(h.Spiellokal),
        };
    }

    private static List<Heimtag> MitTerminenAusserhalb(IReadOnlyList<Heimtag> bisher, IReadOnlyList<Wunschterminwoche> wochen)
    {
        List<Heimtag> neu = Termine(wochen);
        if (wochen.Count == 0)
        {
            return [.. bisher];
        }

        DateOnly von = wochen[0].Montag;
        DateOnly bis = wochen[^1].Montag.AddDays(6);
        neu.AddRange(bisher.Where(h => DateOnly.FromDateTime(h.Datum) < von || DateOnly.FromDateTime(h.Datum) > bis));
        return neu.OrderBy(h => h.Datum).ToList();
    }

    private IReadOnlyList<Wunschterminwoche> WunschtermineAus(IReadOnlyList<Heimtag> tage)
    {
        string beginn = Arbeitsstand.Lesen("from");
        if (beginn.Length == 0)
        {
            return [];
        }

        DateOnly beginnTag = Datum(beginn);
        DateOnly montag = beginnTag.AddDays(-(((int)beginnTag.DayOfWeek + 6) % 7));
        string endeText = Arbeitsstand.Lesen("until");
        DateOnly ende = endeText.Length == 0 ? montag.AddDays(360) : Datum(endeText);
        if (ende > montag.AddDays(360))
        {
            ende = montag.AddDays(360);
        }

        var wochen = new List<Wunschterminwoche>();
        for (DateOnly m = montag; m <= ende; m = m.AddDays(7))
        {
            DateOnly woche = m;
            wochen.Add(new Wunschterminwoche(
                m,
                Enumerable.Range(0, 7).Select(i => Heimtagtext.Text(tage.LastOrDefault(h => DateOnly.FromDateTime(h.Datum) == woche.AddDays(i)))).ToList()));
        }

        return wochen;
    }
}
