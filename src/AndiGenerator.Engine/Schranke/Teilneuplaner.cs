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

    /// <summary>So oft wird in der Pause gefragt, ob es weitergeht.</summary>
    private static readonly TimeSpan PauseAbfrage = TimeSpan.FromMilliseconds(300);

    /// <summary>So lange wartet <see cref="Dispose"/> höchstens auf das Ende.</summary>
    private static readonly TimeSpan Abschied = TimeSpan.FromSeconds(3);

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
    private readonly System.Diagnostics.Stopwatch ausschnittUhr = new();
    private Task? lauf;
    private int beendet;
    private string zustand = "wartet auf das Ende der Basisoptimierung";
    private int ausschnitte;
    private int besser;
    private int eingespielt;
    private int wechsel;
    private bool rechnet;
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
    public Teilneuplaner(Inseloptimierer optimierer, Staffel staffel, Berechnungsoptionen optionen, ISchrankenloeser loeser, Stufeneinteilung einteilung, IReadOnlyDictionary<Kostenkriterium, int>? obergrenzen = null, Func<bool>? warten = null)
    {
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
                return new Teilneuplanungsstand(
                    zustand,
                    ausschnitte,
                    besser,
                    eingespielt,
                    wechsel,
                    rechnet,
                    mannschaften,
                    [.. verlauf],
                    aktuell,
                    groessen.Select(g => new Ausschnittsgroesse(g.Key, g.Value.Ausschnitte, g.Value.Besser, g.Value.Sekunden)).ToArray());
            }
        }
    }

    /// <summary>Beginnt im Hintergrund.</summary>
    public void Starten() => lauf = Task.Run(Schleife, CancellationToken.None);

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

    private async Task Schleife()
    {
        CancellationToken token = abbruch.Token;
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

            // Fortlaufend bis zum Ende der Generierung; angenommen wird nach dem Maßstab des Automodus (mit C-Ausreißern).
            var e = new Neuplanungseinstellungen(
                TimeSpan.MaxValue,
                JeAusschnitt,
                Bewerten: optimierer.NachStufen,
                Besser: optimierer.IstBesser,
                Obergrenzen: obergrenzen,
                Ausschnitt: Ausschnitt,
                Verbessert: (plan, _) => Verbessert(plan),
                Ausgangsplan: Ausgangsplan,
                Unterbrechen: () => optimierer.Pausiert || warten());
            Neuplanungsbericht bericht;
            try
            {
                Setzen("plant Ausschnitte neu");
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
            Rechnen(false);
            Setzen("Fehler: " + fehler.Message);
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
    private IReadOnlyList<Spiel>? Ausgangsplan(Stufenwert eigener)
    {
        lock (sperre)
        {
            aktuell = eigener;
        }

        if (optimierer.Pausiert || warten())
        {
            Setzen(optimierer.Pausiert ? "angehalten" : "wartet auf HiGHS (Stufen A und B)");
            Rechnen(false);
            while ((optimierer.Pausiert || warten()) && !abbruch.IsCancellationRequested)
            {
                abbruch.Token.WaitHandle.WaitOne(PauseAbfrage);
            }

            Setzen("plant Ausschnitte neu");
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

    private void Rechnen(bool ja)
    {
        lock (sperre)
        {
            rechnet = ja;
            if (!ja)
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
    private void Ausschnitt(Ausschnittsmeldung meldung)
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
