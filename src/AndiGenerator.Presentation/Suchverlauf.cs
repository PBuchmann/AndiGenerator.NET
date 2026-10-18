// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using AndiGenerator.Application;
using AndiGenerator.Engine.Inseln;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>
/// Verlauf der Suche seit dem Start, im Takt der Statusabfrage: Kosten bzw. Verstöße A, B und C, die Grenzen aus HiGHS, Pläne
/// je Sekunde, die Aufteilung des Prozessors und die Phase des Automodus; dazu jede Verbesserung mit Herkunft und Änderungen
/// und die Meilensteine (Basisoptimierung fertig, A bzw. B bestmöglich, Festlegung). Damit auch eine stundenlange Suche in das
/// Diagramm passt, werden die Punkte ab <see cref="HoechstePunkte"/> auf die halbe Auflösung verdichtet.
/// </summary>
public sealed class Suchverlauf : ObservableObject
{
    /// <summary>Höchstens so viele Punkte; darüber wird jeder zweite verworfen.</summary>
    public const int HoechstePunkte = 800;

    /// <summary>Höchstens so viele Verbesserungen; darüber wird jede zweite (außer der letzten) verworfen.</summary>
    public const int HoechsteVerbesserungen = 2000;

    private readonly List<Verlaufspunkt> punkte = [];
    private readonly List<Verlaufsverbesserung> verbesserungen = [];
    private readonly List<Meilenstein> meilensteine = [];
    private IReadOnlyList<Kriterienwert>? kriterienVorher;
    private int verbesserungenVorher = -1;
    private bool basisVorher;
    private bool aBestmoeglich;
    private bool bBestmoeglich;
    private string? festgelegtVorher;
    private long zuletzt;
    private double sekunden;
    private int version;

    /// <summary>Holt die Punkte in zeitlicher Reihenfolge.</summary>
    public IReadOnlyList<Verlaufspunkt> Punkte => punkte;

    /// <summary>Holt die Verbesserungen des besten Plans in zeitlicher Reihenfolge.</summary>
    public IReadOnlyList<Verlaufsverbesserung> Verbesserungen => verbesserungen;

    /// <summary>Holt die Meilensteine in zeitlicher Reihenfolge.</summary>
    public IReadOnlyList<Meilenstein> Meilensteine => meilensteine;

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
        verbesserungen.Clear();
        meilensteine.Clear();
        kriterienVorher = null;
        verbesserungenVorher = -1;
        basisVorher = false;
        aBestmoeglich = false;
        bBestmoeglich = false;
        festgelegtVorher = null;
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
        Aufnehmen(stand);
    }

    /// <summary>Übernimmt den Stand mit vorgegebener Rechenzeit seit dem Start (für Tests und Bildschirmfotos).</summary>
    /// <param name="stand">Der Stand.</param>
    /// <param name="sekundenSeitStart">Rechenzeit seit dem Start.</param>
    public void Hinzufuegen(Optimierungsstand stand, double sekundenSeitStart)
    {
        ArgumentNullException.ThrowIfNull(stand);
        sekunden = sekundenSeitStart;
        Aufnehmen(stand);
    }

    /// <summary>
    /// Verstöße je Stufe des besten Plans: im Automodus nach der Basisoptimierung die Bewertung nach Stufen, in der
    /// Basisoptimierung die Verstöße des besten Plans nach Kosten; bei der Kostenoptimierung keine.
    /// </summary>
    private static (int? A, int? B, int? C) Stufen(Optimierungsstand stand)
    {
        if (stand.Lenkung is { Basis: not null } lenkung)
        {
            return (lenkung.Bester.A, lenkung.Bester.B, lenkung.Bester.CAnzahl);
        }

        if (stand.Lenkung is not null && stand.Maschinenraum?.BesterPlan is { Count: > 0 } kriterien && kriterien[0].Stufe is not null)
        {
            int Summe(Stufe stufe) => (int)Math.Round(kriterien.Where(k => k.Stufe == stufe).Sum(k => k.Wert));
            return (Summe(Stufe.A), Summe(Stufe.B), Summe(Stufe.C));
        }

        return (null, null, null);
    }

    /// <summary>Herkunft der letzten Verbesserung: Inseln, HiGHS oder Teilneuplanung.</summary>
    private static string Herkunft(string? zuletztVon) => zuletztVon switch
    {
        "HiGHS" => "HiGHS",
        "Teilneuplanung" => "Teilneuplanung",
        _ => "Inseln",
    };

    /// <summary>Nimmt den Stand als Punkt auf (nicht in der Pause) und merkt sich Verbesserungen und Meilensteine.</summary>
    private void Aufnehmen(Optimierungsstand stand)
    {
        if (!stand.Laeuft || stand.Pausiert)
        {
            return;
        }

        (int? a, int? b, int? c) = Stufen(stand);
        Lenkungsstand? l = stand.Lenkung;
        Prozessorlast? last = stand.Last;
        var punkt = new Verlaufspunkt(
            sekunden,
            stand.Kosten,
            a,
            b,
            c,
            stand.PlaeneProSekunde,
            l?.Bezeichnung ?? string.Empty,
            l?.Anhebungen ?? 0,
            l?.SchrankeA,
            l?.SchrankeB,
            last?.Suche ?? 0,
            last?.Highs ?? 0,
            last?.Sonstiges ?? 0);
        punkte.Add(punkt);
        if (punkte.Count > HoechstePunkte)
        {
            Verdichten();
        }

        VerbesserungMerken(stand, punkt);
        MeilensteineMerken(l, punkt);
        Version++;
    }

    /// <summary>Merkt sich eine neue Verbesserung (die Zahl der Kachel „Verbesserungen“ ist gestiegen).</summary>
    private void VerbesserungMerken(Optimierungsstand stand, Verlaufspunkt punkt)
    {
        IReadOnlyList<Kriterienwert>? kriterien = stand.Maschinenraum?.BesterPlan;
        if (verbesserungenVorher >= 0 && stand.Verbesserungen > verbesserungenVorher && stand.Kosten >= 0)
        {
            verbesserungen.Add(new Verlaufsverbesserung(
                punkt.Sekunden,
                stand.Verbesserungen,
                Herkunft(stand.Lenkung?.ZuletztVon),
                punkt.A,
                punkt.B,
                punkt.C,
                stand.Kosten,
                Schaubild.Vergleichen(kriterienVorher, kriterien)));
            if (verbesserungen.Count > HoechsteVerbesserungen)
            {
                for (int i = verbesserungen.Count - 2; i > 0; i -= 2)
                {
                    verbesserungen.RemoveAt(i);
                }
            }
        }

        verbesserungenVorher = stand.Verbesserungen;
        kriterienVorher = kriterien ?? kriterienVorher;
    }

    /// <summary>Meilensteine: Ende der Basisoptimierung, A bzw. B bei der Grenze aus HiGHS, Festlegung und ihr Aufheben.</summary>
    private void MeilensteineMerken(Lenkungsstand? l, Verlaufspunkt punkt)
    {
        if (l is null)
        {
            return;
        }

        void Hinzu(string text) => meilensteine.Add(new Meilenstein(punkt.Sekunden, text));
        if (l.Basis is not null && !basisVorher && punkte.Count > 1)
        {
            Hinzu("Basisoptimierung fertig");
        }

        basisVorher = l.Basis is not null;
        if (!aBestmoeglich && punkt is { A: int a, SchrankeA: int sa } && a <= sa)
        {
            aBestmoeglich = true;
            Hinzu("A bestmöglich");
        }

        if (!bBestmoeglich && aBestmoeglich && punkt is { B: int b, SchrankeB: int sb } && b <= sb)
        {
            bBestmoeglich = true;
            Hinzu("B bestmöglich");
        }

        if (l.Festgelegt != festgelegtVorher)
        {
            Hinzu(l.Festgelegt is { } name ? $"Festgelegt: {name}" : "Festlegung aufgehoben");
            festgelegtVorher = l.Festgelegt;
        }
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
