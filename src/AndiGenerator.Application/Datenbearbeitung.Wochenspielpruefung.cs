// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Geo;
using AndiGenerator.Persistence.Gemeinsam;
using AndiGenerator.Persistence.Geo;
using AndiGenerator.Persistence.Plandaten;
using AndiGenerator.Persistence.Terminwuensche;

namespace AndiGenerator.Application;

/// <summary>
/// Prüfung der Liste „keine Auswärtsspiele unter der Woche“ (60-km-Regel, nicht im Original): Die Wettspielordnung legt
/// kein Maß fest, die Liste ist ein Wunsch der Vereine. Die Prüfung gibt deshalb nur Hinweise – nach der Luftlinie
/// zwischen den Hallen (aus Postleitzahl und Ort) und nur für Gastgeber mit Heimterminen unter der Woche.
/// </summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>Vorgabe für die Grenze in Kilometern Luftlinie.</summary>
    public const double Wochenspielgrenze = 60;

    /// <summary>Prüft mit den gespeicherten Hallenadressen (aus einem übernommenen Bericht „Terminwünsche“).</summary>
    /// <param name="grenze">Grenze in Kilometern Luftlinie, z. B. 60.</param>
    /// <returns>Das Ergebnis.</returns>
    public Wochenspielpruefung WochenspielePruefen(double grenze) => WochenspielePruefen(grenze, []);

    /// <summary>Prüft; die Adressen kommen aus den Meldungen, sonst aus den gespeicherten Hallenadressen.</summary>
    /// <param name="grenze">Grenze in Kilometern Luftlinie, z. B. 60.</param>
    /// <param name="meldungen">Meldungen eines gerade geladenen Berichts „Terminwünsche“ (darf leer sein).</param>
    /// <returns>Das Ergebnis.</returns>
    public Wochenspielpruefung WochenspielePruefen(double grenze, IReadOnlyList<Vereinsmeldung> meldungen)
    {
        ArgumentNullException.ThrowIfNull(meldungen);
        List<string> namen = Teams(Arbeitsstand).Select(t => t.Lesen("teamname")).ToList();
        Dictionary<string, Koordinate> lage = HallenLagen(meldungen);
        List<string> ohneLage = namen.Where(n => !lage.ContainsKey(n)).ToList();
        var gesperrt = namen.ToDictionary(n => n, n => NurWochenendeAus(Arbeitsstand, n).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.Ordinal);
        List<string> pauschal = namen.Where(n => namen.Count > 2 && namen.All(g => g == n || gesperrt[n].Contains(g))).ToList();
        HashSet<string> wochentags = Wochentagsgastgeber();

        var befunde = new List<Wochenspielbefund>();
        int geprueft = 0;
        foreach (string gast in namen.Where(lage.ContainsKey))
        {
            foreach (string heim in namen.Where(h => h != gast && wochentags.Contains(h) && lage.ContainsKey(h)))
            {
                geprueft++;
                double km = lage[gast].Luftlinie(lage[heim]);
                bool gesperrtGegen = gesperrt[gast].Contains(heim);
                if (!gesperrtGegen && km > grenze)
                {
                    befunde.Add(new Wochenspielbefund(gast, heim, km, Wochenspielhinweis.SperreFehlt));
                }
                else if (gesperrtGegen && km <= grenze && !pauschal.Contains(gast))
                {
                    befunde.Add(new Wochenspielbefund(gast, heim, km, Wochenspielhinweis.SperreUnnoetig));
                }
            }
        }

        return new Wochenspielpruefung(
            grenze,
            befunde.OrderBy(b => b.Art).ThenByDescending(b => b.Luftlinie).ToList(),
            pauschal,
            ohneLage,
            namen.Where(wochentags.Contains).ToList(),
            geprueft);
    }

    /// <summary>
    /// Entfernungen von der Halle einer Mannschaft zu den Hallen der anderen (aus den gespeicherten Hallenadressen) – für
    /// die Seite „Wochenende (60-km-Regel)“.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname (der Gast).</param>
    /// <returns>Je anderer Mannschaft die Entfernung, in der Reihenfolge der Staffel.</returns>
    public IReadOnlyList<Hallenentfernung> Hallenentfernungen(string mannschaft)
    {
        Dictionary<string, Koordinate> lage = HallenLagen([]);
        HashSet<string> wochentags = Wochentagsgastgeber();
        bool eigene = lage.TryGetValue(mannschaft, out Koordinate start);
        return Teams(Arbeitsstand)
            .Select(t => t.Lesen("teamname"))
            .Where(n => n != mannschaft)
            .Select(n => new Hallenentfernung(
                n,
                eigene && lage.TryGetValue(n, out Koordinate ziel) ? start.Luftlinie(ziel) : null,
                wochentags.Contains(n)))
            .ToList();
    }

    /// <summary>
    /// Setzt die Sperren der Befunde „Sperre fehlt vermutlich“: Der Gast spielt beim Gastgeber nur noch am Wochenende.
    /// Vorhandene Sperren bleiben.
    /// </summary>
    /// <param name="befunde">Befunde der Prüfung; nur <see cref="Wochenspielhinweis.SperreFehlt"/> zählt.</param>
    /// <returns>Die tatsächlich gesetzten Sperren (für das Protokoll).</returns>
    public IReadOnlyList<Wochenspielbefund> WochenspielSperrenSetzen(IEnumerable<Wochenspielbefund> befunde)
    {
        ArgumentNullException.ThrowIfNull(befunde);
        var gesetzt = new List<Wochenspielbefund>();
        foreach (IGrouping<string, Wochenspielbefund> gast in befunde.Where(b => b.Art == Wochenspielhinweis.SperreFehlt).GroupBy(b => b.Gast, StringComparer.Ordinal))
        {
            List<string> gegner = NurWochenendeAus(Arbeitsstand, gast.Key);
            List<Wochenspielbefund> neu = gast.Where(b => !gegner.Contains(b.Heim, StringComparer.OrdinalIgnoreCase)).ToList();
            NurWochenendeSpeichern(gast.Key, gegner.Concat(neu.Select(b => b.Heim)));
            gesetzt.AddRange(neu);
        }

        return gesetzt;
    }

    /// <summary>Nimmt mit <see cref="WochenspielSperrenSetzen"/> gesetzte Sperren wieder zurück.</summary>
    /// <param name="gesetzt">Die gesetzten Sperren.</param>
    public void WochenspielSperrenEntfernen(IEnumerable<Wochenspielbefund> gesetzt)
    {
        ArgumentNullException.ThrowIfNull(gesetzt);
        foreach (IGrouping<string, Wochenspielbefund> gast in gesetzt.GroupBy(b => b.Gast, StringComparer.Ordinal))
        {
            HashSet<string> weg = gast.Select(b => b.Heim).ToHashSet(StringComparer.OrdinalIgnoreCase);
            NurWochenendeSpeichern(gast.Key, NurWochenendeAus(Arbeitsstand, gast.Key).Where(n => !weg.Contains(n)));
        }
    }

    /// <summary>Lage der Halle: Spiellokal der Mannschaft (Nummer am Team, sonst die häufigste der Heimtermine, sonst 1).</summary>
    private static Koordinate? HallenLage(DatenKnoten team, IReadOnlyList<Vereinsmeldung> meldungen)
    {
        int nummer = DelphiKompatibel.ZahlLesen(team.Lesen("location"), 0);
        if (nummer is < 1 or > 3)
        {
            nummer = team.KinderMitNamen("homegameday")
                .Select(k => DelphiKompatibel.ZahlLesen(k.Lesen("location"), 0))
                .Where(n => n is >= 1 and <= 3)
                .GroupBy(n => n)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault(1);
        }

        Spiellokaladresse? adresse = MeldungZu(team, meldungen) is Vereinsmeldung m
            ? m.Spiellokale.GetValueOrDefault(nummer) ?? m.Spiellokale.Values.FirstOrDefault()
            : GespeicherteAdresse(team, nummer);
        return adresse switch
        {
            { Lage: Koordinate gps } => gps,
            { Postleitzahl.Length: > 0 } a => Postleitzahlen.Lage(a.Postleitzahl, a.Ort),
            _ => null,
        };
    }

    private static Spiellokaladresse? GespeicherteAdresse(DatenKnoten team, int nummer)
    {
        string text = team.Lesen(AdressAttribut + nummer.ToString(CultureInfo.InvariantCulture));
        if (text.Length == 0)
        {
            text = team.Lesen(AdressAttribut + "1");
        }

        return text.Length == 0 ? null : Spiellokaladresse.Aus(text);
    }

    private Dictionary<string, Koordinate> HallenLagen(IReadOnlyList<Vereinsmeldung> meldungen)
    {
        var lage = new Dictionary<string, Koordinate>(StringComparer.Ordinal);
        foreach (DatenKnoten team in Teams(Arbeitsstand))
        {
            if (HallenLage(team, meldungen) is Koordinate k)
            {
                lage[team.Lesen("teamname")] = k;
            }
        }

        return lage;
    }

    private HashSet<string> Wochentagsgastgeber() =>
        Teams(Arbeitsstand).Where(HatWochentagsTermine).Select(t => t.Lesen("teamname")).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Heimtermine unter der Woche und zugleich am Wochenende: Nur dann entscheidet die Sperre, wann gespielt wird. Unter der
    /// Woche heißt Montag bis Donnerstag, Freitag nur, wenn er nicht zum Wochenende zählt.
    /// </summary>
    private bool HatWochentagsTermine(DatenKnoten team)
    {
        List<bool> wochenende = team.KinderMitNamen("homegameday")
            .Select(k => DelphiKompatibel.DatumZeitStreng(k.Lesen("datetime")))
            .OfType<DateTime>()
            .Select(t => t.DayOfWeek switch
            {
                DayOfWeek.Saturday or DayOfWeek.Sunday => true,
                DayOfWeek.Friday => FreitagZaehltZumWochenende,
                _ => false,
            })
            .ToList();
        return wochenende.Contains(true) && wochenende.Contains(false);
    }
}
