// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Kern;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Inseln;

/// <summary>
/// Optimierer mit Inselmodell (Original <c>TPlanOptimizer</c>, MIGRATIONSPLAN E6): mehrere Inseln mit je festen und
/// dynamischen Suchplätzen, gerechnet von einer frei wählbaren Zahl von Threads. Ein Koordinator gleicht etwa alle 200 ms
/// die Inseln ab, übernimmt etwa jede Sekunde die beste Insel als Gesamtergebnis und startet die schlechteste Insel neu,
/// wenn sie zu lange keine Verbesserung gefunden hat. Im inneren Durchlauf gibt es keine Sperren.
/// </summary>
public sealed class Inseloptimierer : IDisposable
{
    private const int AbgleichMs = 200;
    private const int AbgleicheJeOptimiererSchritt = 5;
    private const int SchritteJeAbgleich = 2_000;

    /// <summary>So viele größte Verursacher zeigt der Leitstand je Insel.</summary>
    private const int Verursacher = 3;

    private readonly Lock sperre = new();
    private readonly Optimierungseinstellungen einstellungen;
    private readonly Staffel staffel;
    private readonly Insel[] inseln;
    private readonly Insel? spezial;
    private readonly Suchplatz[] alle;
    private readonly Loesung beste;
    private readonly Loesung leer;
    private readonly Automodus? auto;
    private readonly double autoBudget;
    private readonly int[] autoStand;
    private readonly Stufenwert?[] autoWerte;
    private readonly Kostenverursacher[][] autoVerursacher;
    private readonly Random koordinatorZufall;
    private readonly ManualResetEventSlim laufen = new(true);
    private readonly ManualResetEventSlim freigabe = new(true);
    private readonly Stopwatch uhr = new();

    /// <summary>Reine Rechenzeit ohne Pausen (für den Automodus, der nach Sekunden lenkt).</summary>
    private readonly Stopwatch laufzeit = new();
    private readonly List<Thread> threads = [];
    private Suchplatz[][] kernPlaetze = [];
    private int ruhend;
    private CancellationTokenSource? abbruch;
    private KernDefinition definition;
    private Berechnungsoptionen optionen;
    private KernPlan bewerter;
    private long rechnenZiel;
    private double besteKosten;
    private int verbesserungen;
    private (int Verbesserungen, bool Gelenkt) zaehlStand;
    private int planVerbesserungen;
    private (int Verbesserungen, bool Gelenkt)? besteKriterienStand;
    private Kriterienwert[] besteKriterien = [];
    private int abgleiche;
    private bool spezialLaeuft;
    private int spezialVerwendet;
    private MannschaftsKostenart? spezialKostenart;
    private long durchlaeufeBeiMessbeginn;
    private Exception? fehler;

    /// <summary>Plan aus HiGHS, der beim nächsten Schritt des Automodus nach dem Grundlauf eingespielt wird.</summary>
    private Loesung? highsPlan;

    /// <summary>Lädt die Staffel und legt Inseln und Suchplätze an; die Rechnung beginnt mit <see cref="Starten"/> oder <see cref="Rechnen"/>.</summary>
    /// <param name="staffel">Staffel; ein bestehender Spielplan ist der Ausgangspunkt (ungültige Termine werden entfernt).</param>
    /// <param name="optionen">Berechnungsoptionen.</param>
    /// <param name="einstellungen">Einstellungen des Inselmodells; <c>null</c> = <see cref="Optimierungseinstellungen.Standard"/>.</param>
    public Inseloptimierer(Staffel staffel, Berechnungsoptionen optionen, Optimierungseinstellungen? einstellungen = null)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        this.einstellungen = einstellungen ?? Optimierungseinstellungen.Standard;
        if (this.einstellungen.Kerne < 1 || this.einstellungen.Inseln < 1)
        {
            throw new ArgumentException("Kerne und Inseln müssen mindestens 1 sein.", nameof(einstellungen));
        }

        this.staffel = staffel;
        this.optionen = optionen;
        RefPlan referenz = RefPlan.Laden(staffel, optionen);
        definition = new KernDefinition(referenz);
        bewerter = NeuerPlan(referenz, definition);
        besteKosten = bewerter.Kosten();
        if (this.einstellungen.Automodus)
        {
            autoBudget = this.einstellungen.Zeitbudget > 0 ? this.einstellungen.Zeitbudget : Automodus.StandardBudget;
            auto = new Automodus(referenz, definition, this.einstellungen);
        }

        beste = new Loesung(bewerter.AnzahlErlaubt);
        bewerter.LoesungSchreiben(beste);
        leer = new Loesung(bewerter.AnzahlErlaubt);

        var samen = new Random(this.einstellungen.Startwert ?? Random.Shared.Next());
        koordinatorZufall = new Random(samen.Next());
        inseln = new Insel[this.einstellungen.Inseln];
        autoStand = new int[inseln.Length];
        autoWerte = new Stufenwert?[inseln.Length];
        autoVerursacher = new Kostenverursacher[inseln.Length][];
        Array.Fill(autoStand, -1);
        for (int i = 0; i < inseln.Length; i++)
        {
            inseln[i] = InselAnlegen(i + 1, referenz, samen, aktiv: true);
        }

        if (this.einstellungen.Startplan is { Count: > 0 } startplan)
        {
            StartplanVerteilen(startplan);
        }

        if (this.einstellungen.SpezialInsel && auto is null)
        {
            spezial = InselAnlegen(0, referenz, samen, aktiv: false);
        }

        Insel[] mitSpezial = spezial is null ? inseln : [.. inseln, spezial];
        alle = mitSpezial.SelectMany(i => i.Plaetze).ToArray();
    }

    /// <summary>Summe aller Durchläufe.</summary>
    public long Durchlaeufe
    {
        get
        {
            long summe = 0;
            foreach (Suchplatz platz in alle)
            {
                summe += platz.Durchlaeufe;
            }

            return summe;
        }
    }

    /// <summary>Durchläufe je Sekunde seit Start bzw. Ende der letzten Pause (Original <c>PlanPerSec</c>).</summary>
    public double PlaeneProSekunde
    {
        get
        {
            double sekunden = uhr.Elapsed.TotalSeconds;
            return sekunden > 0 ? (Durchlaeufe - Volatile.Read(ref durchlaeufeBeiMessbeginn)) / sekunden : 0;
        }
    }

    /// <summary>Anzahl der Neustarts von Inseln.</summary>
    public int Neustarts
    {
        get
        {
            lock (sperre)
            {
                return inseln.Sum(i => i.Neustarts);
            }
        }
    }

    /// <summary>Wie oft die Lösung der Spezial-Insel eine Insel neu gestartet hat.</summary>
    public int SpezialVerwendet
    {
        get
        {
            lock (sperre)
            {
                return spezialVerwendet;
            }
        }
    }

    /// <summary>Aktuell extrem gewichtete Kostenart der Spezial-Insel; <c>null</c>, solange sie nicht läuft.</summary>
    public MannschaftsKostenart? SpezialKostenart
    {
        get
        {
            lock (sperre)
            {
                return spezialKostenart;
            }
        }
    }

    /// <summary>Leitstand: Pläne je Rechenkern und der Stand jeder Insel.</summary>
    public Maschinenraum Maschinenraum
    {
        get
        {
            long[] kerne = kernPlaetze.Select(eigene => eigene.Sum(p => p.Durchlaeufe)).ToArray();
            lock (sperre)
            {
                // Die schlechteste Insel wie beim Neustart im Optimierer-Schritt: die mit den höchsten Kosten.
                int schlechteste = -1;
                for (int i = 0; i < inseln.Length; i++)
                {
                    if (inseln[i].Kosten >= 0 && (schlechteste < 0 || inseln[i].Kosten > inseln[schlechteste].Kosten))
                    {
                        schlechteste = i;
                    }
                }

                IEnumerable<Inselstand> stand = inseln.Select((insel, i) =>
                    StandVon(insel, spezial: false, laeuft: true, autoWerte[i], autoWerte[i] is null ? Kostentreiber(insel) : autoVerursacher[i]) with
                    {
                        Schlechteste = i == schlechteste && inseln.Length > 1,
                    });
                if (spezial is not null)
                {
                    stand = stand.Append(StandVon(spezial, spezial: true, laeuft: spezialLaeuft));
                }

                return new Maschinenraum(kerne, stand.ToArray(), einstellungen.DurchlaeufeVorNeustart, spezialKostenart, spezialVerwendet, BesteKriterien(), planVerbesserungen);
            }
        }
    }

    /// <summary>Stand des Automodus; <c>null</c>, wenn er nicht eingeschaltet ist.</summary>
    public Lenkungsstand? Lenkung
    {
        get
        {
            lock (sperre)
            {
                return auto?.Stand;
            }
        }
    }

    /// <summary>Angehalten (Original <c>Paused</c>); die Threads warten ohne Rechenlast.</summary>
    public bool Pausiert
    {
        get => !laufen.IsSet;
        set
        {
            if (value)
            {
                laufen.Reset();
                uhr.Stop();
                laufzeit.Stop();
            }
            else
            {
                Volatile.Write(ref durchlaeufeBeiMessbeginn, Durchlaeufe);
                uhr.Restart();
                if (abbruch is not null)
                {
                    laufzeit.Start();
                }

                laufen.Set();
            }
        }
    }

    /// <summary>
    /// Holt oder setzt, wie viele Rechen-Threads ruhen (Vorrang für HiGHS: solange es Stufe A, B und die Alternativen rechnet,
    /// bekommen die Inseln nur die übrigen Kerne). Mindestens ein Thread rechnet immer.
    /// </summary>
    public int Ruhend
    {
        get => Volatile.Read(ref ruhend);
        set
        {
            Volatile.Write(ref ruhend, Math.Clamp(value, 0, einstellungen.Kerne - 1));
            if (Volatile.Read(ref ruhend) == 0)
            {
                freigabe.Set();
            }
            else
            {
                freigabe.Reset();
            }
        }
    }

    /// <summary>Fehler, an dem ein Rechen-Thread abgebrochen ist; dann ist die Optimierung beendet.</summary>
    public Exception? Fehler => Volatile.Read(ref fehler);

    /// <summary>
    /// Automodus mit HiGHS (MIGRATIONSPLAN O7): übernimmt die untere Schranke und den in A und B bestmöglichen Plan. Die Schranke
    /// beendet das Drängen einer Stufe, sobald der beste Plan bei ihr ist. Kommt der Plan während des Grundlaufs, endet dieser
    /// beim nächsten Schritt des Automodus, frühestens nach 10 s (sein bester Plan ist der Maßstab für C). Der Plan wird dann
    /// geprüft: Ist er nach Stufen besser, wird er bester
    /// Plan, und die schlechtere Hälfte der Inseln beginnt mit ihm neu – die andere sucht mit ihren Plänen weiter (Vielfalt).
    /// </summary>
    /// <param name="plan">Plan aus HiGHS (leer = keiner).</param>
    /// <param name="schrankeA">Bewiesene Schranke der Summe der Stufe A, oder <c>null</c>.</param>
    /// <param name="schrankeB">Bewiesene Schranke der Summe der Stufe B (bei bestmöglichem A), oder <c>null</c>.</param>
    public void HighsEinspielen(IReadOnlyList<Spiel> plan, int? schrankeA, int? schrankeB)
    {
        ArgumentNullException.ThrowIfNull(plan);
        lock (sperre)
        {
            if (auto is null)
            {
                return;
            }

            auto.SchrankenSetzen(schrankeA, schrankeB, AutoSekunden());
            highsPlan = plan.Count > 0 ? AlsLoesung(plan) : highsPlan;
        }
    }

    /// <summary>
    /// Automodus: bewertet einen Plan nach Stufen wie den besten Plan (mit C-Ausreißern gegenüber der Basisoptimierung);
    /// <c>null</c> ohne Automodus oder während der Basisoptimierung.
    /// </summary>
    /// <param name="plan">Der Plan.</param>
    /// <returns>Die Bewertung.</returns>
    public Stufenwert? NachStufen(IReadOnlyList<Spiel> plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        lock (sperre)
        {
            return auto is { Grundlauf: false } ? auto.NachStufen(AlsLoesung(plan)) : null;
        }
    }

    /// <summary>
    /// Automodus: der beste Plan nach Stufen mit seiner Bewertung; <c>null</c> ohne Automodus oder während der
    /// Basisoptimierung.
    /// </summary>
    /// <returns>Plan und Bewertung.</returns>
    public (IReadOnlyList<Spiel> Plan, Stufenwert Wert)? BesterGelenkt()
    {
        lock (sperre)
        {
            return auto is { Grundlauf: false } ? (BesterStand().Spiele, auto.BesterWert) : null;
        }
    }

    /// <summary>
    /// Automodus: Der Staffelleiter legt A und B fest (siehe <see cref="Automodus.Festlegen"/>); <c>null</c> hebt das auf.
    /// </summary>
    /// <param name="grenzen">Je Kriterium der Stufen A und B die Obergrenze, oder <c>null</c>.</param>
    /// <param name="name">Was festgelegt wurde (für die Anzeige).</param>
    public void Festlegen(IReadOnlyDictionary<Kostenkriterium, int>? grenzen, string? name)
    {
        lock (sperre)
        {
            auto?.Festlegen(grenzen, name, AutoSekunden());
        }
    }

    /// <summary>Automodus: vergleicht zwei Bewertungen wie bei der Wahl des besten Plans (nach Stufen).</summary>
    /// <param name="neu">Der neue Wert.</param>
    /// <param name="alt">Der bisherige.</param>
    /// <returns><c>true</c>, wenn <paramref name="neu"/> besser ist (ohne Automodus: nie).</returns>
    public bool IstBesser(Stufenwert neu, Stufenwert alt)
    {
        lock (sperre)
        {
            return auto is not null && Stufenvergleich.Besser(neu, alt);
        }
    }

    /// <summary>
    /// Automodus: Plan der Teilneuplanung (MIGRATIONSPLAN O7 Schritt 3) oder aus HiGHS. Ist er nach Stufen besser, wird er
    /// bester Plan, und die schlechteste Insel rechnet von ihm aus weiter – mit <paramref name="inselnNachziehen"/> jede Insel,
    /// die in A oder B (oder bei den harten Fehlern) schlechter ist. Während der Basisoptimierung wird nichts übernommen.
    /// </summary>
    /// <param name="plan">Der Plan.</param>
    /// <param name="herkunft">Für das Protokoll des Automodus.</param>
    /// <param name="inselnNachziehen">
    /// Alle Inseln, die in A oder B schlechter sind, beginnen sofort mit dem Plan (für die Pläne der unteren Schranke: Sie sind
    /// in A und B oft weit besser – die Inseln würden sonst lange an ihren alten Plänen weiterrechnen).
    /// </param>
    /// <returns><c>true</c>, wenn er bester Plan geworden ist.</returns>
    public bool PlanEinspielen(IReadOnlyList<Spiel> plan, string herkunft, bool inselnNachziehen = false)
    {
        ArgumentNullException.ThrowIfNull(plan);
        lock (sperre)
        {
            if (auto is not { Grundlauf: false } || plan.Count == 0)
            {
                return false;
            }

            Loesung loesung = AlsLoesung(plan);
            if (!auto.Einspielen(loesung, AutoSekunden(), herkunft, sofortLenken: false))
            {
                return false;
            }

            double kosten = Bewerten(bewerter, loesung);
            Insel schlechteste = inseln.OrderByDescending(i => i.Kosten < 0 ? double.MaxValue : i.Kosten).First();
            Stufenwert bester = auto.BesterWert;
            for (int i = 0; i < inseln.Length; i++)
            {
                bool schlechter = autoWerte[i] is not { } w || (w.Harte, w.A, w.B).CompareTo((bester.Harte, bester.A, bester.B)) > 0;
                if (inseln[i] == schlechteste || (inselnNachziehen && schlechter))
                {
                    inseln[i].Neustarten(loesung, kosten);
                    autoStand[i] = -1;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Übernimmt eine geänderte Einteilung der Kriterien in den laufenden Automodus (ohne neue Basisoptimierung); die Inseln
    /// werden danach nach der neuen Einteilung verglichen.
    /// </summary>
    /// <param name="neu">Die neue Einteilung.</param>
    public void EinteilungAendern(Stufeneinteilung neu)
    {
        ArgumentNullException.ThrowIfNull(neu);
        lock (sperre)
        {
            if (auto is null)
            {
                return;
            }

            auto.EinteilungAendern(neu, AutoSekunden());
            Array.Fill(autoStand, -1);
        }
    }

    /// <summary>Startet die Rechen-Threads und den Koordinator im Hintergrund.</summary>
    public void Starten()
    {
        if (abbruch is not null)
        {
            throw new InvalidOperationException("Der Optimierer läuft bereits.");
        }

        abbruch = new CancellationTokenSource();
        CancellationToken token = abbruch.Token;
        int kerne = einstellungen.Kerne;
        var plaetze = new Suchplatz[kerne][];
        for (int k = 0; k < kerne; k++)
        {
            Suchplatz[] eigene = alle.Where((_, index) => index % kerne == k).ToArray();
            plaetze[k] = eigene;
            int nummer = k;
            threads.Add(ThreadStarten(() => Arbeiten(eigene, nummer, token), $"Suche {k + 1}", ThreadPriority.BelowNormal));
        }

        kernPlaetze = plaetze;

        threads.Add(ThreadStarten(() => Koordinieren(token), "Koordinator", ThreadPriority.Normal));
        uhr.Restart();
        if (!Pausiert)
        {
            laufzeit.Start();
        }
    }

    /// <summary>Beendet alle Threads und übernimmt die letzten Ergebnisse.</summary>
    public void Stoppen()
    {
        if (abbruch is null)
        {
            return;
        }

        abbruch.Cancel();
        laufen.Set();
        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        threads.Clear();
        kernPlaetze = [];
        abbruch.Dispose();
        abbruch = null;
        uhr.Stop();
        laufzeit.Stop();
        lock (sperre)
        {
            Abgleichen(optimiererSchritt: true);
        }
    }

    /// <summary>
    /// Rechnet im aufrufenden Thread, bis insgesamt <paramref name="durchlaeufe"/> Durchläufe erreicht sind. Die Suchplätze
    /// werden reihum gerechnet und nach festen Durchlaufzahlen abgeglichen; mit <see cref="Optimierungseinstellungen.Startwert"/>
    /// ist das Ergebnis reproduzierbar (Kommandozeile, Tests).
    /// </summary>
    /// <param name="durchlaeufe">Gesamtzahl der Durchläufe.</param>
    public void Rechnen(long durchlaeufe)
    {
        if (abbruch is not null)
        {
            throw new InvalidOperationException("Der Optimierer läuft bereits im Hintergrund.");
        }

        uhr.Restart();
        rechnenZiel = durchlaeufe;
        long gerechnet = Durchlaeufe;
        int index = 0;
        while (gerechnet < durchlaeufe)
        {
            if (alle[index].Rechnen())
            {
                gerechnet++;
                if (gerechnet % SchritteJeAbgleich == 0)
                {
                    lock (sperre)
                    {
                        Abgleichen(optimiererSchritt: MitOptimiererSchritt());
                    }
                }
            }

            index = (index + 1) % alle.Length;
        }

        uhr.Stop();
        lock (sperre)
        {
            Abgleichen(optimiererSchritt: true);
        }
    }

    /// <summary>
    /// Bester Plan, Stand des Automodus und Leitstand aus demselben Augenblick (unter einer Sperre), damit Kacheln,
    /// Verlauf und Leitstand dieselben Verbesserungen zeigen.
    /// </summary>
    /// <returns>Die drei Angaben.</returns>
    public (Optimierungsergebnis Bester, Lenkungsstand? Lenkung, Maschinenraum Raum) Momentaufnahme()
    {
        lock (sperre)
        {
            return (BesterStand(), Lenkung, Maschinenraum);
        }
    }

    /// <summary>Bester bisher gefundener Plan (Original <c>copyPlanDates</c>).</summary>
    /// <returns>Kosten, Anzahl der Verbesserungen des Gesamtergebnisses und alle Spiele.</returns>
    public Optimierungsergebnis BesterStand()
    {
        lock (sperre)
        {
            // Im Automodus nach dem Grundlauf: bester Plan nach Stufen, Kosten mit den Gewichtungen des Anwenders.
            bool gelenkt = auto is { Grundlauf: false };
            Loesung ergebnis = gelenkt ? auto!.Beste : beste;
            var spiele = new List<Spiel>(ergebnis.Datum.Length);
            for (int s = 0; s < ergebnis.Datum.Length; s++)
            {
                DateTime? zeitpunkt = ergebnis.Datum[s] != 0.0 ? DelphiDatum.ZuDateTime(ergebnis.Datum[s]) : null;
                spiele.Add(new Spiel(zeitpunkt, definition.Name[definition.SpielHeim[s]], definition.Name[definition.SpielGast[s]], string.Empty));
            }

            return new Optimierungsergebnis(gelenkt ? auto!.BesteKosten : besteKosten, planVerbesserungen, spiele);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stoppen();
        laufen.Dispose();
        freigabe.Dispose();
    }

    /// <summary>Stand einer Insel für den Leitstand (unter der Sperre des Koordinators).</summary>
    /// <param name="insel">Die Insel.</param>
    /// <param name="spezial">Ist die Spezial-Insel.</param>
    /// <param name="laeuft">Rechnet gerade.</param>
    /// <param name="verstoesse">Im Automodus: ihre Verstöße nach Stufen.</param>
    /// <param name="kostentreiber">Ihre größten Kostenverursacher.</param>
    /// <returns>Der Stand.</returns>
    private static Inselstand StandVon(Insel insel, bool spezial, bool laeuft, Stufenwert? verstoesse = null, IReadOnlyList<Kostenverursacher>? kostentreiber = null) => new(
        insel.Nummer,
        spezial,
        laeuft,
        insel.Kosten,
        insel.Plaetze.Sum(p => p.Durchlaeufe),
        insel.OhneVerbesserung,
        insel.Neustarts,
        insel.Plaetze.Select(p => p.Strategie.Kurz()).ToArray(),
        verstoesse,
        insel.Erfolge,
        kostentreiber);

    private static Thread ThreadStarten(ThreadStart arbeit, string name, ThreadPriority prioritaet)
    {
        var thread = new Thread(arbeit) { IsBackground = true, Name = name, Priority = prioritaet };
        thread.Start();
        return thread;
    }

    /// <summary>Plan wie beim Start eines Workers: bestehender Spielplan ohne ungültige Termine als gemerkte Lösung.</summary>
    private static KernPlan NeuerPlan(RefPlan referenz, KernDefinition stammdaten)
    {
        var plan = new KernPlan(referenz, stammdaten);
        plan.UngueltigeTermineEntfernen();
        plan.Merken();
        return plan;
    }

    /// <summary>Bewertet eine Lösung mit den Stammdaten (und Gewichtungen) des übergebenen Plans.</summary>
    private static double Bewerten(KernPlan plan, Loesung loesung)
    {
        plan.LoesungUebernehmen(loesung);
        plan.Zuruecksetzen();
        plan.Sortieren();
        return plan.Kosten();
    }

    /// <summary>
    /// Die drei größten Kostenverursacher des besten Plans einer Insel mit den aktuellen Gewichten (unter der Sperre des
    /// Koordinators; bewertet mit dem gemeinsamen Bewerter).
    /// </summary>
    /// <param name="insel">Die Insel.</param>
    /// <returns>Kriterium und Anteil an den Kosten aller Kriterien, größter zuerst.</returns>
    private Kostenverursacher[] Kostentreiber(Insel insel)
    {
        if (insel.Kosten < 0)
        {
            return [];
        }

        List<Kriterienwert> kosten = Kriterienkosten(insel.Loesung);
        double summe = kosten.Sum(k => k.Wert);
        return summe <= 0
            ? []
            : kosten.Where(k => k.Wert > 0).OrderByDescending(k => k.Wert).Take(Verursacher).Select(k => new Kostenverursacher(k.Kriterium, k.Wert / summe)).ToArray();
    }

    /// <summary>
    /// Die Kosten je Kriterium einer Lösung mit den aktuellen Gewichten (unter der Sperre des Koordinators; bewertet mit dem
    /// gemeinsamen Bewerter).
    /// </summary>
    private List<Kriterienwert> Kriterienkosten(Loesung loesung)
    {
        Bewerten(bewerter, loesung);
        var kosten = new List<Kriterienwert>();
        foreach (MannschaftsKostenart art in Enum.GetValues<MannschaftsKostenart>())
        {
            if (optionen.Fuer(art) != Gewichtung.NichtBeruecksichtigen)
            {
                kosten.Add(new Kriterienwert((Kostenkriterium)(int)art, bewerter.MannschaftsKosten(art)));
            }
        }

        if (optionen.Spieltag != Gewichtung.NichtBeruecksichtigen || optionen.SpieltagUeberlappung != Gewichtung.NichtBeruecksichtigen)
        {
            RefPlan.Spieltagskosten s = bewerter.SpieltagsKosten();
            kosten.Add(new Kriterienwert(Kostenkriterium.Spieltaglaenge, s.Breite + s.LetzteBreite));
            kosten.Add(new Kriterienwert(Kostenkriterium.Spieltagueberlappung, s.Ueberlappung + s.LetzteUeberlappung));
        }

        return kosten;
    }

    /// <summary>
    /// Die Kriterien des besten Plans (unter der Sperre des Koordinators): bei der Kostenoptimierung seine Kosten je Kriterium,
    /// im Automodus die Verstöße je Kriterium (im Grundlauf des besten Plans nach Kosten). Neu berechnet nur, wenn sich der beste Plan geändert
    /// hat, der gerade gilt (im Automodus nach dem Grundlauf der beste nach Stufen), oder die Bewertung wechselt.
    /// </summary>
    private Kriterienwert[] BesteKriterien()
    {
        bool gelenkt = auto is { Grundlauf: false };
        (int, bool) stand = (planVerbesserungen, gelenkt);
        if (besteKriterienStand != stand)
        {
            besteKriterienStand = stand;

            // Im Automodus schon im Grundlauf die Verstöße (des besten Plans nach Kosten), sonst die Kosten je Kriterium.
            besteKriterien = auto switch
            {
                { Grundlauf: false } => auto.BesteKriterien(),
                not null when besteKosten >= 0 => auto.KriterienVon(beste),
                _ => KostenDesBesten(),
            };
        }

        return besteKriterien;
    }

    /// <summary>
    /// Zählt die Verbesserungen des geltenden besten Plans: höchstens eine je Optimierer-Schritt, bei der Kostenoptimierung und
    /// im Grundlauf nach Kosten, im Automodus danach nach Stufen. Der Wechsel vom Grundlauf zur Lenkung zählt nicht. Diese Zahl
    /// zeigen die Kachel „Verbesserungen“, der Verlauf und der Leitstand (ein grüner Punkt je Verbesserung).
    /// </summary>
    private void PlanVerbesserungenZaehlen()
    {
        bool gelenkt = auto is { Grundlauf: false };
        (int Verbesserungen, bool Gelenkt) stand = gelenkt ? (auto!.Verbesserungen, true) : (verbesserungen, false);
        if (stand.Gelenkt == zaehlStand.Gelenkt && stand.Verbesserungen > zaehlStand.Verbesserungen)
        {
            planVerbesserungen++;
        }

        zaehlStand = stand;
    }

    private Kriterienwert[] KostenDesBesten() => besteKosten >= 0 ? Kriterienkosten(beste).ToArray() : [];

    private Insel InselAnlegen(int nummer, RefPlan referenz, Random samen, bool aktiv)
    {
        double startKosten = aktiv ? besteKosten : -1;
        var plaetze = new List<Suchplatz>();
        foreach (string text in einstellungen.Strategien)
        {
            plaetze.Add(new Suchplatz(NeuerPlan(referenz, definition), new Zufall(samen.Next()), Tauschstrategie.Lesen(text), false, startKosten, aktiv));
        }

        Tauschstrategie dynamisch = Tauschstrategie.Lesen(einstellungen.DynamischeStartstrategie);
        for (int j = 0; j < einstellungen.DynamischeJeInsel; j++)
        {
            plaetze.Add(new Suchplatz(NeuerPlan(referenz, definition), new Zufall(samen.Next()), dynamisch, true, startKosten, aktiv));
        }

        return new Insel(nummer, plaetze.ToArray(), beste, startKosten);
    }

    /// <summary>Ein Plan als Lösung (ungültige Termine werden wie beim bestehenden Plan entfernt).</summary>
    private Loesung AlsLoesung(IReadOnlyList<Spiel> spiele)
    {
        RefPlan referenz = RefPlan.Laden(staffel with { BestehenderSpielplan = spiele }, optionen);
        KernPlan plan = NeuerPlan(referenz, new KernDefinition(referenz));
        var loesung = new Loesung(plan.AnzahlErlaubt);
        plan.LoesungSchreiben(loesung);
        return loesung;
    }

    /// <summary>Die ersten Inseln beginnen mit dem Startplan.</summary>
    private void StartplanVerteilen(IReadOnlyList<Spiel> startplan)
    {
        Loesung start = AlsLoesung(startplan);
        double kosten = Bewerten(bewerter, start);
        foreach (Insel insel in inseln.Take(einstellungen.StartplanInseln))
        {
            insel.Neustarten(start, kosten);
        }

        // Automodus: ohne Basisoptimierung – der Startplan ist sofort der beste Plan nach Stufen und Maßstab für C.
        if (kosten < besteKosten || besteKosten < 0 || auto is not null)
        {
            start.KopierenNach(beste);
            besteKosten = kosten;
        }

        auto?.GrundlaufBeenden(start, 0);
    }

    /// <summary>
    /// Original <c>StartSpecialThread</c>/<c>SetMaxOption</c>: die Spezial-Insel übernimmt das Gesamtbeste und rechnet mit einer
    /// neu gewählten, extrem gewichteten Kostenart weiter. Ihr Zähler beginnt dabei neu (Befund #10 korrigiert).
    /// </summary>
    private void SpezialNeuAufsetzen(Insel insel)
    {
        MannschaftsKostenart art = NaechsteSpezialKostenart();
        Gewichtung[] gewichte = optionen.JeKostenart.ToArray();
        gewichte[(int)art] = Gewichtung.ExtremHoch;
        RefPlan referenz = RefPlan.Laden(staffel, optionen with { JeKostenart = gewichte });
        var stammdaten = new KernDefinition(referenz);
        double kosten = Bewerten(NeuerPlan(referenz, stammdaten), beste);
        insel.NeuAufsetzen(beste, kosten, () => NeuerPlan(referenz, stammdaten));
        spezialKostenart = art;
        spezialLaeuft = true;
    }

    /// <summary>
    /// Original <c>getNextMaxGewichtung</c>: zufällig eine der fachlich vorhandenen Kostenarten, die im Gesamtbesten Kosten
    /// verursachen; gibt es keine, Abstand Heim/Auswärts.
    /// </summary>
    private MannschaftsKostenart NaechsteSpezialKostenart()
    {
        KernDefinition d = definition;
        var moegliche = new List<MannschaftsKostenart>();
        if (d.HatAuswaertsKoppeltermine)
        {
            moegliche.Add(MannschaftsKostenart.Auswaertskoppel);
        }

        if (d.HatKoppeltermine)
        {
            moegliche.Add(MannschaftsKostenart.Heimkoppel);
        }

        if (d.HatSetzliste)
        {
            moegliche.Add(MannschaftsKostenart.Setzliste);
        }

        moegliche.Add(MannschaftsKostenart.ParalleleSpiele);
        if (d.HatSechzigKilometer)
        {
            moegliche.Add(MannschaftsKostenart.SechzigKilometerRegel);
        }

        moegliche.Add(MannschaftsKostenart.AbstandHeimAuswaerts);
        moegliche.Add(MannschaftsKostenart.ZweiSpieleProWoche);

        Bewerten(bewerter, beste);
        List<MannschaftsKostenart> kandidaten = moegliche.Where(art => bewerter.MannschaftsKosten(art) > 0).ToList();
        return kandidaten.Count == 0 ? MannschaftsKostenart.AbstandHeimAuswaerts : kandidaten[koordinatorZufall.Next(kandidaten.Count)];
    }

    private void Arbeiten(Suchplatz[] eigene, int nummer, CancellationToken token)
    {
        try
        {
            einstellungen.Fadenstart?.Invoke();
            int index = 0;
            while (!token.IsCancellationRequested)
            {
                laufen.Wait(token);

                // Vorrang für HiGHS: Die letzten Threads ruhen, bis sie wieder gebraucht werden.
                if (nummer >= einstellungen.Kerne - Volatile.Read(ref ruhend))
                {
                    freigabe.Wait(250, token);
                    continue;
                }

                eigene[index].Rechnen();
                index = (index + 1) % eigene.Length;
            }
        }
        catch (OperationCanceledException)
        {
            // regulär beendet
        }
        catch (Exception ex) when (ex is InvalidOperationException or IndexOutOfRangeException or ArgumentException or NullReferenceException)
        {
            Volatile.Write(ref fehler, ex);
            abbruch?.Cancel();
        }
    }

    private void Koordinieren(CancellationToken token)
    {
        try
        {
            while (!token.WaitHandle.WaitOne(AbgleichMs))
            {
                if (!laufen.IsSet)
                {
                    continue;
                }

                lock (sperre)
                {
                    Abgleichen(optimiererSchritt: MitOptimiererSchritt());
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // regulär beendet (Abbruch-Token freigegeben)
        }
        catch (Exception ex) when (ex is InvalidOperationException or IndexOutOfRangeException or ArgumentException or NullReferenceException or ArithmeticException)
        {
            // Wie bei den Rechen-Threads: Die Optimierung endet mit dem Fehler, statt das Programm zu beenden.
            Volatile.Write(ref fehler, ex);
            abbruch?.Cancel();
        }
    }

    /// <summary>Zählt die Abgleiche; jeder fünfte enthält den Optimierer-Schritt (Original ≈ 1 s bei 200 ms je Insel-Poll).</summary>
    private bool MitOptimiererSchritt()
    {
        abgleiche++;
        return abgleiche % AbgleicheJeOptimiererSchritt == 0;
    }

    /// <summary>
    /// Abgleich aller Inseln; beim Optimierer-Schritt (Original <c>TPlanOptimizer.Execute</c>) Übernahme des Gesamtbesten,
    /// Start der Spezial-Insel und Neustart der schlechtesten Insel.
    /// </summary>
    private void Abgleichen(bool optimiererSchritt)
    {
        foreach (Insel insel in inseln)
        {
            insel.Abgleichen();
        }

        spezial?.Abgleichen();
        if (!optimiererSchritt)
        {
            return;
        }

        Insel? schlechteste = null;
        bool besser = false;
        foreach (Insel insel in inseln)
        {
            if (insel.Kosten < 0)
            {
                continue;
            }

            if (insel.Kosten < besteKosten || besteKosten < 0)
            {
                insel.Loesung.KopierenNach(beste);
                besteKosten = insel.Kosten;
                besser = true;
            }

            if (schlechteste is null || insel.Kosten > schlechteste.Kosten)
            {
                schlechteste = insel;
            }
        }

        if (besser)
        {
            verbesserungen++;
        }

        if (auto is not null)
        {
            AutoSchritt();
        }

        PlanVerbesserungenZaehlen();

        if (spezial is not null && !spezialLaeuft && Durchlaeufe > einstellungen.DurchlaeufeVorSpezialInsel)
        {
            SpezialNeuAufsetzen(spezial);
        }

        if (inseln.Length < 2 || schlechteste is null || schlechteste.OhneVerbesserung <= einstellungen.DurchlaeufeVorNeustart)
        {
            return;
        }

        if (spezial is not null && spezialLaeuft && spezial.Kosten >= 0 && spezial.SeitNeustart > einstellungen.MindestDurchlaeufeSpezialInsel)
        {
            // Die schlechteste Insel startet mit der Lösung der Spezial-Insel, bewertet mit den normalen Gewichtungen.
            schlechteste.Neustarten(spezial.Loesung, Bewerten(bewerter, spezial.Loesung));
            spezialVerwendet++;
            SpezialNeuAufsetzen(spezial);
        }
        else
        {
            schlechteste.Neustarten(leer, -1);
        }
    }

    /// <summary>Sekunden reiner Rechenzeit für den Automodus (bei fester Zahl von Durchläufen anteilig am Zeitbudget).</summary>
    private double AutoSekunden() => rechnenZiel > 0 ? Durchlaeufe / (double)rechnenZiel * autoBudget : laufzeit.Elapsed.TotalSeconds;

    /// <summary>
    /// Automodus, bei jedem Optimierer-Schritt: Grundlauf beenden, Inseln nach Stufen bewerten und bei Stillstand
    /// die Gewichte ändern.
    /// </summary>
    private void AutoSchritt()
    {
        // Der Automodus beobachtet die Verstöße über die reine Rechenzeit (ohne Pausen); er kennt keine Zeitgrenzen. Bei einer
        // festen Zahl von Durchläufen (reproduzierbar) entspricht der ganze Lauf dem Zeitbudget bzw. 300 s.
        double f = AutoSekunden();
        auto!.Zaehlen(Durchlaeufe);
        auto.Beobachten(besteKosten, f);
        if (auto.Grundlauf)
        {
            // Nach einem Wechsel von der Kostenoptimierung ist der Plan schon optimiert: gleich mit Stufe A beginnen.
            // Liegt der Plan aus HiGHS vor, endet sie sofort (frühestens nach 10 s): Er wird gleich geprüft und angezeigt.
            bool highs = highsPlan is not null && (einstellungen.AutoOhneGrundlauf || auto.GrundlaufLangGenug(f));
            if (besteKosten >= 0 && (einstellungen.AutoOhneGrundlauf || highs || auto.GrundlaufVorbei(f)))
            {
                auto.GrundlaufBeenden(beste, f);
                if (highs)
                {
                    auto.Melden($"{f:0} s Plan aus HiGHS liegt vor: Basisoptimierung beendet");
                }
            }

            if (auto.Grundlauf || !highs)
            {
                return;
            }
        }

        if (highsPlan is { } plan)
        {
            highsPlan = null;
            if (auto.Einspielen(plan, f, "Plan aus HiGHS", sofortLenken: true))
            {
                // Die schlechtere Hälfte der Inseln beginnt mit dem Plan aus HiGHS, die andere sucht weiter.
                double kosten = Bewerten(bewerter, plan);
                foreach (Insel insel in inseln.OrderByDescending(i => i.Kosten < 0 ? double.MaxValue : i.Kosten).Take((inseln.Length + 1) / 2))
                {
                    insel.Neustarten(plan, kosten);
                }
            }
        }

        for (int i = 0; i < inseln.Length; i++)
        {
            Insel insel = inseln[i];
            if (insel.Kosten >= 0 && insel.Stand != autoStand[i])
            {
                autoStand[i] = insel.Stand;
                autoWerte[i] = auto.PruefenVonInsel(insel.Loesung, insel.Nummer);
                autoVerursacher[i] = auto.Verursacher(Verursacher);
            }
        }

        if (besteKosten >= 0)
        {
            auto.Verfolgen(beste, f);
        }

        if (besteKosten >= 0 && auto.Lenken(beste, f) is { } neu)
        {
            GewichteWechseln(neu);
        }
    }

    /// <summary>
    /// Neue Gewichtungen für alle Inseln (wie ein Anwender, der während der Rechnung die Gewichte ändert): Jede Insel rechnet
    /// mit neuen Plänen ab dem besten Plan nach Stufen weiter.
    /// </summary>
    private void GewichteWechseln(Berechnungsoptionen neu)
    {
        optionen = neu;
        RefPlan referenz = RefPlan.Laden(staffel, neu);
        var stammdaten = new KernDefinition(referenz);
        definition = stammdaten;
        bewerter = NeuerPlan(referenz, stammdaten);

        // Normalerweise rechnet die Suche mit ihrem Plan weiter; beim Glätten beginnt sie beim besten Plan neu.
        if (auto!.NeustartVomBesten)
        {
            auto.Beste.KopierenNach(beste);
            auto.NeustartVomBesten = false;
        }

        besteKosten = Bewerten(bewerter, beste);
        foreach (Insel insel in inseln)
        {
            insel.NeuAufsetzen(beste, besteKosten, () => NeuerPlan(referenz, stammdaten));
        }
    }
}
