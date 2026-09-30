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
    private readonly KernDefinition definition;
    private readonly Staffel staffel;
    private readonly Berechnungsoptionen optionen;
    private readonly Insel[] inseln;
    private readonly Insel? spezial;
    private readonly Suchplatz[] alle;
    private readonly Loesung beste;
    private readonly Loesung leer;
    private readonly KernPlan bewerter;
    private readonly Random koordinatorZufall;
    private readonly ManualResetEventSlim laufen = new(true);
    private readonly Stopwatch uhr = new();
    private readonly List<Thread> threads = [];
    private CancellationTokenSource? abbruch;
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
        beste = new Loesung(bewerter.AnzahlErlaubt);
        bewerter.LoesungSchreiben(beste);
        leer = new Loesung(bewerter.AnzahlErlaubt);

        var samen = new Random(this.einstellungen.Startwert ?? Random.Shared.Next());
        koordinatorZufall = new Random(samen.Next());
        inseln = new Insel[this.einstellungen.Inseln];
        for (int i = 0; i < inseln.Length; i++)
        {
            inseln[i] = InselAnlegen(i + 1, referenz, samen, aktiv: true);
        }

        if (this.einstellungen.SpezialInsel)
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
            }
            else
            {
                Volatile.Write(ref durchlaeufeBeiMessbeginn, Durchlaeufe);
                uhr.Restart();
                laufen.Set();
            }
        }
    }

    /// <summary>Fehler, an dem ein Rechen-Thread abgebrochen ist; dann ist die Optimierung beendet.</summary>
    public Exception? Fehler => Volatile.Read(ref fehler);

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
            var spiele = new List<Spiel>(beste.Datum.Length);
            for (int s = 0; s < beste.Datum.Length; s++)
            {
                DateTime? zeitpunkt = beste.Datum[s] != 0.0 ? DelphiDatum.ZuDateTime(beste.Datum[s]) : null;
                spiele.Add(new Spiel(zeitpunkt, definition.Name[definition.SpielHeim[s]], definition.Name[definition.SpielGast[s]], string.Empty));
            }

            return new Optimierungsergebnis(besteKosten, verbesserungen, spiele);
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
}
