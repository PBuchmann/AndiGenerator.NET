// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using AndiGenerator.Application;
using AndiGenerator.Engine.Inseln;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>
/// Verlauf der Suche seit dem Start, im Takt der Statusabfrage: Kosten bzw. Verstöße A, B und C, Pläne je Sekunde, Phase
/// des Automodus. Damit auch eine stundenlange Suche in das Diagramm passt, wird der Verlauf ab
/// <see cref="HoechstePunkte"/> Punkten auf die halbe Auflösung verdichtet.
/// </summary>
public sealed class Suchverlauf : ObservableObject
{
    /// <summary>Höchstens so viele Punkte; darüber wird jeder zweite verworfen.</summary>
    public const int HoechstePunkte = 800;

    private readonly List<Verlaufspunkt> punkte = [];
    private long zuletzt;
    private double sekunden;
    private int version;

    /// <summary>Holt die Punkte in zeitlicher Reihenfolge.</summary>
    public IReadOnlyList<Verlaufspunkt> Punkte => punkte;

    /// <summary>Holt eine Zahl, die sich mit jedem neuen Punkt ändert (zum Neuzeichnen).</summary>
    public int Version
    {
        get => version;
        private set => SetProperty(ref version, value);
    }

    /// <summary>Beginnt einen neuen Verlauf (Start einer Generierung).</summary>
    public void Zuruecksetzen()
    {
        punkte.Clear();
        zuletzt = 0;
        sekunden = 0;
        Version++;
    }

    /// <summary>Übernimmt den Stand der Generierung; in der Pause läuft die Zeit nicht weiter.</summary>
    /// <param name="stand">Der Stand.</param>
    public void Hinzufuegen(Optimierungsstand stand)
    {
        ArgumentNullException.ThrowIfNull(stand);
        long jetzt = Stopwatch.GetTimestamp();
        if (zuletzt != 0 && !stand.Pausiert)
        {
            sekunden += Stopwatch.GetElapsedTime(zuletzt, jetzt).TotalSeconds;
        }

        zuletzt = jetzt;
        if (!stand.Laeuft || stand.Pausiert)
        {
            return;
        }

        Stufenwert? bester = stand.Lenkung is { Basis: not null } lenkung ? lenkung.Bester : null;
        punkte.Add(new Verlaufspunkt(
            sekunden,
            stand.Kosten,
            bester?.A,
            bester?.B,
            bester?.CAnzahl,
            stand.PlaeneProSekunde,
            stand.Lenkung?.Bezeichnung ?? string.Empty,
            stand.Lenkung?.Anhebungen ?? 0));
        if (punkte.Count > HoechstePunkte)
        {
            Verdichten();
        }

        Version++;
    }

    /// <summary>Verwirft jeden zweiten Punkt; der erste und der letzte bleiben.</summary>
    private void Verdichten()
    {
        for (int i = punkte.Count - 2; i > 0; i -= 2)
        {
            punkte.RemoveAt(i);
        }
    }
}
