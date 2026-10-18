// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Kern;

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

    /// <summary>
    /// Die Startverfahren reihum: vor allem kleine Stöße am besten Plan und Pläne der Teilneuplanung, seltener eine Mannschaft
    /// und nur jedes achte Mal ein leerer Plan (der braucht lange, bis er in A und B mithält).
    /// </summary>
    private static readonly Startverfahren[] Reihum =
    [
        Startverfahren.Spiele, Startverfahren.Zeitfenster, Startverfahren.Teilneuplanung, Startverfahren.Mannschaften,
        Startverfahren.Spiele, Startverfahren.Zeitfenster, Startverfahren.Teilneuplanung, Startverfahren.Leer,
    ];

    /// <summary>Je Insel, womit sie zuletzt neu begonnen hat (<c>null</c>: noch nicht im Automodus).</summary>
    private readonly Startverfahren?[] startverfahren;

    /// <summary>Je Spiel, ob es einen festen Termin hat (bleibt bei jedem Startverfahren).</summary>
    private readonly bool[] festeSpiele;

    /// <summary>Je Startverfahren die Neustarts und die Verbesserungen des besten Plans danach.</summary>
    private readonly Dictionary<Startverfahren, (int Starts, int Erfolge)> startstatistik = [];

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

    /// <summary>Startet eine Insel mit dem nächsten Startverfahren neu.</summary>
    private void Neustarten(int i)
    {
        Startverfahren verfahren = Reihum[naechstesVerfahren];
        naechstesVerfahren = (naechstesVerfahren + 1) % Reihum.Length;
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

            verfahren = Startverfahren.Mannschaften;
        }

        if (verfahren == Startverfahren.Leer)
        {
            return leer;
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

    /// <summary>Ein Plan der Teilneuplanung als möglicher Startplan.</summary>
    /// <param name="Plan">Der Plan.</param>
    /// <param name="Bei">Die Durchläufe der Inseln, als er kam.</param>
    private sealed record Startkandidat(IReadOnlyList<Spiel> Plan, long Bei);
}
