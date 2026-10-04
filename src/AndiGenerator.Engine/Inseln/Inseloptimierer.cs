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
    private readonly Random koordinatorZufall;
    private readonly ManualResetEventSlim laufen = new(true);
    private readonly Stopwatch uhr = new();

    /// <summary>Reine Rechenzeit ohne Pausen (für den Automodus, der nach Sekunden lenkt).</summary>
    private readonly Stopwatch laufzeit = new();
    private readonly List<Thread> threads = [];
    private CancellationTokenSource? abbruch;
    private KernDefinition definition;
    private Berechnungsoptionen optionen;
    private KernPlan bewerter;
    private long rechnenZiel;
    private double besteKosten;
    private int verbesserungen;
    private int abgleiche;
    private bool spezialLaeuft;
    private int spezialVerwendet;
    private MannschaftsKostenart? spezialKostenart;
    private long durchlaeufeBeiMessbeginn;
    private Exception? fehler;

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
        Array.Fill(autoStand, -1);
        for (int i = 0; i < inseln.Length; i++)
        {
            inseln[i] = InselAnlegen(i + 1, referenz, samen, aktiv: true);
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

    /// <summary>Fehler, an dem ein Rechen-Thread abgebrochen ist; dann ist die Optimierung beendet.</summary>
    public Exception? Fehler => Volatile.Read(ref fehler);

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
        for (int k = 0; k < kerne; k++)
        {
            Suchplatz[] eigene = alle.Where((_, index) => index % kerne == k).ToArray();
            threads.Add(ThreadStarten(() => Arbeiten(eigene, token), $"Suche {k + 1}", ThreadPriority.BelowNormal));
        }

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

            return new Optimierungsergebnis(gelenkt ? auto!.BesteKosten : besteKosten, verbesserungen + (auto?.Verbesserungen ?? 0), spiele);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stoppen();
        laufen.Dispose();
    }

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

    private void Arbeiten(Suchplatz[] eigene, CancellationToken token)
    {
        try
        {
            int index = 0;
            while (!token.IsCancellationRequested)
            {
                laufen.Wait(token);
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
                verbesserungen++;
            }

            if (schlechteste is null || insel.Kosten > schlechteste.Kosten)
            {
                schlechteste = insel;
            }
        }

        if (auto is not null)
        {
            AutoSchritt();
        }

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
        auto!.Beobachten(besteKosten, f);
        if (auto.Grundlauf)
        {
            // Nach einem Wechsel von der Kostenoptimierung ist der Plan schon optimiert: gleich mit Stufe A beginnen.
            if (besteKosten >= 0 && (einstellungen.AutoOhneGrundlauf || auto.GrundlaufVorbei(f)))
            {
                auto.GrundlaufBeenden(beste, f, sofortLenken: einstellungen.AutoOhneGrundlauf);
            }

            return;
        }

        for (int i = 0; i < inseln.Length; i++)
        {
            Insel insel = inseln[i];
            if (insel.Kosten >= 0 && insel.Stand != autoStand[i])
            {
                autoStand[i] = insel.Stand;
                auto.Pruefen(insel.Loesung);
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
