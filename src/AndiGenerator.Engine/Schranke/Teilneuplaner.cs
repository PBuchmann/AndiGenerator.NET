// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Engine.Schranke;

/// <summary>
/// Teilneuplanung im Automodus (MIGRATIONSPLAN O7 Schritt 3): Nach der Basisoptimierung plant sie fortlaufend Ausschnitte des
/// besten Plans mit HiGHS neu (<see cref="Ligaschranke.Neuplanen"/>). Ein besserer Ausschnitt geht sofort an den Automodus:
/// Ist der Plan nach seinen Regeln besser, wird er bester Plan, und die schlechteste Insel rechnet von ihm aus weiter. Haben
/// die Inseln inzwischen einen besseren Plan, wird dieser vor dem nächsten Ausschnitt ihr neuer Ausgangsplan. So gibt es
/// immer nur einen besten Plan, an dem Inseln und HiGHS gemeinsam arbeiten. Läuft auf einem eigenen Faden neben den Inseln.
/// </summary>
public sealed class Teilneuplaner : IDisposable
{
    /// <summary>So oft wird gefragt, ob die Basisoptimierung vorbei ist.</summary>
    private static readonly TimeSpan Warten = TimeSpan.FromSeconds(1);

    /// <summary>So lange wird nach der Basisoptimierung gewartet, bevor die Teilneuplanung beginnt.</summary>
    private static readonly TimeSpan NachDerBasis = TimeSpan.FromSeconds(5);

    /// <summary>Zeitgrenze für HiGHS je Ausschnitt mit zwei Mannschaften (größere Ausschnitte entsprechend länger).</summary>
    private static readonly TimeSpan JeAusschnitt = TimeSpan.FromSeconds(5);

    /// <summary>
    /// So oft wird verglichen, ob Inseln oder Teilneuplanung je Rechen-Thread mehr verbessern (danach rechnet eine Teilneuplanung
    /// mehr oder weniger).
    /// </summary>
    private static readonly TimeSpan Anpassabstand = TimeSpan.FromSeconds(30);

    /// <summary>So oft wird in der Pause gefragt, ob es weitergeht.</summary>
    private static readonly TimeSpan PauseAbfrage = TimeSpan.FromMilliseconds(300);

    /// <summary>So lange wartet <see cref="Dispose"/> höchstens auf das Ende.</summary>
    private static readonly TimeSpan Abschied = TimeSpan.FromSeconds(3);

    /// <summary>So lange rechnet ein Lauf einer HiGHS-Linie, danach beginnt sie mit neuem Startwert an ihrem besten Plan.</summary>
    private static readonly TimeSpan Linienlauf = TimeSpan.FromMinutes(20);

    private readonly Inseloptimierer optimierer;
    private readonly Staffel staffel;
    private readonly Berechnungsoptionen optionen;
    private readonly ISchrankenloeser loeser;
    private readonly Stufeneinteilung einteilung;
    private readonly IReadOnlyDictionary<Kostenkriterium, int>? obergrenzen;
    private readonly Func<bool> warten;
    private readonly CancellationTokenSource abbruch = new();
    private readonly Lock sperre = new();
    private readonly List<Ausschnittsergebnis> verlauf = [];
    private readonly SortedDictionary<int, (int Ausschnitte, int Besser, double Sekunden)> groessen = [];
    private readonly int arbeiter;
    private readonly Ausschnittsbelegung belegung = new();
    private readonly System.Diagnostics.Stopwatch anpassUhr = new();
    private readonly Func<IReadOnlyList<Spiel>, IReadOnlyDictionary<string, int>> verstoesse;
    private readonly Func<int, ISchrankenloeser>? linienloeser;
    private readonly (IReadOnlyList<Spiel> Plan, Stufenwert? Wert)?[] linienBester;
    private readonly int linienArbeiter;
    private Task? lauf;
    private int erlaubt;
    private (int Inseln, int Eigene) zuletztGezaehlt;
    private (double Inseln, double InselSekunden, double Eigene, double EigeneSekunden) erfolge;
    private int beendet;
    private string zustand = "wartet auf das Ende der Basisoptimierung";
    private int ausschnitte;
    private int besser;
    private int eingespielt;
    private int wechsel;
    private int rechnende;
    private int? mannschaften;
    private Stufenwert? aktuell;

    /// <summary>Legt die Teilneuplanung für eine laufende Generierung an.</summary>
    /// <param name="optimierer">Die Generierung (Automodus).</param>
    /// <param name="staffel">Die Staffel wie für die Generierung.</param>
    /// <param name="optionen">Die Gewichtungen des Anwenders.</param>
    /// <param name="loeser">HiGHS.</param>
    /// <param name="einteilung">Die Einteilung der Kriterien.</param>
    /// <param name="obergrenzen">Festlegung des Staffelleiters (je Kriterium höchstens so viele Verstöße), oder <c>null</c>.</param>
    /// <param name="warten">Solange es <c>true</c> liefert, wartet die Teilneuplanung (Vorrang für HiGHS), oder <c>null</c>.</param>
    /// <param name="arbeiter">
    /// So viele Teilneuplanungen rechnen beim Glätten von C gleichzeitig (eine schon vorher); <c>null</c> = bis zu drei Viertel der
    /// Kerne (anfangs die Hälfte, mehr, wenn sie je Kern mehr bringt als die Inseln). Je laufender Rechnung ruht ein Rechen-Thread der Inseln.
    /// </param>
    /// <param name="linienloeser">
    /// Der Löser je HiGHS-Linie (Nummer des Laufs, für verschiedene Startwerte), oder <c>null</c> = keine Linien.
    /// </param>
    /// <param name="linien">
    /// So viele HiGHS-Linien (<see cref="Ligaschranke.Linie"/>): lange Läufe für A und B ab dem Start der Generierung, je
    /// zwei Teilneuplanungen verfeinern ihre Pläne (sie rechnen von Anfang an, nicht erst beim Glätten).
    /// </param>
    public Teilneuplaner(Inseloptimierer optimierer, Staffel staffel, Berechnungsoptionen optionen, ISchrankenloeser loeser, Stufeneinteilung einteilung, IReadOnlyDictionary<Kostenkriterium, int>? obergrenzen = null, Func<bool>? warten = null, int? arbeiter = null, Func<int, ISchrankenloeser>? linienloeser = null, int linien = 0)
    {
        this.linienloeser = linienloeser;
        linienBester = new (IReadOnlyList<Spiel>, Stufenwert?)?[linienloeser is null ? 0 : Math.Max(0, linien)];

        // Höchstens die Hälfte der Kerne (die andere Hälfte bleibt den Inseln).
        this.arbeiter = Math.Max(1 + (2 * linienBester.Length), arbeiter ?? (Environment.ProcessorCount / 2));
        linienArbeiter = 2 * linienBester.Length;
        erlaubt = this.arbeiter;
        verstoesse = Ligaschranke.VerstoesseInC(staffel, optionen, einteilung);
        this.warten = warten ?? (() => false);
        this.optimierer = optimierer;
        this.staffel = staffel;
        this.optionen = optionen;
        this.loeser = loeser;
        this.einteilung = einteilung;
        this.obergrenzen = obergrenzen;
    }

    /// <summary>Holt den Stand für den Leitstand.</summary>
    public Teilneuplanungsstand Stand
    {
        get
        {
            lock (sperre)
            {
                string linienText = linienBester.Length == 0 ? string.Empty : $" · {linienBester.Length} HiGHS-Linien";
                return new Teilneuplanungsstand(
                    (rechnende > 1 ? $"{zustand} ({rechnende} von {arbeiter} gleichzeitig)" : zustand) + linienText,
                    ausschnitte,
                    besser,
                    eingespielt,
                    wechsel,
                    rechnende > 0,
                    mannschaften,
                    [.. verlauf],
                    aktuell,
                    groessen.Select(g => new Ausschnittsgroesse(g.Key, g.Value.Ausschnitte, g.Value.Besser, g.Value.Sekunden)).ToArray());
            }
        }
    }

    /// <summary>Beginnt im Hintergrund: eine Teilneuplanung sofort, die weiteren, sobald der Automodus C glättet.</summary>
    public void Starten() => lauf = Task.WhenAll(Enumerable.Range(0, arbeiter).Select(i => Task.Run(() => Schleife(i), CancellationToken.None))
        .Concat(Enumerable.Range(0, linienBester.Length).Select(i => Task.Run(() => Linie(i), CancellationToken.None))));

    /// <summary>
    /// Beendet die Teilneuplanung: HiGHS bricht seinen Lauf ab, danach wird nichts mehr eingespielt. Wartet kurz, bis sie
    /// beendet ist (höchstens <see cref="Abschied"/>), damit nicht zwei Läufe gleichzeitig rechnen.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref beendet, 1) == 1)
        {
            return;
        }

        abbruch.Cancel();
        if (lauf is null || lauf.Wait(Abschied, CancellationToken.None))
        {
            abbruch.Dispose();
        }
    }

    private async Task Schleife(int nummer)
    {
        CancellationToken token = abbruch.Token;
        var uhr = new System.Diagnostics.Stopwatch();
        try
        {
            // Erst nach der Basisoptimierung (dann gibt es den Maßstab des Automodus).
            (IReadOnlyList<Spiel> Plan, Stufenwert Wert)? start;
            while ((start = optimierer.BesterGelenkt()) is null)
            {
                Setzen("wartet auf das Ende der Basisoptimierung");
                await Task.Delay(Warten, token).ConfigureAwait(false);
            }

            // Gleich nach der Basisoptimierung kurz warten: Dann ist ein fertiger Plan aus HiGHS meist schon eingespielt. Rechnet
            // HiGHS noch an A und B, so lange.
            await Task.Delay(NachDerBasis, token).ConfigureAwait(false);
            while (warten())
            {
                Setzen("wartet auf HiGHS (Stufen A und B)");
                await Task.Delay(Warten, token).ConfigureAwait(false);
            }

            start = optimierer.BesterGelenkt() ?? start;

            // Die weiteren erst beim Glätten von C: Dann sind A und B entschieden, und Ausschnitte bringen am meisten. Die
            // Teilneuplanungen der HiGHS-Linien beginnen, sobald ihre Linie einen Plan hat, der nicht schlechter ist als der beste.
            int? linie = Linienindex(nummer);
            while (nummer > 0 && linie is null && !optimierer.Glaettet)
            {
                await Task.Delay(Warten, token).ConfigureAwait(false);
            }

            start = optimierer.BesterGelenkt() ?? start;
            while (linie is int l && LinieBereit(l) is null)
            {
                await Task.Delay(Warten, token).ConfigureAwait(false);
            }

            if (linie is int l2 && LinieBereit(l2) is { } bereit)
            {
                start = bereit;
            }

            // Fortlaufend bis zum Ende der Generierung; angenommen wird nach dem Maßstab des Automodus (mit C-Ausreißern).
            var e = new Neuplanungseinstellungen(
                TimeSpan.MaxValue,
                JeAusschnitt,
                Bewerten: optimierer.NachStufen,
                Besser: optimierer.IstBesser,
                Startwert: nummer + 1,
                Obergrenzen: obergrenzen,
                Ausschnitt: m => Ausschnitt(m, uhr),
                Verbessert: (plan, wert) =>
                {
                    if (linie is int l)
                    {
                        LinieMerken(l, plan, wert);
                    }

                    Verbessert(plan);
                },
                Abgelehnt: optimierer.StartplanAnbieten,
                Ausgangsplan: eigener => linie is int l ? LinienAusgangsplan(l, eigener) : Ausgangsplan(eigener, nummer),
                Unterbrechen: () => linie is null ? Ruht(nummer) : optimierer.Pausiert,
                Verstoesse: verstoesse,
                Belegung: belegung,
                Platz: nummer);
            Neuplanungsbericht bericht;
            try
            {
                if (nummer == 0)
                {
                    Setzen("plant Ausschnitte neu");
                }

                Rechnen(true);
                bericht = Ligaschranke.Neuplanen(staffel, start.Value.Plan, optionen, loeser, einteilung, e, abbruch: token);
            }
            finally
            {
                Rechnen(false);
            }

            if (bericht.Schritte == 0 && !token.IsCancellationRequested)
            {
                // Für diese Staffel gibt es kein Modell (oder der Plan passt nicht hinein).
                Setzen("nicht möglich: " + string.Join(" ", bericht.Protokoll));
            }
        }
        catch (Exception fehler) when (fehler is OperationCanceledException or ObjectDisposedException)
        {
            // Beendet.
        }
        catch (Exception fehler) when (fehler is InvalidOperationException or ArgumentException or IndexOutOfRangeException
            or NullReferenceException or DllNotFoundException or EntryPointNotFoundException)
        {
            // Nicht still verschwinden: Der Leitstand zeigt den Fehler.
            Setzen("Fehler: " + fehler.Message);
        }
    }

    /// <summary>Die Linie, der eine Teilneuplanung folgt (Nummern 1 bis 2 · Linien), sonst <c>null</c>.</summary>
    private int? Linienindex(int nummer) => nummer >= 1 && nummer <= linienArbeiter ? (nummer - 1) % linienBester.Length : null;

    /// <summary>Der beste Plan einer Linie, sobald er nach dem Maßstab des Automodus bewertet ist und A und B einhält.</summary>
    private (IReadOnlyList<Spiel> Plan, Stufenwert Wert)? LinieBereit(int linie)
    {
        (IReadOnlyList<Spiel> Plan, Stufenwert? Wert)? b;
        lock (sperre)
        {
            b = linienBester[linie];
        }

        if (b is not { } x || (x.Wert ?? optimierer.NachStufen(x.Plan)) is not { } wert)
        {
            return null;
        }

        return optimierer.BesterGelenkt() is { } gesamt && (gesamt.Wert.Harte, gesamt.Wert.A).CompareTo((wert.Harte, wert.A)) < 0 ? null : (x.Plan, wert);
    }

    /// <summary>Merkt einen Plan der Linie (aus HiGHS oder ihrer Teilneuplanung), wenn er besser ist.</summary>
    private void LinieMerken(int linie, IReadOnlyList<Spiel> plan, Stufenwert? wert)
    {
        wert ??= optimierer.NachStufen(plan);
        lock (sperre)
        {
            if (linienBester[linie] is not { } alt || alt.Wert is not { } altWert || wert is not { } neu || optimierer.IstBesser(neu, altWert))
            {
                linienBester[linie] = (plan, wert);
            }
        }
    }

    /// <summary>Vor jedem Ausschnitt einer Linien-Teilneuplanung: Hat ihre Linie einen besseren Plan, wird er Ausgangsplan.</summary>
    private IReadOnlyList<Spiel>? LinienAusgangsplan(int linie, Stufenwert eigener)
    {
        while (optimierer.Pausiert && !abbruch.IsCancellationRequested)
        {
            abbruch.Token.WaitHandle.WaitOne(PauseAbfrage);
        }

        return LinieBereit(linie) is { } b && optimierer.IstBesser(b.Wert, eigener) ? b.Plan : null;
    }

    /// <summary>
    /// Eine HiGHS-Linie: lange Läufe für A und B (je <see cref="Linienlauf"/>, danach mit neuem Startwert an ihrem besten Plan),
    /// jeder bessere Plan geht sofort an den Automodus. In der Pause ruht sie.
    /// </summary>
    private async Task Linie(int linie)
    {
        CancellationToken token = abbruch.Token;
        try
        {
            int runde = 0;
            while (!token.IsCancellationRequested)
            {
                while (optimierer.Pausiert)
                {
                    await Task.Delay(PauseAbfrage, token).ConfigureAwait(false);
                }

                ISchrankenloeser l = linienloeser!(linie + (runde * linienBester.Length));
                runde++;
                bool fertig = Ligaschranke.Linie(
                    staffel,
                    optionen,
                    l,
                    einteilung,
                    () =>
                    {
                        lock (sperre)
                        {
                            return linienBester[linie]?.Plan;
                        }
                    },
                    plan =>
                    {
                        LinieMerken(linie, plan, null);
                        optimierer.PlanEinspielen(plan, $"HiGHS-Linie {linie + 1}");
                    },
                    Linienlauf,
                    () => token.IsCancellationRequested || optimierer.Pausiert);
                if (fertig)
                {
                    // Bewiesen bestmöglich (kleine Staffel): Die Linie ist fertig, ihre Teilneuplanungen verfeinern C weiter.
                    return;
                }
            }
        }
        catch (Exception fehler) when (fehler is OperationCanceledException or ObjectDisposedException)
        {
            // Beendet.
        }
        catch (Exception fehler) when (fehler is InvalidOperationException or ArgumentException or IndexOutOfRangeException
            or NullReferenceException or DllNotFoundException or EntryPointNotFoundException)
        {
            Setzen($"Fehler in HiGHS-Linie {linie + 1}: " + fehler.Message);
        }
    }

    /// <summary>Ein angenommener Ausschnitt: sofort an den Automodus.</summary>
    private void Verbessert(IReadOnlyList<Spiel> plan)
    {
        if (abbruch.IsCancellationRequested || !optimierer.PlanEinspielen(plan, "Teilneuplanung"))
        {
            return;
        }

        lock (sperre)
        {
            eingespielt++;
        }
    }

    /// <summary>
    /// Vor jedem Ausschnitt: in der Pause und während HiGHS an A und B rechnet warten; haben die Inseln einen besseren Plan als die Teilneuplanung, wird er ihr neuer
    /// Ausgangsplan.
    /// </summary>
    private IReadOnlyList<Spiel>? Ausgangsplan(Stufenwert eigener, int nummer)
    {
        lock (sperre)
        {
            aktuell = eigener;
            if (nummer == 0)
            {
                Anpassen();
            }
        }

        if (Ruht(nummer))
        {
            if (nummer == 0)
            {
                Setzen(optimierer.Pausiert ? "angehalten" : "wartet auf HiGHS (Stufen A und B)");
            }

            Rechnen(false);
            while (Ruht(nummer) && !abbruch.IsCancellationRequested)
            {
                abbruch.Token.WaitHandle.WaitOne(PauseAbfrage);
            }

            if (nummer == 0)
            {
                Setzen("plant Ausschnitte neu");
            }

            Rechnen(true);
        }

        if (optimierer.BesterGelenkt() is not { } gesamt || !optimierer.IstBesser(gesamt.Wert, eigener))
        {
            return null;
        }

        lock (sperre)
        {
            wechsel++;
        }

        return gesamt.Plan;
    }

    /// <summary>
    /// Pause, Vorrang für HiGHS, und die weiteren Teilneuplanungen außerhalb des Glättens oder wenn die Inseln je Rechen-Thread
    /// mehr verbessern.
    /// </summary>
    private bool Ruht(int nummer) => optimierer.Pausiert || warten() || (nummer > 0 && (!optimierer.Glaettet || nummer >= Volatile.Read(ref erlaubt)));

    /// <summary>
    /// Teilt die Kerne beim Glätten nach dem Erfolg: Alle <see cref="Anpassabstand"/> werden die Verbesserungen des besten Plans je
    /// Rechen-Thread verglichen (ältere zählen halb so viel). Bringt die Teilneuplanung je Thread mehr als die Inseln, rechnet
    /// eine mehr (bis zu drei Vierteln der Kerne), bringt sie weniger als die Hälfte, eine weniger (mindestens eine); die freien Kerne
    /// rechnen für die Inseln. Aufruf unter der Sperre.
    /// </summary>
    private void Anpassen()
    {
        int inseln = optimierer.Lenkung?.VonInseln ?? 0;
        if (!optimierer.Glaettet || optimierer.Pausiert || !anpassUhr.IsRunning)
        {
            zuletztGezaehlt = (inseln, eingespielt);
            anpassUhr.Restart();
            return;
        }

        double sekunden = anpassUhr.Elapsed.TotalSeconds;
        if (sekunden < Anpassabstand.TotalSeconds)
        {
            return;
        }

        int eigeneThreads = Volatile.Read(ref erlaubt);
        int inselThreads = Math.Max(1, Environment.ProcessorCount - eigeneThreads);
        erfolge = (
            (erfolge.Inseln / 2) + (inseln - zuletztGezaehlt.Inseln),
            (erfolge.InselSekunden / 2) + (sekunden * inselThreads),
            (erfolge.Eigene / 2) + (eingespielt - zuletztGezaehlt.Eigene),
            (erfolge.EigeneSekunden / 2) + (sekunden * eigeneThreads));
        zuletztGezaehlt = (inseln, eingespielt);
        anpassUhr.Restart();
        if (erfolge.Inseln + erfolge.Eigene < 2)
        {
            return;
        }

        double jeInsel = erfolge.Inseln / erfolge.InselSekunden;
        double jeEigene = erfolge.Eigene / erfolge.EigeneSekunden;
        if (jeEigene > jeInsel && eigeneThreads < arbeiter)
        {
            Volatile.Write(ref erlaubt, eigeneThreads + 1);
        }
        else if (jeEigene * 2 < jeInsel && eigeneThreads > 1)
        {
            Volatile.Write(ref erlaubt, eigeneThreads - 1);
        }
    }

    private void Rechnen(bool ja)
    {
        lock (sperre)
        {
            rechnende = Math.Max(0, rechnende + (ja ? 1 : -1));
            if (rechnende == 0)
            {
                mannschaften = null;
            }
        }
    }

    private void Setzen(string text)
    {
        lock (sperre)
        {
            zustand = text;
        }
    }

    /// <summary>Beginn und Ausgang eines Ausschnitts (aus <see cref="Ligaschranke.Neuplanen"/>), für das Schaubild.</summary>
    private void Ausschnitt(Ausschnittsmeldung meldung, System.Diagnostics.Stopwatch ausschnittUhr)
    {
        lock (sperre)
        {
            aktuell = meldung.Bester;
            if (meldung.Ergebnis == Ausschnittsergebnis.Laeuft)
            {
                mannschaften = meldung.Mannschaften;
                ausschnittUhr.Restart();
                return;
            }

            if (meldung.Ergebnis != Ausschnittsergebnis.Ausgangsplan)
            {
                (int a, int b, double sek) = groessen.GetValueOrDefault(meldung.Mannschaften);
                bool erfolg = meldung.Ergebnis == Ausschnittsergebnis.Besser;
                groessen[meldung.Mannschaften] = (a + 1, b + (erfolg ? 1 : 0), sek + ausschnittUhr.Elapsed.TotalSeconds);
            }

            mannschaften = null;
            if (meldung.Ergebnis != Ausschnittsergebnis.Ausgangsplan)
            {
                ausschnitte++;
            }

            if (meldung.Ergebnis == Ausschnittsergebnis.Besser)
            {
                besser++;
            }

            verlauf.Add(meldung.Ergebnis);
            if (verlauf.Count > 60)
            {
                verlauf.RemoveRange(0, verlauf.Count - 60);
            }
        }
    }
}
