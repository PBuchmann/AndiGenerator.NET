// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Kern;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Inseln;

/// <summary>
/// Neustart feststeckender Inseln im Automodus: Eine Insel nimmt nur Verbesserungen an – findet sie lange keine, steckt sie in
/// einem Tal fest. Dann beginnt die schlechteste der feststeckenden Inseln neu, reihum mit einem anderen
/// <see cref="Startverfahren"/>: bester Plan mit einigen Spielen, einer Woche bzw. einer Mannschaft ohne Termin (die Insel
/// plant sie neu), ein Plan der Teilneuplanung, den HiGHS anders verteilt hat, oder selten ein leerer Plan. Ebenso beginnt
/// eine Insel neu, die nach einem Neustart in A oder B lange nicht zum besten Plan aufschließt.
/// </summary>
public sealed partial class Inseloptimierer
{
    /// <summary>So viele Tage umfasst das Zeitfenster, dessen Spiele beim Startverfahren „Zeitfenster“ neu geplant werden.</summary>
    private const int Fenstertage = 7;

    /// <summary>
    /// Eine Insel, die in A oder B schlechter ist als der beste Plan, beginnt spätestens nach so vielen Neustart-Grenzen neu, auch
    /// wenn sie noch vorankommt: Bis sie dort aufholt, trägt sie nichts bei.
    /// </summary>
    private const int Aufholgrenzen = 3;

    /// <summary>So lange (in Plänen der Inseln) taugt ein Plan der Teilneuplanung als Startplan.</summary>
    private const long TeilneuplanungFrisch = 5_000_000;

    /// <summary>So viele Suchschritte macht ein Stoß, die A und B nicht verschlechtern.</summary>
    private const int Stossschritte = 4;

    /// <summary>So viele Suchschritte versucht ein Stoß höchstens.</summary>
    private const int Stossversuche = 40;

    /// <summary>So viele andere gute Pläne merkt sich der Optimierer (je Herkunft der jüngste).</summary>
    private const int AnderePlaene = 8;

    /// <summary>
    /// Die Startverfahren reihum: vor allem kleine Stöße am besten Plan und Pläne der Teilneuplanung, seltener eine Mannschaft
    /// und nur jedes achte Mal ein leerer Plan (der braucht lange, bis er in A und B mithält).
    /// </summary>
    private static readonly Startverfahren[] Reihum =
    [
        Startverfahren.Spiele, Startverfahren.Zeitfenster, Startverfahren.Teilneuplanung, Startverfahren.Mannschaften,
        Startverfahren.Spiele, Startverfahren.Zeitfenster, Startverfahren.Teilneuplanung, Startverfahren.Leer,
    ];

    /// <summary>
    /// Die Startverfahren reihum bei später Annahme: vor allem Stöße, dazwischen Pläne aus anderen Gegenden (Teilneuplanung,
    /// HiGHS) und der beste Plan selbst – sucht dort schon eine andere Insel, stattdessen ein anderer guter Plan.
    /// </summary>
    private static readonly Startverfahren[] ReihumSpaet =
    [
        Startverfahren.Stoss, Startverfahren.Teilneuplanung, Startverfahren.Stoss, Startverfahren.Anderer, Startverfahren.Stoss, Startverfahren.Bester,
    ];

    /// <summary>Der Suchschritt, mit dem ein Stoß den besten Plan verändert (2 % der Spiele neu verteilt).</summary>
    private static readonly Tauschstrategie Stossschritt = Tauschstrategie.Lesen("2");

    /// <summary>Je Insel, womit sie zuletzt neu begonnen hat (<c>null</c>: noch nicht im Automodus).</summary>
    private readonly Startverfahren?[] startverfahren;

    /// <summary>Je Spiel, ob es einen festen Termin hat (bleibt bei jedem Startverfahren).</summary>
    private readonly bool[] festeSpiele;

    /// <summary>Je Startverfahren die Neustarts und die Verbesserungen des besten Plans danach.</summary>
    private readonly Dictionary<Startverfahren, (int Starts, int Erfolge)> startstatistik = [];

    /// <summary>Andere gute Pläne, die nicht bester Plan wurden, je Herkunft der jüngste (Zugriff unter der Sperre).</summary>
    private readonly Dictionary<string, Startkandidat> anderePlaene = [];

    /// <summary>Das nächste Startverfahren (reihum).</summary>
    private int naechstesVerfahren;

    /// <summary>Ein Plan der Teilneuplanung als Startplan; <c>null</c>, wenn keiner.</summary>
    private Startkandidat? teilneuplanungsStart;

    /// <summary>
    /// Bietet einen Plan der Teilneuplanung als Startplan einer Insel an: HiGHS hielt ihn im Modell für besser, die Bewertung
    /// nicht. In A und B ist er so gut wie der beste Plan, anders verteilt – ein guter Ausgangspunkt für eine Insel, die
    /// feststeckt. Nur der jüngste zählt.
    /// </summary>
    /// <param name="plan">Der Plan.</param>
    public void StartplanAnbieten(IReadOnlyList<Spiel> plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Count > 0)
        {
            // Erst beim Neustart umgewandelt und geprüft (selten): hier nur gemerkt.
            Volatile.Write(ref teilneuplanungsStart, new Startkandidat(plan, Durchlaeufe));
        }
    }

    /// <summary>
    /// Die Insel, die als Nächste neu beginnt: die schlechteste (nach ihren Kosten, beim Glätten also nach Verstößen), bei
    /// Gleichstand die, die am längsten nichts gefunden hat – unter denen, die feststecken (oder allen).
    /// </summary>
    /// <param name="nurFeststeckende">Nur Inseln, die länger als die Grenze nichts Besseres gefunden haben.</param>
    /// <returns>Ihr Index; −1, wenn keine.</returns>
    private int NeustartKandidat(bool nurFeststeckende = true)
    {
        int kandidat = -1;
        for (int i = 0; i < inseln.Length; i++)
        {
            Insel insel = inseln[i];
            if (insel.Kosten < 0 || (nurFeststeckende && !Feststeckend(i)))
            {
                continue;
            }

            if (kandidat < 0 || (insel.Kosten, insel.OhneVerbesserung).CompareTo((inseln[kandidat].Kosten, inseln[kandidat].OhneVerbesserung)) > 0)
            {
                kandidat = i;
            }
        }

        return inseln.Length < 2 ? -1 : kandidat;
    }

    /// <summary>
    /// Ob eine Insel neu beginnen soll: Sie hat länger als die Grenze nichts Besseres gefunden, oder sie ist in A oder B (bzw. bei
    /// den harten Fehlern) noch immer schlechter als der beste Plan, obwohl sie seit ihrem Neustart schon ein Mehrfaches der
    /// Grenze gerechnet hat.
    /// </summary>
    private bool Feststeckend(int i)
    {
        Insel insel = inseln[i];
        long grenze = einstellungen.DurchlaeufeVorNeustart;
        if (insel.OhneVerbesserung > grenze)
        {
            return true;
        }

        Stufenwert bester = auto!.BesterWert;
        return insel.SeitNeustart > Aufholgrenzen * grenze && autoWerte[i] is { } w && (w.Harte, w.A, w.B).CompareTo((bester.Harte, bester.A, bester.B)) > 0;
    }

    /// <summary>
    /// Späte Annahme: Die Suchplätze dürfen A und B nie schlechter machen als im besten Plan (harte Fehler, A, B als Grenze im
    /// Zählziel).
    /// </summary>
    private void GrenzeSetzen()
    {
        if (zaehlziel is { Spaetannahme: > 0 } ziel && auto is { Grundlauf: false })
        {
            Stufenwert bester = auto.BesterWert;
            ziel.Grenze = (bester.Harte, bester.A, bester.B);
        }
    }

    /// <summary>
    /// Merkt einen Plan, der nicht bester Plan wurde, als Startplan für eine Insel (<see cref="Startverfahren.Anderer"/>): je
    /// Herkunft der jüngste, höchstens <see cref="AnderePlaene"/>. Erst beim Neustart umgewandelt und geprüft.
    /// </summary>
    private void AndererPlan(IReadOnlyList<Spiel> plan, string herkunft)
    {
        anderePlaene[herkunft] = new Startkandidat(plan, Durchlaeufe);
        if (anderePlaene.Count > AnderePlaene)
        {
            anderePlaene.Remove(anderePlaene.MinBy(e => e.Value.Bei).Key);
        }
    }

    /// <summary>
    /// Ein anderer guter Plan als Startplan: frisch, ohne mehr harte Fehler, in A so gut wie der beste Plan, in B höchstens ein
    /// Zehntel (mindestens 2) schlechter – der beste davon in A und B. Er wird danach vergessen (sonst begännen mehrere Inseln
    /// mit ihm).
    /// </summary>
    /// <returns>Der Plan oder <c>null</c>.</returns>
    private Loesung? AndererStart()
    {
        Stufenwert bester = auto!.BesterWert;
        int spielraum = Math.Max(2, bester.B / 10);
        (string Herkunft, Loesung Plan, Stufenwert Wert)? gewaehlt = null;
        foreach ((string herkunft, Startkandidat k) in anderePlaene.ToList())
        {
            if (Durchlaeufe - k.Bei > TeilneuplanungFrisch)
            {
                anderePlaene.Remove(herkunft);
                continue;
            }

            Loesung plan = AlsLoesung(k.Plan);
            Stufenwert w = auto.NachStufen(plan);
            if (w.Harte > bester.Harte || w.A > bester.A || w.B > bester.B + spielraum || w == bester)
            {
                anderePlaene.Remove(herkunft);
            }
            else if (gewaehlt is not { } g || (w.A, w.B).CompareTo((g.Wert.A, g.Wert.B)) < 0)
            {
                gewaehlt = (herkunft, plan, w);
            }
        }

        if (gewaehlt is not { } ergebnis)
        {
            return null;
        }

        anderePlaene.Remove(ergebnis.Herkunft);
        return ergebnis.Plan;
    }

    /// <summary>Ob eine andere Insel als <paramref name="i"/> gerade genau beim besten Plan sucht (gleiche Werte).</summary>
    private bool AndereBeimBesten(int i)
    {
        Stufenwert bester = auto!.BesterWert;
        for (int j = 0; j < inseln.Length; j++)
        {
            if (j != i && autoWerte[j] == bester)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Startet eine Insel mit dem nächsten Startverfahren neu.</summary>
    private void Neustarten(int i)
    {
        // Mit später Annahme braucht es keine großen Umbauten: Die Insel beginnt beim besten Plan, einem kleinen Stoß daran oder
        // einem Plan der Teilneuplanung, und die Plätze gehen von dort ihre eigenen Wege.
        Startverfahren[] reihum = zaehlziel is { Spaetannahme: > 0 } ? ReihumSpaet : Reihum;
        Startverfahren verfahren = reihum[naechstesVerfahren % reihum.Length];
        naechstesVerfahren = (naechstesVerfahren + 1) % (Reihum.Length * ReihumSpaet.Length);

        // Steht schon eine andere Insel beim besten Plan, beginnt diese mit einem anderen guten Plan (sonst einem Stoß): Eine
        // weitere Kopie des besten Plans brächte kaum Neues (Verbandsoberliga: viermal 10·31·319 über Minuten).
        if (verfahren == Startverfahren.Bester && AndereBeimBesten(i))
        {
            verfahren = Startverfahren.Anderer;
        }

        Loesung start = Startplan(ref verfahren);
        inseln[i].Neustarten(start, ReferenceEquals(start, leer) ? -1 : Bewerten(bewerter, start));
        autoStand[i] = -1;
        startverfahren[i] = verfahren;
        (int starts, int erfolge) = startstatistik.GetValueOrDefault(verfahren);
        startstatistik[verfahren] = (starts + 1, erfolge);
    }

    /// <summary>Der Startplan eines Verfahrens; gibt es keinen Plan der Teilneuplanung, wird es „Mannschaften“.</summary>
    private Loesung Startplan(ref Startverfahren verfahren)
    {
        if (verfahren == Startverfahren.Teilneuplanung)
        {
            // Nur ein frischer Plan, der in A und B so gut ist wie der beste.
            if (Interlocked.Exchange(ref teilneuplanungsStart, null) is { } t && Durchlaeufe - t.Bei <= TeilneuplanungFrisch)
            {
                Loesung plan = AlsLoesung(t.Plan);
                Stufenwert wert = auto!.NachStufen(plan);
                Stufenwert bester = auto.BesterWert;
                if ((wert.Harte, wert.A, wert.B).CompareTo((bester.Harte, bester.A, bester.B)) <= 0)
                {
                    return plan;
                }
            }

            verfahren = zaehlziel is { Spaetannahme: > 0 } ? Startverfahren.Bester : Startverfahren.Mannschaften;
            if (verfahren == Startverfahren.Bester)
            {
                return auto!.Beste.Kopie();
            }
        }

        if (verfahren == Startverfahren.Anderer)
        {
            if (AndererStart() is { } anderer)
            {
                return anderer;
            }

            verfahren = Startverfahren.Stoss;
        }

        if (verfahren == Startverfahren.Leer)
        {
            return leer;
        }

        if (verfahren == Startverfahren.Bester)
        {
            return auto!.Beste.Kopie();
        }

        if (verfahren == Startverfahren.Stoss)
        {
            return Stossen();
        }

        // Bester Plan; die freigegebenen Spiele verlieren ihren Termin, die Insel plant sie neu.
        Loesung start = auto!.Beste.Kopie();
        Func<int, bool> frei = verfahren switch
        {
            Startverfahren.Zeitfenster => Zeitfenster(start),
            Startverfahren.Spiele => EinigeSpiele(start),
            _ => Mannschaften(),
        };
        for (int s = 0; s < start.Datum.Length; s++)
        {
            if (!festeSpiele[s] && frei(s))
            {
                start.Datum[s] = 0.0;
                start.Optionen[s] = default;
                start.MaxHeim[s] = 0;
                start.Wunsch[s] = -1;
                start.NichtNotwendig[s] = false;
            }
        }

        return start;
    }

    /// <summary>Eine zufällige Mannschaft: ihre Spiele.</summary>
    private Func<int, bool> Mannschaften()
    {
        int mannschaft = koordinatorZufall.Next(definition.N);
        return s => definition.SpielHeim[s] == mannschaft || definition.SpielGast[s] == mannschaft;
    }

    /// <summary>Einige zufällige Spiele (drei bis acht) mit Termin.</summary>
    private Func<int, bool> EinigeSpiele(Loesung plan)
    {
        int[] kandidaten = [.. Enumerable.Range(0, plan.Datum.Length).Where(s => plan.Datum[s] > 0 && !festeSpiele[s])];
        koordinatorZufall.Shuffle(kandidaten);
        var gewaehlt = new HashSet<int>(kandidaten.Take(3 + koordinatorZufall.Next(6)));
        return gewaehlt.Contains;
    }

    /// <summary>Eine Woche ab dem Termin eines zufälligen Spiels: alle Spiele darin.</summary>
    private Func<int, bool> Zeitfenster(Loesung plan)
    {
        double[] termine = [.. plan.Datum.Where((d, s) => d > 0 && !festeSpiele[s])];
        if (termine.Length == 0)
        {
            return _ => true;
        }

        double von = termine[koordinatorZufall.Next(termine.Length)];
        return s => plan.Datum[s] >= von && plan.Datum[s] < von + Fenstertage;
    }

    /// <summary>Zählt, wenn eine Insel den besten Plan verbessert hat, für ihr Startverfahren.</summary>
    private void ErfolgZaehlen(int i, bool besser)
    {
        if (besser && startverfahren[i] is { } verfahren)
        {
            (int starts, int erfolge) = startstatistik.GetValueOrDefault(verfahren);
            startstatistik[verfahren] = (starts, erfolge + 1);
        }
    }

    private Startstatistik[] Startstatistiken() =>
        [.. Enum.GetValues<Startverfahren>().Where(startstatistik.ContainsKey).Select(v => new Startstatistik(v, startstatistik[v].Starts, startstatistik[v].Erfolge))];

    /// <summary>
    /// Stoß am besten Plan: einige Suchschritte, die A und B nicht verschlechtern (C darf schlechter werden). Die Spiele werden
    /// gleich wieder terminiert – kein Spiel bleibt ohne Termin –, und die Insel beginnt in A und B so gut wie der beste Plan,
    /// nur anders verteilt (ein Stoß, der A verschlechtert, ließe sie lange mit Verstößen in A suchen).
    /// </summary>
    private Loesung Stossen()
    {
        var zufall = new Zufall(koordinatorZufall.Next());
        Stufenwert bester = auto!.BesterWert;
        Loesung plan = auto.Beste.Kopie();
        int schritte = 0;
        for (int versuch = 0; versuch < Stossversuche && schritte < Stossschritte; versuch++)
        {
            bewerter.LoesungUebernehmen(plan);
            bewerter.Zuruecksetzen();
            bewerter.VorgegebeneSpieleSetzen();
            bewerter.NeuWuerfeln(Stossschritt, zufall);
            bewerter.TermineFuellen(zufall);
            bewerter.Merken();
            var gestossen = new Loesung(bewerter.AnzahlErlaubt);
            bewerter.LoesungSchreiben(gestossen);
            Stufenwert wert = auto.NachStufen(gestossen);
            if ((wert.Harte, wert.A, wert.B).CompareTo((bester.Harte, bester.A, bester.B)) <= 0)
            {
                plan = gestossen;
                schritte++;
            }
        }

        return plan;
    }

    /// <summary>Ein Plan der Teilneuplanung als möglicher Startplan.</summary>
    /// <param name="Plan">Der Plan.</param>
    /// <param name="Bei">Die Durchläufe der Inseln, als er kam.</param>
    private sealed record Startkandidat(IReadOnlyList<Spiel> Plan, long Bei);
}
