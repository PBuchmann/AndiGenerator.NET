// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Schranke;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>
/// Dialog „B-Varianten“ (bildschirmfüllend): der aktuelle Plan und alle Alternativen aus HiGHS nebeneinander – je
/// A- und B-Kriterium die Verstöße mit Differenz zum aktuellen Plan, die Summen der Stufen und C vor dem Glätten. Darunter je
/// Mannschaft, welche Verstöße in der ausgewählten Variante dazukommen und welche entfallen. „Übernehmen“ legt A und B mit ihr fest.
/// </summary>
public sealed class AlternativendialogViewModel : ObservableObject
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private readonly Dictionary<Alternativspalte, IReadOnlyList<Vergleichsabschnitt>> details = [];
    private readonly Func<Planstand, Kostenkriterium, IReadOnlyList<Meldungsgruppe>> meldungen;
    private readonly Dictionary<Alternativspalte, (Planstand Plan, IReadOnlyList<Qualitaetskriterium> Kriterien)> plaene = [];
    private Alternativspalte? auswahl;
    private IReadOnlyList<Vergleichsabschnitt> auswahlDetails = [];

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="aktuell">Der angezeigte Plan.</param>
    /// <param name="alternativen">Die Alternativen mit ihrer Bewertung.</param>
    /// <param name="start">Die Alternative, die zuerst ausgewählt ist.</param>
    /// <param name="einteilung">Die Einteilung der Kriterien.</param>
    /// <param name="meldungen">Liefert zu einem Plan und Kriterium die Verstöße je Mannschaft.</param>
    /// <param name="kannUebernehmen">Übernehmen ist möglich (die Generierung läuft im Automodus).</param>
    /// <param name="hinweis">Hinweis unter der Erklärung, z. B. welche Kriterien HiGHS nicht kennt; leer = keiner.</param>
    public AlternativendialogViewModel(
        Planstand aktuell,
        IReadOnlyList<(Planalternative Alternative, Planbewertung Bewertung)> alternativen,
        Planalternative? start,
        Stufeneinteilung einteilung,
        Func<Planstand, Kostenkriterium, IReadOnlyList<Meldungsgruppe>> meldungen,
        bool kannUebernehmen,
        string hinweis = "")
    {
        ArgumentNullException.ThrowIfNull(aktuell);
        ArgumentNullException.ThrowIfNull(alternativen);
        ArgumentNullException.ThrowIfNull(einteilung);
        this.meldungen = meldungen ?? throw new ArgumentNullException(nameof(meldungen));
        KannUebernehmen = kannUebernehmen;
        Hinweis = hinweis ?? string.Empty;
        Stufeneinteilung voll = einteilung.Vervollstaendigt();

        var spalten = new List<Alternativspalte>();
        void Hinzu(string titel, Planalternative? alternative, Planstand plan)
        {
            IReadOnlyList<Qualitaetskriterium> k = Planqualitaet.Kriterien(plan.Bewertung, voll);
            var spalte = new Alternativspalte(titel, Summen(k), alternative, Auswaehlen);
            plaene[spalte] = (plan, k);
            spalten.Add(spalte);
        }

        Hinzu("Aktueller Plan", null, aktuell);
        foreach ((Planalternative alternative, Planbewertung bewertung) in alternativen)
        {
            Hinzu(HauptfensterViewModel.Alternativname(alternative), alternative, new Planstand(alternative.Plan, bewertung));
        }

        Spalten = spalten;
        Zeilen = ZeilenBilden(spalten);
        Auswaehlen(spalten.Find(s => s.Alternative is not null && ReferenceEquals(s.Alternative, start)) ?? spalten.Find(s => s.Alternative is not null) ?? spalten[0]);
    }

    /// <summary>Holt die Spalten: zuerst der aktuelle Plan, dann die Alternativen.</summary>
    public IReadOnlyList<Alternativspalte> Spalten { get; }

    /// <summary>Holt die Zeilen: je A- und B-Kriterium, die Summen der Stufen und C.</summary>
    public IReadOnlyList<Alternativzeile> Zeilen { get; }

    /// <summary>Holt einen Wert, der angibt, ob „Übernehmen“ möglich ist (Generierung im Automodus).</summary>
    public bool KannUebernehmen { get; }

    /// <summary>Holt einen Wert, der angibt, ob die Auswahl übernommen werden kann (eine Alternative, Generierung im Automodus).</summary>
    public bool UebernehmenMoeglich => KannUebernehmen && Gewaehlt is not null;

    /// <summary>Holt die ausgewählte Spalte.</summary>
    public Alternativspalte? Auswahl
    {
        get => auswahl;
        private set
        {
            if (SetProperty(ref auswahl, value))
            {
                OnPropertyChanged(nameof(Gewaehlt));
                OnPropertyChanged(nameof(UebernehmenText));
                OnPropertyChanged(nameof(UebernehmenMoeglich));
            }
        }
    }

    /// <summary>Holt die ausgewählte Alternative (für „Übernehmen“); <c>null</c>, wenn keine.</summary>
    public Planalternative? Gewaehlt => Auswahl?.Alternative;

    /// <summary>Holt die Beschriftung von „Übernehmen“, z. B. <c>„Heimkoppel bestmöglich“ übernehmen – A und B festlegen</c>.</summary>
    public string UebernehmenText => Auswahl?.Alternative is null ? "Übernehmen" : $"„{Auswahl.Titel}“ übernehmen – A und B festlegen";

    /// <summary>Holt den Hinweis unter der Erklärung (welche Kriterien HiGHS nicht kennt); leer, wenn keiner.</summary>
    public string Hinweis { get; }

    /// <summary>Holt einen Wert, der angibt, ob es einen Hinweis gibt.</summary>
    public bool HatHinweis => Hinweis.Length > 0;

    /// <summary>Holt die Erklärung oben im Dialog.</summary>
    public string Erklaerung => KannUebernehmen
        ? "Jede Variante ist in Stufe A bestmöglich – außer „A+B“: Dort zählt jeder Verstoß in A und B gleich, A darf für deutlich weniger B-Verstöße schlechter werden. „Vorrang …“ hält ein B-Kriterium so niedrig, wie HiGHS es allein geschafft hat (mit „bestmöglich“: bewiesen), "
            + "der Plan für die Summe in B hat die wenigsten B-Verstöße zusammen, die HiGHS gefunden hat („Plan B optimal“: bewiesen; kennt HiGHS nicht alle B-Kriterien, nur für die genannten). Grün: besser als der aktuelle Plan, rot: schlechter. "
            + "C ist in den Alternativen noch nicht geglättet – es wird nach dem Übernehmen besser. „Übernehmen“ legt A und B fest: Kein A- oder B-Kriterium "
            + "darf mehr Verstöße haben als in der gewählten Alternative, danach glättet der Automodus nur noch C. Die Einteilung bleibt."
        : "Übernehmen geht nur, solange die Generierung im Automodus läuft. Zum Vergleichen: Grün besser als der aktuelle Plan, rot schlechter; C ist in den Alternativen noch nicht geglättet.";

    /// <summary>Holt die Überschrift unter der Tabelle.</summary>
    public string DetailTitel => Auswahl switch
    {
        null => string.Empty,
        { IstAktuell: true } => "Betroffene Mannschaften – Aktueller Plan",
        _ when AuswahlDetails.Count == 0 => $"Unterschiede zum aktuellen Plan – {Auswahl.Titel}: in A und B keine",
        _ => $"Unterschiede zum aktuellen Plan – {Auswahl.Titel} (rot: kommt dazu, grün: entfällt)",
    };

    /// <summary>
    /// Holt je A- und B-Kriterium beim aktuellen Plan die betroffenen Mannschaften, bei einer Variante je Mannschaft, welche
    /// Verstöße gegenüber dem aktuellen Plan dazukommen und welche entfallen.
    /// </summary>
    public IReadOnlyList<Vergleichsabschnitt> AuswahlDetails
    {
        get => auswahlDetails;
        private set
        {
            if (SetProperty(ref auswahlDetails, value))
            {
                OnPropertyChanged(nameof(DetailTitel));
            }
        }
    }

    private static string Summen(IReadOnlyList<Qualitaetskriterium> k)
    {
        int Summe(char stufe) => k.Where(x => x.Stufe[0] == stufe && (stufe == 'C' || x.Kriterium is not null)).Sum(x => x.Anzahl ?? 0);
        return string.Create(Deutsch, $"A {Summe('A')} · B {Summe('B')} · C {Summe('C')}");
    }

    private List<Alternativzeile> ZeilenBilden(List<Alternativspalte> spalten)
    {
        IReadOnlyList<Qualitaetskriterium> basis = plaene[spalten[0]].Kriterien;
        Alternativzelle Zelle(Alternativspalte spalte, int wert, int bezug)
        {
            int d = wert - bezug;
            string text = spalte.IstAktuell || d == 0 ? wert.ToString(Deutsch) : string.Create(Deutsch, $"{wert} ({d:+0;−0})");
            return new Alternativzelle(text, spalte.IstAktuell ? 0 : Math.Sign(d), spalte);
        }

        var zeilen = new List<Alternativzeile>();
        foreach (char stufe in "AB")
        {
            for (int i = 0; i < basis.Count; i++)
            {
                if (basis[i].Stufe[0] == stufe && basis[i].Kriterium is not null)
                {
                    int bezug = basis[i].Anzahl ?? 0;
                    zeilen.Add(new Alternativzeile(basis[i].Stufe, basis[i].Name, false, [.. spalten.Select(s => Zelle(s, plaene[s].Kriterien[i].Anzahl ?? 0, bezug))]));
                }
            }

            int Summe(Alternativspalte s) => plaene[s].Kriterien.Where(x => x.Stufe[0] == stufe && x.Kriterium is not null).Sum(x => x.Anzahl ?? 0);
            int bezugSumme = Summe(spalten[0]);
            zeilen.Add(new Alternativzeile(string.Empty, $"{stufe} zusammen", true, [.. spalten.Select(s => Zelle(s, Summe(s), bezugSumme))]));
        }

        int C(Alternativspalte s) => plaene[s].Kriterien.Where(x => x.Stufe[0] == 'C').Sum(x => x.Anzahl ?? 0);
        int bezugC = C(spalten[0]);
        zeilen.Add(new Alternativzeile(string.Empty, "C zusammen (Alternativen noch nicht geglättet)", true, [.. spalten.Select(s => Zelle(s, C(s), bezugC))]));
        return zeilen;
    }

    /// <summary>Die Verstöße des aktuellen Plans je A- und B-Kriterium und Mannschaft.</summary>
    private List<Vergleichsabschnitt> Verstoesse((Planstand Plan, IReadOnlyList<Qualitaetskriterium> Kriterien) aktuell) =>
        aktuell.Kriterien
            .Where(k => k.Stufe[0] is 'A' or 'B' && k.Kriterium is not null && k.Anzahl > 0)
            .Select(k => new Vergleichsabschnitt(
                string.Create(Deutsch, $"{k.Stufe} · {k.Name} ({k.Anzahl})"),
                [.. meldungen(aktuell.Plan, k.Kriterium!.Value).Select(g => new Unterschiedsgruppe(g.Name, g.Anzahl, [.. g.Meldungen.Select(m => new Unterschied(m, 0))]))]))
            .Where(a => a.Gruppen.Count > 0)
            .ToList();

    /// <summary>
    /// Je A- und B-Kriterium und Mannschaft die Unterschiede einer Variante zum aktuellen Plan: Meldungen, die nur in der Variante
    /// stehen (kommen dazu), und solche, die nur im aktuellen Plan stehen (entfallen). Gleiche Meldungen fehlen.
    /// </summary>
    private List<Vergleichsabschnitt> Unterschiede(
        (Planstand Plan, IReadOnlyList<Qualitaetskriterium> Kriterien) aktuell,
        (Planstand Plan, IReadOnlyList<Qualitaetskriterium> Kriterien) variante)
    {
        var abschnitte = new List<Vergleichsabschnitt>();
        for (int i = 0; i < variante.Kriterien.Count; i++)
        {
            Qualitaetskriterium k = variante.Kriterien[i];
            int vorher = aktuell.Kriterien[i].Anzahl ?? 0;
            if (k.Stufe[0] is not ('A' or 'B') || k.Kriterium is not Kostenkriterium kriterium || (vorher == 0 && k.Anzahl is null or 0))
            {
                continue;
            }

            Dictionary<string, IReadOnlyList<string>> alt = meldungen(aktuell.Plan, kriterium).ToDictionary(g => g.Name, g => g.Meldungen);
            Dictionary<string, IReadOnlyList<string>> neu = meldungen(variante.Plan, kriterium).ToDictionary(g => g.Name, g => g.Meldungen);
            var gruppen = new List<Unterschiedsgruppe>();
            foreach (string mannschaft in alt.Keys.Concat(neu.Keys).Distinct().Order(StringComparer.CurrentCulture))
            {
                List<string> dazu = [.. neu.GetValueOrDefault(mannschaft) ?? []];
                List<string> weg = [.. alt.GetValueOrDefault(mannschaft) ?? []];

                // Gleiche Meldungen (auch mehrfach) heben sich auf.
                for (int j = weg.Count - 1; j >= 0; j--)
                {
                    if (dazu.Remove(weg[j]))
                    {
                        weg.RemoveAt(j);
                    }
                }

                if (dazu.Count + weg.Count > 0)
                {
                    string anzahl = string.Join(" · ", new[] { dazu.Count > 0 ? $"+{dazu.Count} neu" : null, weg.Count > 0 ? $"−{weg.Count} entfällt" : null }.OfType<string>());
                    gruppen.Add(new Unterschiedsgruppe(mannschaft, anzahl, [.. dazu.Select(m => new Unterschied("+ " + m, 1)), .. weg.Select(m => new Unterschied("− " + m, -1))]));
                }
            }

            if (gruppen.Count == 0 && vorher != (k.Anzahl ?? 0))
            {
                // Ohne Meldungen (oder mit gleichen) bleibt wenigstens die Anzahl: sonst stünde „keine Unterschiede“ da.
                int d = (k.Anzahl ?? 0) - vorher;
                gruppen.Add(new Unterschiedsgruppe("Ganzer Plan", string.Create(Deutsch, $"{d:+0;−0}"), [new Unterschied(string.Create(Deutsch, $"{vorher} → {k.Anzahl ?? 0} Verstöße (keine Einzelheiten)"), Math.Sign(d))]));
            }

            if (gruppen.Count > 0)
            {
                abschnitte.Add(new Vergleichsabschnitt(string.Create(Deutsch, $"{k.Stufe} · {k.Name}: {vorher} → {k.Anzahl ?? 0}"), gruppen));
            }
        }

        return abschnitte;
    }

    private void Auswaehlen(Alternativspalte spalte)
    {
        foreach (Alternativspalte s in Spalten ?? [])
        {
            s.IstGewaehlt = s == spalte;
        }

        spalte.IstGewaehlt = true;
        Auswahl = spalte;
        if (!details.TryGetValue(spalte, out IReadOnlyList<Vergleichsabschnitt>? liste))
        {
            liste = spalte.IstAktuell ? Verstoesse(plaene[spalte]) : Unterschiede(plaene.First(e => e.Key.IstAktuell).Value, plaene[spalte]);
            details[spalte] = liste;
        }

        AuswahlDetails = liste;
    }
}
