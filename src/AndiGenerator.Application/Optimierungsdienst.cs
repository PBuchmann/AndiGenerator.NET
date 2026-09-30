// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;

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
    private Inseloptimierer? optimierer;
    private CancellationTokenSource? beobachtung;
    private Thread? beobachter;
    private long fruehereDurchlaeufe;
    private int fruehereVerbesserungen;
    private double letzteKosten = -1;
    private bool pausiert;

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
            NeuStarten(staffel with { BestehenderSpielplan = ausgangsplan ?? [] }, optionen, ausgangsplan is { Count: > 0 });
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
            optimierer.Dispose();
            NeuStarten(staffel with { BestehenderSpielplan = bisher.Spiele }, optionen, ausgangsplanMelden: true);
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

        Optimierungsergebnis bester = optimierer.BesterStand();
        return new Optimierungsstand(
            true,
            pausiert,
            fruehereDurchlaeufe + optimierer.Durchlaeufe,
            optimierer.PlaeneProSekunde,
            bester.Kosten,
            fruehereVerbesserungen + bester.Verbesserungen,
            seitVerbesserung.Elapsed,
            optimierer.SpezialKostenart,
            bester.Spiele);
    }

    private void NeuStarten(Staffel staffel, Berechnungsoptionen optionen, bool ausgangsplanMelden)
    {
        optimierer = new Inseloptimierer(staffel, optionen, einstellungen);
        optimierer.Pausiert = pausiert;
        optimierer.Starten();

        // Ein vorgegebener Ausgangsplan (Start mit gemerktem Plan, neu bewerteter Plan nach einer Datenänderung) wird
        // sofort gemeldet: Er ist der beste Plan, und ohne Meldung blieben Ansichten und Pflichtregeln leer, bis er
        // übertroffen wird. Ohne Ausgangsplan (noch keine Termine) wird erst der erste gefundene Plan gemeldet.
        letzteKosten = ausgangsplanMelden ? -1 : optimierer.BesterStand().Kosten;
        seitVerbesserung.Restart();
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

                double kosten = optimierer.BesterStand().Kosten;
                if (kosten >= 0 && (kosten < letzteKosten || letzteKosten < 0))
                {
                    letzteKosten = kosten;
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
