// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Persistence.Aufstellungen;
using AndiGenerator.Persistence.Gemeinsam;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>Seite „Setzliste“ (Original <c>TDialogPanelRanking</c>).</summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>Attribut am Knoten <c>plan</c>: wann welche Mannschaftsmeldung übernommen wurde (nicht im Original).</summary>
    private const string MannschaftsmeldungAttribut = "mannschaftsmeldung";

    /// <summary>Attribut am Knoten <c>team</c>: Schnitt der TTR-Werte der gewerteten Stammspieler (nicht im Original).</summary>
    private const string TtrAttribut = "ttrschnitt";

    /// <summary>Attribut am Knoten <c>team</c>: Zahl der Stammspieler laut Mannschaftsmeldung (nicht im Original).</summary>
    private const string StammspielerAttribut = "stammspieler";

    /// <summary>Holt den Vermerk, welche Mannschaftsmeldung wann übernommen wurde, oder einen leeren Text.</summary>
    public string MannschaftsmeldungVermerk => Arbeitsstand.Lesen(MannschaftsmeldungAttribut);

    /// <summary>Vermerkt, dass eine Mannschaftsmeldung übernommen wurde (für die Einrichtung: „schon erledigt“).</summary>
    /// <param name="datei">Name der Datei.</param>
    public void MannschaftsmeldungVermerken(string datei) =>
        Arbeitsstand.Setzen(MannschaftsmeldungAttribut, $"„{datei}“ übernommen am {DateTime.Today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}");

    /// <summary>
    /// Liest die Setzliste wie das Original (<c>DataToForm</c>): zuerst die gespeicherte Reihenfolge (nur vorhandene
    /// Mannschaften), dann alle übrigen Mannschaften alphabetisch.
    /// </summary>
    /// <returns>Die Setzliste.</returns>
    public Setzlistendaten Setzliste()
    {
        IReadOnlyList<string> namen = Mannschaftsnamen();
        var liste = new List<string>();
        bool aktiv = false;
        if (Arbeitsstand.ErstesKind("ranking") is DatenKnoten ranking)
        {
            aktiv = ranking.LesenBool("active");
            foreach (DatenKnoten team in ranking.KinderMitNamen("team"))
            {
                string name = team.Lesen("teamname");
                if (namen.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    // Wie im Original: bis zum gespeicherten Platz auffüllen (bei fortlaufenden Plätzen genau ein Eintrag).
                    int platz = team.LesenZahl("rankingindex");
                    while (liste.Count <= platz)
                    {
                        liste.Add(name);
                    }
                }
            }

            liste.RemoveAll(n => n.Length == 0);
        }

        liste.AddRange(namen.Where(n => !liste.Contains(n, StringComparer.Ordinal)).ToList());
        return new Setzlistendaten(aktiv, liste);
    }

    /// <summary>Liest die Mannschaftsmeldung aus click-TT (CSV) und schlägt daraus die Setzliste vor.</summary>
    /// <param name="pfad">Pfad der CSV-Datei.</param>
    /// <returns>Der Vorschlag.</returns>
    /// <exception cref="FormatException">Die Datei ist keine Mannschaftsmeldung.</exception>
    /// <exception cref="IOException">Die Datei kann nicht gelesen werden.</exception>
    public Setzlistenvorschlag SetzlisteAusMannschaftsmeldung(string pfad) => SetzlisteVorschlagen(MannschaftsaufstellungenCsv.Laden(pfad));

    /// <summary>
    /// Vorschlag für die Setzliste aus den Mannschaftsaufstellungen von click-TT: je Mannschaft der Schnitt der besten
    /// Stammspieler (bei Sechser-Mannschaften 6, sonst 4 – erkannt an der üblichen Zahl gemeldeter Stammspieler), die
    /// stärkste Mannschaft zuerst. Mannschaften ohne Aufstellung kommen in ihrer bisherigen Reihenfolge ans Ende.
    /// </summary>
    /// <param name="aufstellungen">Die Aufstellungen aus <see cref="MannschaftsaufstellungenCsv"/>.</param>
    /// <returns>Der Vorschlag.</returns>
    public Setzlistenvorschlag SetzlisteVorschlagen(IReadOnlyList<Mannschaftsaufstellung> aufstellungen)
    {
        ArgumentNullException.ThrowIfNull(aufstellungen);
        IReadOnlyList<string> bisher = Setzliste().Reihenfolge;
        var zugeordnet = bisher.ToDictionary(
            n => n,
            n => aufstellungen.FirstOrDefault(a => a.Mannschaft == n) ?? aufstellungen.FirstOrDefault(a => Vergleichsform(a.Mannschaft) == Vergleichsform(n)),
            StringComparer.Ordinal);
        List<int> anzahlen = zugeordnet.Values.OfType<Mannschaftsaufstellung>().Select(a => a.Stammspieler.Count).Where(n => n > 0).Order().ToList();
        int gewertet = anzahlen.Count > 0 && anzahlen[anzahlen.Count / 2] >= 6 ? 6 : 4;
        List<Mannschaftsstaerke> staerken = bisher
            .Select(n => zugeordnet[n] is Mannschaftsaufstellung a ? new Mannschaftsstaerke(n, a.Schnitt(gewertet), a.Stammspieler.Count) : new Mannschaftsstaerke(n, 0.0, 0))
            .ToList();
        List<Mannschaftsstaerke> reihenfolge = [.. staerken.Where(s => s.HatAufstellung).OrderByDescending(s => s.Schnitt), .. staerken.Where(s => !s.HatAufstellung)];
        return new Setzlistenvorschlag(reihenfolge, gewertet);
    }

    /// <summary>
    /// Speichert die Stärke jeder Mannschaft aus der Mannschaftsmeldung (TTR-Schnitt und Stammspieler) an der Mannschaft;
    /// Mannschaften ohne Aufstellung verlieren einen früheren Wert.
    /// </summary>
    /// <param name="staerken">Die Stärken aus dem Vorschlag.</param>
    public void MannschaftsstaerkenSpeichern(IEnumerable<Mannschaftsstaerke> staerken)
    {
        ArgumentNullException.ThrowIfNull(staerken);
        var nachName = staerken.ToDictionary(s => s.Mannschaft, StringComparer.Ordinal);
        foreach (DatenKnoten team in Teams(Arbeitsstand))
        {
            if (!nachName.TryGetValue(team.Lesen("teamname"), out Mannschaftsstaerke? s) || (!s.HatAufstellung && !team.Hat(TtrAttribut)))
            {
                continue;
            }

            team.Setzen(TtrAttribut, s.HatAufstellung ? Math.Round(s.Schnitt).ToString("0", CultureInfo.InvariantCulture) : string.Empty);
            team.Setzen(StammspielerAttribut, s.HatAufstellung ? s.Stammspieler.ToString(CultureInfo.InvariantCulture) : string.Empty);
        }
    }

    /// <summary>Liest die gespeicherten Stärken (TTR-Schnitt aus der Mannschaftsmeldung) je Mannschaft.</summary>
    /// <returns>Mannschaftsname → Stärke; Mannschaften ohne gespeicherten Wert fehlen.</returns>
    public IReadOnlyDictionary<string, Mannschaftsstaerke> Mannschaftsstaerken()
    {
        var ergebnis = new Dictionary<string, Mannschaftsstaerke>(StringComparer.Ordinal);
        foreach (DatenKnoten team in Teams(Arbeitsstand))
        {
            if (double.TryParse(team.Lesen(TtrAttribut), NumberStyles.Float, CultureInfo.InvariantCulture, out double schnitt) && schnitt > 0)
            {
                string name = team.Lesen("teamname");
                ergebnis[name] = new Mannschaftsstaerke(name, schnitt, Math.Max(1, DelphiKompatibel.ZahlLesen(team.Lesen(StammspielerAttribut), 1)));
            }
        }

        return ergebnis;
    }

    /// <summary>Schreibt die Setzliste wie das Original (<c>FormToData</c>): der Knoten wird neu aufgebaut.</summary>
    /// <param name="daten">Die Setzliste.</param>
    public void SetzlisteSpeichern(Setzlistendaten daten)
    {
        ArgumentNullException.ThrowIfNull(daten);
        DatenKnoten ranking = Arbeitsstand.ErstesKind("ranking") ?? new DatenKnoten("ranking", Arbeitsstand);
        ranking.AttributeZuweisen(new DatenKnoten("ranking"));
        ranking.Kinder.Clear();
        ranking.Setzen("active", daten.Aktiv);
        for (int i = 0; i < daten.Reihenfolge.Count; i++)
        {
            var team = new DatenKnoten("team", ranking);
            team.Setzen("teamname", daten.Reihenfolge[i]);
            team.Setzen("rankingindex", i);
        }
    }

    private static string Vergleichsform(string name) => string.Concat(name.Where(char.IsLetterOrDigit)).ToUpperInvariant();
}
