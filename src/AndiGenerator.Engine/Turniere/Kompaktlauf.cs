// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Ein Lauf des Kompaktplaners: zuerst, wo es geht, die vollständige Suche nach dem kürzesten Plan nur aus 4er-Spieltagen
/// (<see cref="Viererbaum"/>), danach mehrere zufällige Suchen parallel bis zum Ende der Zeit (auch mit 3er-Spieltagen).
/// Findet sich kein Plan, werden schrittweise Verstöße gegen die Meldungen erlaubt, erst danach Mannschaften ohne
/// Heimspieltag.
/// </summary>
internal sealed class Kompaktlauf(Kompaktmodell modell, Kompaktplanoptionen optionen, CancellationToken abbruch, Action<string>? fortschritt)
{
    /// <summary>Höchstzahl erlaubter Verstöße gegen die Meldungen.</summary>
    public const int MaxVerstoesse = 6;

    private const int VersucheJeStufe = 200;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private readonly Stopwatch uhr = new();
    private readonly Lock sperre = new();
    private long geprueft;
    private int stufe;

    /// <summary>Holt die besten Pläne.</summary>
    public Kompaktfront Front { get; } = new();

    /// <summary>Holt die Zahl der Suchversuche.</summary>
    public long Geprueft => Interlocked.Read(ref geprueft);

    /// <summary>Holt den kürzesten Plan mit 3er-Spieltagen (ohne Verstöße), oder <c>null</c>.</summary>
    public Kompaktloesung? Gemischt { get; private set; }

    /// <summary>Holt die vollständige Suche nach 4er-Plänen, oder <c>null</c>, wenn sie für diese Aufgabe nicht geht.</summary>
    public Viererbaum? Baum { get; private set; }

    /// <summary>Sucht, bis die Zeit abgelaufen, die Zahl der Versuche erreicht oder der Lauf abgebrochen ist.</summary>
    public void Ausfuehren()
    {
        int threads = Math.Max(1, optionen.Threads);
        if (Viererbaum.Moeglich(modell))
        {
            Baum = new Viererbaum(modell);
            Baum.Suchen(threads, optionen.ExaktDauer, abbruch, fortschritt);
            if (Baum.Beste is IReadOnlyList<Kompaktkandidat> turniere)
            {
                Aufnehmen(new Kompaktloesung(modell, turniere));
            }
        }

        uhr.Start();
        if (threads == 1)
        {
            Arbeiten(0);
            return;
        }

        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, Arbeiten);
    }

    /// <summary>Erlaubte Verstöße und Mannschaften ohne Heimspieltag einer Stufe.</summary>
    private (int Verstoesse, int OhneAusrichtung) Grenzen(int s) =>
        s <= MaxVerstoesse
            ? (s, modell.MindestensOhneAusrichtung)
            : (MaxVerstoesse, Math.Min(modell.N, modell.MindestensOhneAusrichtung + s - MaxVerstoesse));

    private void Arbeiten(int nummer)
    {
        var suche = new Kompaktsuche(modell, unchecked(optionen.Startwert + (nummer * 7919)));
        int versuche = 0;
        while (Weiter(versuche))
        {
            versuche++;
            int s = Volatile.Read(ref stufe);
            (int verstoesse, int ohne) = Grenzen(s);

            // Jeder zweite Versuch nur mit 4er-Spieltagen, wenn das rechnerisch aufgehen kann.
            bool nurVierer = modell.NurViererMoeglich && versuche % 2 == 0;
            IReadOnlyList<Kompaktkandidat>? turniere = suche.Versuch(verstoesse, ohne, nurVierer);
            long anzahl = Interlocked.Increment(ref geprueft);
            if (turniere is not null)
            {
                Aufnehmen(new Kompaktloesung(modell, turniere));
            }
            else
            {
                Lockern(s, versuche);
            }

            if (anzahl % 128 == 0)
            {
                fortschritt?.Invoke(string.Create(Deutsch, $"Weitere Varianten (auch mit 3er-Spieltagen): {anzahl:N0} Versuche …"));
            }
        }
    }

    private bool Weiter(int versuche) =>
        !abbruch.IsCancellationRequested && (optionen.Versuche is int hoechstens ? versuche < hoechstens : uhr.Elapsed < optionen.Dauer);

    /// <summary>Ohne gefundenen Plan nach einer gewissen Zeit die nächste Stufe erlauben.</summary>
    private void Lockern(int s, int versuche)
    {
        if (!Front.Leer || s >= MaxVerstoesse + modell.N)
        {
            return;
        }

        bool faellig = optionen.Versuche is not null
            ? versuche % VersucheJeStufe == 0
            : uhr.Elapsed > optionen.Dauer * (s + 1) / 16;
        if (faellig)
        {
            Interlocked.CompareExchange(ref stufe, s + 1, s);
        }
    }

    private void Aufnehmen(Kompaktloesung loesung)
    {
        Kompaktloesung ausgeglichen = Kompaktverbesserung.EinzelAusgleichen(modell, loesung);
        if (ausgeglichen.Dreier > 0 && ausgeglichen.Verstoesse == 0 && ausgeglichen.OhneHeimspieltag <= modell.MindestensOhneAusrichtung)
        {
            lock (sperre)
            {
                if (Gemischt is null || ausgeglichen.Km < Gemischt.Km)
                {
                    Gemischt = ausgeglichen;
                }
            }
        }

        if (!Front.Hinzufuegen(ausgeglichen))
        {
            return;
        }

        Front.Hinzufuegen(Kompaktverbesserung.TageVerteilen(modell, ausgeglichen, Kompaktverbesserung.Stand));
        Front.Hinzufuegen(Kompaktverbesserung.TageVerteilen(modell, ausgeglichen, Kompaktverbesserung.Gemeinsam));
    }
}
