// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Schranke;
using AndiGenerator.Import;

namespace AndiGenerator.Application;

/// <summary>
/// Steuert die Generierung für die Oberfläche (MIGRATIONSPLAN E5 <c>IOptimizationService</c>, Original <c>TPlanOptimizer</c> und
/// Timer des Hauptfensters): Starten, Anhalten, Datenänderung übernehmen, Stand abfragen. Verbesserungen werden als Ereignis
/// gemeldet (aus einem Hintergrund-Thread, höchstens alle <see cref="Meldeabstand"/>).
/// </summary>
public sealed class Optimierungsdienst : IDisposable
{
    /// <summary>Abstand, in dem auf Verbesserungen geprüft wird.</summary>
    public static readonly TimeSpan Meldeabstand = TimeSpan.FromMilliseconds(500);

    private readonly Lock sperre = new();
    private readonly Optimierungseinstellungen einstellungen;
    private readonly Stopwatch seitVerbesserung = new();
    private readonly Prozessormessung messung = new();
    private Inseloptimierer? optimierer;
    private CancellationTokenSource? beobachtung;
    private Thread? beobachter;
    private long fruehereDurchlaeufe;
    private int fruehereVerbesserungen;
    private double letzteKosten = -1;
    private int letzteVerbesserungen = -1;
    private bool pausiert;
    private bool automodus;
    private bool laufenderAutomodus;
    private volatile bool highsVorrang;
    private (IReadOnlyDictionary<Kostenkriterium, int> Grenzen, string Name)? festlegung;
    private Teilneuplaner? teilneuplaner;
    private (Staffel Staffel, Berechnungsoptionen Optionen)? laufend;

    /// <summary>Legt den Dienst an.</summary>
    /// <param name="einstellungen">Einstellungen des Inselmodells; <c>null</c> = Standard.</param>
    public Optimierungsdienst(Optimierungseinstellungen? einstellungen = null)
    {
        this.einstellungen = einstellungen ?? Optimierungseinstellungen.Standard;
    }

    /// <summary>Ein besserer Plan wurde gefunden (Aufruf aus einem Hintergrund-Thread).</summary>
    public event EventHandler<Optimierungsstand>? Verbessert;

    /// <summary>Eine Generierung ist gestartet.</summary>
    public bool Laeuft
    {
        get
        {
            lock (sperre)
            {
                return optimierer is not null;
            }
        }
    }

    /// <summary>Angehalten; setzbar auch ohne laufende Generierung (gilt dann für den nächsten Start).</summary>
    public bool Pausiert
    {
        get
        {
            lock (sperre)
            {
                return pausiert;
            }
        }

        set
        {
            lock (sperre)
            {
                pausiert = value;
                if (optimierer is not null)
                {
                    optimierer.Pausiert = value;
                }
            }
        }
    }

    /// <summary>
    /// Mit Automodus (MIGRATIONSPLAN Abschnitt 11): Die Suche lenkt sich selbst über die Gewichte, um Verstöße der
    /// Stufen A und B herauszudrängen und danach C zu glätten. Gilt ab dem nächsten <see cref="Starten"/>.
    /// </summary>
    public bool Automodus
    {
        get
        {
            lock (sperre)
            {
                return automodus;
            }
        }

        set
        {
            lock (sperre)
            {
                automodus = value;
            }
        }
    }

    /// <summary>
    /// Vorrang für HiGHS: Solange die untere Schranke die Stufen A und B rechnet, wartet die Teilneuplanung – ihre Arbeit wäre
    /// verloren, wenn danach eine Variante in B gewählt wird, und HiGHS bekommt die Kerne.
    /// </summary>
    public bool HighsVorrang
    {
        get => highsVorrang;
        set => highsVorrang = value;
    }

    /// <summary>Fehler, an dem die Generierung abgebrochen ist; <c>null</c>, solange alles läuft.</summary>
    public Exception? Fehler
    {
        get
        {
            lock (sperre)
            {
                return optimierer?.Fehler;
            }
        }
    }

    /// <summary>
    /// Startet die Generierung. Das Original beginnt nach dem Öffnen mit leerem Plan; mit <paramref name="ausgangsplan"/>
    /// wird von einem vorhandenen Plan aus weitergerechnet (ungültige Termine werden entfernt).
    /// </summary>
    /// <param name="staffel">Staffel.</param>
    /// <param name="optionen">Berechnungsoptionen.</param>
    /// <param name="ausgangsplan">Ausgangsplan oder <c>null</c> für einen leeren Plan.</param>
    public void Starten(Staffel staffel, Berechnungsoptionen optionen, IReadOnlyList<Spiel>? ausgangsplan = null)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        Stoppen();
        lock (sperre)
        {
            fruehereDurchlaeufe = 0;
            fruehereVerbesserungen = 0;
            festlegung = null;

            // Bei „nur Rückrunde“ bleibt die Vorrunde aus click-TT fest, auch mit leerem oder gemerktem Startplan.
            NeuStarten(staffel with { BestehenderSpielplan = Vorrunde.Festhalten(staffel, optionen, ausgangsplan) }, optionen, ausgangsplan is { Count: > 0 });
        }

        beobachtung = new CancellationTokenSource();
        CancellationToken token = beobachtung.Token;
        beobachter = new Thread(() => Beobachten(token)) { IsBackground = true, Name = "Generierung beobachten" };
        beobachter.Start();
    }

    /// <summary>
    /// Übernimmt geänderte Stammdaten oder Optionen während der Generierung (Original <c>setNewPlanData</c>):
    /// Der beste Plan bleibt Ausgangspunkt, soweit seine Termine gültig bleiben.
    /// </summary>
    /// <param name="staffel">Neue Staffel.</param>
    /// <param name="optionen">Neue Optionen.</param>
    public void DatenAendern(Staffel staffel, Berechnungsoptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        lock (sperre)
        {
            if (optimierer is null)
            {
                return;
            }

            Optimierungsergebnis bisher = optimierer.BesterStand();
            fruehereDurchlaeufe += optimierer.Durchlaeufe;
            fruehereVerbesserungen += bisher.Verbesserungen;
            TeilneuplanungBeenden();
            optimierer.Dispose();

            // Wechsel von der Kostenoptimierung in den Automodus: Der Plan ist schon optimiert, keine neue Basisoptimierung.
            bool wechselZumAutomodus = automodus && !laufenderAutomodus && bisher.Spiele.Count > 0;
            NeuStarten(staffel with { BestehenderSpielplan = Vorrunde.Festhalten(staffel, optionen, bisher.Spiele) }, optionen, ausgangsplanMelden: true, ohneGrundlauf: wechselZumAutomodus);
        }
    }

    /// <summary>
    /// Automodus: übernimmt Schranke und Plan aus HiGHS in die laufende Generierung (siehe
    /// <see cref="Inseloptimierer.HighsEinspielen"/>).
    /// </summary>
    /// <param name="plan">Plan aus HiGHS (leer = keiner).</param>
    /// <param name="schrankeA">Bewiesene Schranke der Summe der Stufe A, oder <c>null</c>.</param>
    /// <param name="schrankeB">Bewiesene Schranke der Summe der Stufe B (bei bestmöglichem A), oder <c>null</c>.</param>
    public void HighsEinspielen(IReadOnlyList<Spiel> plan, int? schrankeA, int? schrankeB)
    {
        lock (sperre)
        {
            optimierer?.HighsEinspielen(plan, schrankeA, schrankeB);
        }
    }

    /// <summary>
    /// Automodus: bietet einen Zwischenplan aus HiGHS sofort an (siehe <see cref="Inseloptimierer.PlanEinspielen"/>) – ist er
    /// nach Stufen besser, wird er bester Plan, und alle Inseln, die in A oder B schlechter sind, beginnen mit ihm. Während der
    /// Basisoptimierung wird nichts übernommen.
    /// </summary>
    /// <param name="plan">Der Plan.</param>
    /// <param name="herkunft">Für das Protokoll des Automodus.</param>
    public void PlanAnbieten(IReadOnlyList<Spiel> plan, string herkunft)
    {
        Inseloptimierer? o;
        lock (sperre)
        {
            o = laufenderAutomodus ? optimierer : null;
        }

        o?.PlanEinspielen(plan, herkunft, inselnNachziehen: true);
    }

    /// <summary>
    /// Übernimmt eine geänderte Einteilung der Kriterien in den laufenden Automodus: keine neue Basisoptimierung, er macht
    /// beim wichtigsten geänderten Kriterium weiter. Bei der Kostenoptimierung ändert sich nichts.
    /// </summary>
    /// <param name="neu">Die neue Einteilung.</param>
    public void EinteilungAendern(Stufeneinteilung neu)
    {
        ArgumentNullException.ThrowIfNull(neu);
        lock (sperre)
        {
            optimierer?.EinteilungAendern(neu);
            TeilneuplanungStarten(neu);
        }
    }

    /// <summary>
    /// Automodus: Der Staffelleiter legt A und B fest (gewählte Alternative aus HiGHS) – kein Kriterium der Stufen A und B darf
    /// mehr Verstöße haben als angegeben, danach wird nur noch C geglättet. Der Plan wird angeboten (er hält die Grenzen ein und
    /// wird so bester Plan), die Teilneuplanung beginnt mit den Grenzen neu. <c>null</c> hebt die Festlegung auf. Gilt auch
    /// nach einer Datenänderung, bis zum nächsten Start.
    /// </summary>
    /// <param name="grenzen">Je Kriterium die Obergrenze, oder <c>null</c>.</param>
    /// <param name="name">Was festgelegt wurde.</param>
    /// <param name="plan">Der gewählte Plan, oder <c>null</c>.</param>
    public void Festlegen(IReadOnlyDictionary<Kostenkriterium, int>? grenzen, string name, IReadOnlyList<Spiel>? plan)
    {
        lock (sperre)
        {
            festlegung = grenzen is null ? null : (grenzen, name);
            if (optimierer is null || !laufenderAutomodus)
            {
                return;
            }

            optimierer.Festlegen(grenzen, grenzen is null ? null : name);
            if (plan is { Count: > 0 })
            {
                optimierer.PlanEinspielen(plan, $"Festgelegt: {name}", inselnNachziehen: true);
            }

            if (laufend is { } l)
            {
                TeilneuplanungStarten(Stufeneinteilung.AusText(l.Staffel.Kriterienstufen));
            }
        }
    }

    /// <summary>Holt den besten Plan des Automodus (nach der Basisoptimierung), z. B. als Startlösung für HiGHS.</summary>
    /// <returns>Der Plan, oder <c>null</c>, wenn es (noch) keinen gibt.</returns>
    public IReadOnlyList<Spiel>? BesterPlan()
    {
        lock (sperre)
        {
            return laufenderAutomodus ? optimierer?.BesterGelenkt()?.Plan : null;
        }
    }

    /// <summary>Hält die Generierung an und stellt beim <see cref="IDisposable.Dispose"/> den vorherigen Zustand wieder her.</summary>
    /// <returns>Der Bereich; typischerweise mit <c>using</c> um einen Dialog.</returns>
    public IDisposable Anhalten()
    {
        bool vorher = Pausiert;
        Pausiert = true;
        return new Pausenbereich(this, vorher);
    }

    /// <summary>Aktueller Stand der Generierung.</summary>
    /// <returns>Der Stand; <see cref="Optimierungsstand.Leer"/>, wenn keine Generierung läuft.</returns>
    public Optimierungsstand Stand()
    {
        lock (sperre)
        {
            return StandIntern();
        }
    }

    /// <summary>Beendet die Generierung (Original <c>KillOptimizer</c>).</summary>
    public void Stoppen()
    {
        beobachtung?.Cancel();
        beobachter?.Join();
        beobachtung?.Dispose();
        beobachtung = null;
        beobachter = null;
        lock (sperre)
        {
            TeilneuplanungBeenden();
            optimierer?.Dispose();
            optimierer = null;
        }
    }

    /// <inheritdoc/>
    public void Dispose() => Stoppen();

    /// <summary>Stand ohne eigene Sperre (Aufrufer hält <c>sperre</c>).</summary>
    private Optimierungsstand StandIntern()
    {
        if (optimierer is null)
        {
            return Optimierungsstand.Leer;
        }

        (Optimierungsergebnis bester, Lenkungsstand? lenkung, Maschinenraum raum) = optimierer.Momentaufnahme();
        return new Optimierungsstand(
            true,
            pausiert,
            fruehereDurchlaeufe + optimierer.Durchlaeufe,
            optimierer.PlaeneProSekunde,
            bester.Kosten,
            fruehereVerbesserungen + bester.Verbesserungen,
            seitVerbesserung.Elapsed,
            optimierer.SpezialKostenart,
            bester.Spiele,
            lenkung,
            raum,
            teilneuplaner?.Stand,
            messung.Messen());
    }

    private void NeuStarten(Staffel staffel, Berechnungsoptionen optionen, bool ausgangsplanMelden, bool ohneGrundlauf = false)
    {
        // Der Automodus lenkt nach der Einteilung der Kriterien, die der Staffelleiter in der Qualitätsansicht gewählt hat.
        Optimierungseinstellungen e = einstellungen with
        {
            Automodus = automodus,
            AutoStufen = Stufeneinteilung.AusText(staffel.Kriterienstufen),
            AutoOhneGrundlauf = ohneGrundlauf,
            Fadenstart = Rechenfaeden.SucheBeginnt,
        };
        Rechenfaeden.SucheVergessen();
        messung.Zuruecksetzen();

        optimierer = new Inseloptimierer(staffel, optionen, e);
        laufenderAutomodus = automodus;
        optimierer.Pausiert = pausiert;
        if (automodus && festlegung is { } f)
        {
            optimierer.Festlegen(f.Grenzen, f.Name);
        }

        optimierer.Starten();
        laufend = (staffel, optionen);
        TeilneuplanungStarten(e.AutoStufen ?? Stufeneinteilung.Standard);

        // Ein vorgegebener Ausgangsplan (Start mit gemerktem Plan, neu bewerteter Plan nach einer Datenänderung) wird
        // sofort gemeldet: Er ist der beste Plan, und ohne Meldung blieben Ansichten und Pflichtregeln leer, bis er
        // übertroffen wird. Ohne Ausgangsplan (noch keine Termine) wird erst der erste gefundene Plan gemeldet.
        letzteKosten = ausgangsplanMelden ? -1 : optimierer.BesterStand().Kosten;
        letzteVerbesserungen = ausgangsplanMelden ? -1 : optimierer.BesterStand().Verbesserungen;
        seitVerbesserung.Restart();
    }

    /// <summary>
    /// Automodus mit HiGHS: Teilneuplanung nebenher (MIGRATIONSPLAN O7 Schritt 3) – nach der Basisoptimierung plant sie
    /// Ausschnitte des besten Plans neu und spielt bessere Pläne ein. Eine laufende endet vorher.
    /// </summary>
    private void TeilneuplanungStarten(Stufeneinteilung einteilung)
    {
        // Der Optimierer läuft weiter: Die alte Teilneuplanung endet im Hintergrund (sie bricht ab, wartet aber bis zu 3 s auf
        // HiGHS) – sonst hinge die Oberfläche, z. B. beim Umstufen eines Kriteriums.
        if (teilneuplaner is { } alt)
        {
            teilneuplaner = null;
            _ = Task.Run(alt.Dispose, CancellationToken.None);
        }

        if (optimierer is null || !laufenderAutomodus || laufend is not { } l || !Stufenschranke.Verfuegbar)
        {
            return;
        }

        teilneuplaner = new Teilneuplaner(optimierer, l.Staffel, l.Optionen, new Highsbruecke(), einteilung.Vervollstaendigt(), festlegung?.Grenzen, () => highsVorrang);
        teilneuplaner.Starten();
    }

    private void TeilneuplanungBeenden()
    {
        teilneuplaner?.Dispose();
        teilneuplaner = null;
    }

    private void Beobachten(CancellationToken token)
    {
        while (!token.WaitHandle.WaitOne(Meldeabstand))
        {
            Optimierungsstand? neu = null;
            lock (sperre)
            {
                if (optimierer is null)
                {
                    continue;
                }

                // Vorrang für HiGHS (Automodus): So viele Rechen-Threads der Inseln ruhen, wie HiGHS gerade Rechnungen laufen hat
                // (je Rechnung ein Kern; mindestens ein Thread sucht weiter).
                optimierer.Ruhend = laufenderAutomodus ? Highsauslastung.Laufend : 0;

                // Gemeldet wird jede Verbesserung des geltenden besten Plans (im Automodus nach Verstößen, dort kann er auch teurer
                // werden) – dieselbe Zahl, die Kachel, Verlauf und Leitstand zeigen.
                Optimierungsergebnis bester = optimierer.BesterStand();
                double kosten = bester.Kosten;
                bool besser = letzteKosten < 0 || bester.Verbesserungen != letzteVerbesserungen;
                if (kosten >= 0 && besser)
                {
                    letzteKosten = kosten;
                    letzteVerbesserungen = bester.Verbesserungen;
                    seitVerbesserung.Restart();
                    neu = StandIntern();
                }
            }

            if (neu is not null)
            {
                Verbessert?.Invoke(this, neu);
            }
        }
    }
}
