// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Steuert eine laufende Planung der Kompaktspieltage (nicht im Original): anhalten, fortsetzen, beenden und den bisher
/// besten Stand abfragen. Während der Pause läuft keine Zeit ab; die Höchstdauern der Suchschritte gelten für die
/// tatsächliche Rechenzeit (<see cref="Laufzeit"/>).
/// </summary>
public sealed class Kompaktsteuerung : IDisposable
{
    private readonly CancellationTokenSource stopp;
    private readonly ManualResetEventSlim weiter = new(true);
    private readonly Stopwatch laufzeit = Stopwatch.StartNew();
    private readonly Lock sperre = new();

    /// <summary>Initialisiert eine Steuerung.</summary>
    public Kompaktsteuerung()
        : this(CancellationToken.None)
    {
    }

    /// <summary>Initialisiert eine Steuerung, die auch ein äußerer Abbruch beendet.</summary>
    /// <param name="abbruch">Äußerer Abbruch.</param>
    public Kompaktsteuerung(CancellationToken abbruch)
    {
        stopp = CancellationTokenSource.CreateLinkedTokenSource(abbruch);
    }

    /// <summary>Holt das Signal „beenden“.</summary>
    public CancellationToken Token => stopp.Token;

    /// <summary>Holt einen Wert, der angibt, ob die Planung angehalten ist.</summary>
    public bool Pausiert => !weiter.IsSet;

    /// <summary>Holt die Rechenzeit seit dem Start ohne die Pausen.</summary>
    public TimeSpan Laufzeit => laufzeit.Elapsed;

    /// <summary>Holt oder setzt, wie der bisher beste Stand gebildet wird (setzt der Planer während des Laufs).</summary>
    internal Func<Kompaktergebnis>? Zwischenstandquelle { get; set; }

    /// <summary>Hält die Planung an; die Suchschritte warten an der nächsten Gelegenheit.</summary>
    public void Pausieren()
    {
        lock (sperre)
        {
            if (weiter.IsSet && !stopp.IsCancellationRequested)
            {
                laufzeit.Stop();
                weiter.Reset();
            }
        }
    }

    /// <summary>Setzt eine angehaltene Planung fort.</summary>
    public void Fortsetzen()
    {
        lock (sperre)
        {
            if (!weiter.IsSet)
            {
                laufzeit.Start();
                weiter.Set();
            }
        }
    }

    /// <summary>Beendet die Planung; sie liefert die bis dahin besten Pläne.</summary>
    public void Stoppen()
    {
        stopp.Cancel();
        Fortsetzen();
    }

    /// <summary>Der bisher beste Stand (z. B. in der Pause), oder <c>null</c>, wenn die Planung noch nicht läuft.</summary>
    /// <returns>Varianten und Hinweise wie am Ende der Planung.</returns>
    public Kompaktergebnis? Zwischenstand() => Zwischenstandquelle?.Invoke();

    /// <inheritdoc/>
    public void Dispose()
    {
        stopp.Dispose();
        weiter.Dispose();
    }

    /// <summary>Wartet, solange die Planung angehalten ist (endet sofort beim Beenden).</summary>
    internal void Warten()
    {
        if (weiter.IsSet)
        {
            return;
        }

        try
        {
            weiter.Wait(stopp.Token);
        }
        catch (OperationCanceledException)
        {
            // Beendet: die Suche merkt es selbst am Signal.
        }
    }
}
