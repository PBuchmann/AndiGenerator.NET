// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using AndiGenerator.Import;

namespace AndiGenerator.Application;

/// <summary>Misst die <see cref="Prozessorlast"/> fortlaufend (höchstens alle 0,4 s neu, dazwischen gilt die letzte).</summary>
internal sealed class Prozessormessung
{
    private static readonly TimeSpan Abstand = TimeSpan.FromSeconds(0.4);

    private readonly Lock sperre = new();
    private (TimeSpan Suche, TimeSpan Highs, TimeSpan Gesamt)? vorher;
    private long zeitVorher;
    private Prozessorlast? letzte;

    /// <summary>Die aktuelle Aufteilung; <c>null</c>, solange (oder wenn) nicht gemessen werden kann.</summary>
    /// <returns>Die Last.</returns>
    public Prozessorlast? Messen()
    {
        lock (sperre)
        {
            long jetzt = Stopwatch.GetTimestamp();
            if (vorher is not null && Stopwatch.GetElapsedTime(zeitVorher, jetzt) < Abstand)
            {
                return letzte;
            }

            if (Rechenfaeden.Messen() is not { } neu)
            {
                return null;
            }

            if (vorher is { } alt)
            {
                double kapazitaet = Stopwatch.GetElapsedTime(zeitVorher, jetzt).Ticks * (double)Environment.ProcessorCount;
                double suche = Anteil(neu.Suche - alt.Suche, kapazitaet);
                double highs = Anteil(neu.Highs - alt.Highs, kapazitaet);
                double gesamt = Anteil(neu.Gesamt - alt.Gesamt, kapazitaet);
                letzte = new Prozessorlast(suche, highs, Math.Clamp(gesamt - suche - highs, 0, 1), Environment.ProcessorCount);
            }

            vorher = neu;
            zeitVorher = jetzt;
            return letzte;
        }
    }

    /// <summary>Beginnt neu (neue Generierung).</summary>
    public void Zuruecksetzen()
    {
        lock (sperre)
        {
            vorher = null;
            letzte = null;
        }
    }

    private static double Anteil(TimeSpan zeit, double kapazitaet) => kapazitaet > 0 ? Math.Clamp(zeit.Ticks / kapazitaet, 0, 1) : 0;
}
