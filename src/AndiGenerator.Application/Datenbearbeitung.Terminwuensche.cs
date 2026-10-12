// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Import;
using AndiGenerator.Persistence.Gemeinsam;
using AndiGenerator.Persistence.Plandaten;
using AndiGenerator.Persistence.Terminwuensche;

namespace AndiGenerator.Application;

/// <summary>
/// Bericht „Terminwünsche“ aus click-TT (PDF, nicht im Original): Spiellokal-Nummern aus den Hallennamen, Adressen der
/// Spiellokale und Bemerkungen der Vereine übernehmen; Unterschiede zu den Spielplandaten zeigen.
/// </summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>Attribut am Knoten <c>plan</c>: wann welcher Bericht übernommen wurde (nicht im Original).</summary>
    private const string TerminwunschAttribut = "terminwuensche";

    /// <summary>Attribut am Knoten <c>team</c>: Bemerkung des Vereins aus dem Bericht (nicht im Original).</summary>
    private const string BemerkungAttribut = "bemerkung";

    /// <summary>Attribute am Knoten <c>team</c>: Adressen der Spiellokale 1 bis 3 (nicht im Original).</summary>
    private const string AdressAttribut = "spiellokal";

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Holt den Vermerk, welcher Bericht „Terminwünsche“ wann übernommen wurde, oder einen leeren Text.</summary>
    public string TerminwunschVermerk => Arbeitsstand.Lesen(TerminwunschAttribut);

    /// <summary>Liest den Bericht „Terminwünsche“ (PDF) und gleicht ihn mit den Spielplandaten ab, ohne etwas zu ändern.</summary>
    /// <param name="pfad">Pfad der PDF-Datei.</param>
    /// <returns>Der Abgleich.</returns>
    /// <exception cref="FormatException">Die Datei ist kein Bericht „Terminwünsche“.</exception>
    /// <exception cref="IOException">Die Datei kann nicht gelesen werden.</exception>
    public Terminwunschabgleich TerminwuenscheAbgleichen(string pfad) => TerminwuenscheAbgleichen(TerminwunschPdf.Laden(pfad));

    /// <summary>Gleicht gelesene Meldungen mit den Spielplandaten ab, ohne etwas zu ändern.</summary>
    /// <param name="meldungen">Die Meldungen.</param>
    /// <returns>Der Abgleich.</returns>
    public Terminwunschabgleich TerminwuenscheAbgleichen(IReadOnlyList<Vereinsmeldung> meldungen)
    {
        ArgumentNullException.ThrowIfNull(meldungen);
        var mannschaften = new List<Mannschaftsabgleich>();
        foreach (DatenKnoten team in Teams(Arbeitsstand))
        {
            string name = team.Lesen("teamname");
            if (MeldungZu(team, meldungen) is not Vereinsmeldung meldung)
            {
                mannschaften.Add(new Mannschaftsabgleich(name, false, [], 0, string.Empty, []));
                continue;
            }

            mannschaften.Add(new Mannschaftsabgleich(
                name,
                true,
                meldung.Spiellokale.OrderBy(l => l.Key).Select(l => l.Key.ToString(CultureInfo.InvariantCulture) + ": " + l.Value.Text).ToList(),
                Zuordnungen(team, meldung).Count,
                meldung.Bemerkung,
                Abweichungen(team, meldung)));
        }

        return new Terminwunschabgleich(meldungen, mannschaften);
    }

    /// <summary>
    /// Übernimmt den Bericht: setzt bei Heimterminen ohne Spiellokal die Nummer des Spiellokals mit gleichem Hallennamen und
    /// merkt sich Adressen und Bemerkungen an der Mannschaft. Termine und Sperren bleiben, wie sie sind.
    /// </summary>
    /// <param name="abgleich">Der Abgleich.</param>
    /// <param name="datei">Name der Datei für den Vermerk.</param>
    public void TerminwuenscheUebernehmen(Terminwunschabgleich abgleich, string datei)
    {
        ArgumentNullException.ThrowIfNull(abgleich);
        foreach (DatenKnoten team in Teams(Arbeitsstand))
        {
            if (MeldungZu(team, abgleich.Meldungen) is not Vereinsmeldung meldung)
            {
                continue;
            }

            foreach ((DatenKnoten termin, int nummer) in Zuordnungen(team, meldung))
            {
                termin.Setzen("location", nummer.ToString(CultureInfo.InvariantCulture));
            }

            foreach ((int nummer, Spiellokaladresse adresse) in meldung.Spiellokale)
            {
                team.Setzen(AdressAttribut + nummer.ToString(CultureInfo.InvariantCulture), adresse.Text);
            }

            team.Setzen(BemerkungAttribut, meldung.Bemerkung);
        }

        Arbeitsstand.Setzen(TerminwunschAttribut, $"„{datei}“ übernommen am {DateTime.Today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}");
    }

    private static Vereinsmeldung? MeldungZu(DatenKnoten team, IReadOnlyList<Vereinsmeldung> meldungen)
    {
        int verein = DelphiKompatibel.ZahlLesen(team.Lesen("clubid"), -1);
        int nummer = Math.Max(1, team.LesenZahl("teamnumber"));
        return meldungen.FirstOrDefault(m => m.VereinsNummer == verein && m.MannschaftsNummer == nummer);
    }

    /// <summary>Heimtermine ohne Spiellokal, deren Halle (aus click-TT) einem Spiellokal des Berichts entspricht.</summary>
    private static List<(DatenKnoten Termin, int Nummer)> Zuordnungen(DatenKnoten team, Vereinsmeldung meldung)
    {
        var ergebnis = new List<(DatenKnoten Termin, int Nummer)>();
        foreach (DatenKnoten termin in team.KinderMitNamen("homegameday").Where(t => t.Lesen("location").Length == 0))
        {
            string halle = Hallenform(termin.Lesen(StaffelAbbildung.HallenAttribut));
            int nummer = halle.Length == 0 ? 0 : meldung.Spiellokale.FirstOrDefault(l => Hallenform(l.Value.Name) == halle).Key;
            if (nummer > 0)
            {
                ergebnis.Add((termin, nummer));
            }
        }

        return ergebnis;
    }

    /// <summary>Heim- und Sperrtermine, die nur im Bericht oder nur in den Spielplandaten stehen.</summary>
    private static List<string> Abweichungen(DatenKnoten team, Vereinsmeldung meldung)
    {
        var daten = team.KinderMitNamen("homegameday")
            .Select(k => DelphiKompatibel.DatumZeitStreng(k.Lesen("datetime")))
            .OfType<DateTime>()
            .ToHashSet();
        var bericht = meldung.Heimtermine.Select(t => t.Zeitpunkt).ToHashSet();
        var sperrenDaten = team.KinderMitNamen("nogameday").Select(k => Datum(k.Lesen("date"))).ToHashSet();
        var sperrenBericht = meldung.Sperrtermine.ToHashSet();

        var ergebnis = new List<string>();
        ergebnis.AddRange(bericht.Except(daten).Order().Select(t => "Heimtermin nur im Bericht: " + Termintext(t)));
        ergebnis.AddRange(daten.Except(bericht).Order().Select(t => "Heimtermin nur in den Spielplandaten: " + Termintext(t)));
        ergebnis.AddRange(sperrenBericht.Except(sperrenDaten).Order().Select(t => "Sperrtermin nur im Bericht: " + Tagtext(t)));
        ergebnis.AddRange(sperrenDaten.Except(sperrenBericht).Order().Select(t => "Sperrtermin nur in den Spielplandaten: " + Tagtext(t)));
        return ergebnis;
    }

    private static string Hallenform(string halle) =>
        string.Join(' ', halle.Replace("- ", "-", StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static string Termintext(DateTime zeit) => zeit.ToString("ddd dd.MM.yyyy HH:mm", Deutsch);

    private static string Tagtext(DateOnly tag) => tag.ToString("ddd dd.MM.yyyy", Deutsch);
}
