// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Concurrent;

namespace AndiGenerator.Mcp;

/// <summary>
/// Der „Oberflächen-Thread“ des Servers: Alles, was das Hauptfenster-Modell anfasst, läuft nacheinander auf diesem einen
/// Faden – wie in der Oberfläche. Werkzeugaufrufe der KI kommen aus beliebigen Fäden und werden hierher übergeben; Fortsetzungen
/// nach <c>await</c> bleiben hier (eigener Synchronisationskontext).
/// </summary>
internal sealed class Bedienfaden : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Aufgabe, object? Zustand)> schlange = [];
    private readonly Thread faden;

    /// <summary>Startet den Faden.</summary>
    public Bedienfaden()
    {
        faden = new Thread(Schleife) { IsBackground = true, Name = "Bedienfaden" };
        faden.Start();
    }

    /// <summary>Holt den zuletzt aufgetretenen Fehler einer Aufgabe ohne Rückgabe (z. B. aus einer Meldung der Generierung).</summary>
    public Exception? LetzterFehler { get; private set; }

    /// <inheritdoc/>
    public override void Post(SendOrPostCallback d, object? state)
    {
        // Nach dem Beenden melden sich noch Rechen-Fäden (z. B. HiGHS); ihre Meldungen verfallen.
        try
        {
            schlange.Add((d, state));
        }
        catch (Exception fehler) when (fehler is InvalidOperationException or ObjectDisposedException)
        {
            // Beendet.
        }
    }

    /// <inheritdoc/>
    public override void Send(SendOrPostCallback d, object? state)
    {
        if (Environment.CurrentManagedThreadId == faden.ManagedThreadId)
        {
            d(state);
            return;
        }

        Ausfuehren(() =>
        {
            d(state);
            return true;
        }).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public override SynchronizationContext CreateCopy() => this;

    /// <summary>Führt eine Arbeit auf dem Faden aus und liefert ihr Ergebnis.</summary>
    /// <typeparam name="T">Art des Ergebnisses.</typeparam>
    /// <param name="arbeit">Die Arbeit.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<T> Ausfuehren<T>(Func<T> arbeit) => Ausfuehren(() => Task.FromResult(arbeit()));

    /// <summary>Führt eine asynchrone Arbeit auf dem Faden aus (Fortsetzungen ebenfalls dort) und liefert ihr Ergebnis.</summary>
    /// <typeparam name="T">Art des Ergebnisses.</typeparam>
    /// <param name="arbeit">Die Arbeit.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<T> Ausfuehren<T>(Func<Task<T>> arbeit)
    {
        var ergebnis = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(
            _ =>
            {
                Task<T> lauf;
                try
                {
                    lauf = arbeit();
                }
                catch (Exception fehler)
                {
                    ergebnis.SetException(fehler);
                    return;
                }

                lauf.ContinueWith(
                    t =>
                    {
                        if (t.IsFaulted)
                        {
                            ergebnis.SetException(t.Exception.InnerExceptions);
                        }
                        else if (t.IsCanceled)
                        {
                            ergebnis.SetCanceled();
                        }
                        else
                        {
                            ergebnis.SetResult(t.Result);
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            },
            null);
        return ergebnis.Task;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        schlange.CompleteAdding();
        faden.Join(TimeSpan.FromSeconds(5));
        schlange.Dispose();
    }

    private void Schleife()
    {
        SetSynchronizationContext(this);
        foreach ((SendOrPostCallback aufgabe, object? zustand) in schlange.GetConsumingEnumerable())
        {
            try
            {
                aufgabe(zustand);
            }
            catch (Exception fehler)
            {
                // Ein Fehler in einer Meldung darf den Faden nicht beenden; das nächste „status“ nennt ihn.
                LetzterFehler = fehler;
            }
        }
    }
}
