// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Kern;

/// <summary>
/// Anzahlen für Kriterien, die im Original nur Kosten haben (nicht im Original): Spielverteilung, Länge und Überlappung der
/// Spieltage. Sie erklären die Kosten und gehen in die C-Verstöße des Automodus ein; die Kostentabelle bleibt wie im Original.
/// </summary>
internal sealed partial class KernPlan
{
    /// <summary>
    /// Ab so vielen Spieltagen (im Mittel) zwischen zwei Spielen einer Mannschaft gilt die Lücke als Verstoß gegen die
    /// Spielverteilung – normal ist 1 (am Rand der Runde 1,5).
    /// </summary>
    public const double LueckeAb = 2.0;

    /// <summary>Ein Spieltag gilt als zu lang ab dieser Dauer (Tage, auf 10 Mannschaften bezogen wie im Original).</summary>
    private const double SpieltagTage = 6.0;

    /// <summary>Zählt die Verstöße der Kriterien ohne Anzahl im Original.</summary>
    /// <param name="meldungen">Auch die Meldungen dazu (für die Qualitätsansicht).</param>
    /// <returns>Die Anzahlen (und Meldungen).</returns>
    public Zusatzverstoesse ZusatzZaehlen(bool meldungen)
    {
        int[] verteilung = new int[D.N];
        var verteilungsmeldungen = new List<string>[D.N];
        for (int t = 0; t < D.N; t++)
        {
            verteilungsmeldungen[t] = [];
            foreach ((double start, double ende, double soll) in Luecken(t).Where(l => l.Soll >= LueckeAb))
            {
                verteilung[t]++;
                if (meldungen)
                {
                    verteilungsmeldungen[t].Add(string.Create(
                        CultureInfo.GetCultureInfo("de-DE"),
                        $"Pause von {DelphiDatum.Text(start)} bis {DelphiDatum.Text(ende)} – in der Zeit liegen im Mittel {soll:0.0} Spieltage"));
                }
            }
        }

        (int laenge, int ueberlappung, bool letzteLaenge, bool letzteUeberlappung, List<string> spieltage, List<string> letzter) = Spieltage(meldungen);
        return new Zusatzverstoesse(
            verteilung,
            laenge,
            ueberlappung,
            letzteLaenge ? 1 : 0,
            letzteUeberlappung ? 1 : 0,
            spieltage,
            verteilungsmeldungen.Select(m => (IReadOnlyList<string>)m).ToArray(),
            letzter);
    }

    /// <summary>
    /// Die Lücken zwischen den Spielen einer Mannschaft je Runde wie bei <c>Spielverteilung</c>: Start, Ende und wie viele
    /// Spieltage im Mittel darin liegen (am Rand der Runde +0,5, an Koppelterminen geteilt durch 1,5).
    /// </summary>
    private List<(double Start, double Ende, double Soll)> Luecken(int t)
    {
        var luecken = new List<(double Start, double Ende, double Soll)>();
        int[] liste = Spiele[t];
        int spiele = SpieleAnzahl[t];
        if (spiele == 0)
        {
            return luecken;
        }

        bool[] gekoppelt = new bool[spiele + 1];
        for (int i = 0; i <= spiele; i++)
        {
            gekoppelt[i] = GekoppeltDanach(t, i) >= 0;
        }

        for (int runde = 0; runde < D.Runden.Length; runde++)
        {
            if (!D.RundeZuPlanen[runde])
            {
                continue;
            }

            for (int i = -1; i < spiele; i++)
            {
                bool erstes = false;
                bool letztes = false;
                double start;
                double ende;
                if (i < 0 || runde > Runde[liste[i]])
                {
                    if (i + 1 >= spiele || runde != Runde[liste[i + 1]])
                    {
                        continue;
                    }

                    erstes = true;
                    start = D.ErstesEchtesDatum[runde];
                    ende = Datum[liste[i + 1]];
                }
                else if (i >= spiele - 1 || runde < Runde[liste[i + 1]])
                {
                    if (runde != Runde[liste[i]])
                    {
                        continue;
                    }

                    letztes = true;
                    ende = D.LetztesEchtesDatum[runde] + 1;
                    start = Datum[liste[i]];
                }
                else
                {
                    start = Datum[liste[i]];
                    ende = Datum[liste[i + 1]];
                }

                if (ende <= 0 || start <= 0)
                {
                    continue;
                }

                double soll = D.SollSpiele(erstes ? runde : Runde[liste[i]], start, ende);
                if (!letztes && gekoppelt[i + 1])
                {
                    soll /= 1.5;
                }

                if (!erstes && i > 0 && gekoppelt[i - 1])
                {
                    soll /= 1.5;
                }

                if (erstes || letztes)
                {
                    soll += 0.5;
                }

                luecken.Add((start, ende, soll));
            }
        }

        return luecken;
    }

    /// <summary>
    /// Wie <c>SpieltagsKosten</c>: Der i-te Spieltag sind die i-ten Spiele aller Mannschaften. Zu lang ist er, wenn er (auf 10
    /// Mannschaften bezogen) mehr als 6 Tage umfasst; er überlappt, wenn er beginnt, bevor ein früherer zu Ende ist.
    /// </summary>
    private (int Laenge, int Ueberlappung, bool LetzteLaenge, bool LetzteUeberlappung, List<string> Meldungen, List<string> Letzter) Spieltage(bool meldungen)
    {
        int spieltage = 0;
        for (int t = 0; t < D.N; t++)
        {
            spieltage = Math.Max(spieltage, SpieleAnzahl[t]);
        }

        var texte = new List<string>();
        var letzterTexte = new List<string>();
        var maxima = new List<(int Spieltag, double Max)>();
        int laenge = 0;
        int ueberlappung = 0;
        bool letzteLaenge = false;
        bool letzteUeberlappung = false;
        for (int i = 0; i < spieltage; i++)
        {
            double max = 0.0;
            double min = 0.0;
            for (int t = 0; t < D.N; t++)
            {
                int s = SpieleAnzahl[t] > i ? Spiele[t][i] : -1;
                double datum = s >= 0 ? Datum[s] : 0.0;
                if (datum != 0.0 && InPlanung[s])
                {
                    max = Math.Max(max, datum);
                    min = min == 0.0 ? datum : Math.Min(min, datum);
                }
            }

            if (max <= 0 || min <= 0)
            {
                continue;
            }

            bool letzter = i == spieltage - 1;
            int dauer = DelphiDatum.Trunc(max) - DelphiDatum.Trunc(min);
            if (dauer * D.N / 10.0 > SpieltagTage)
            {
                laenge++;
                letzteLaenge |= letzter;
                if (meldungen)
                {
                    texte.Add($"{i + 1}. Spieltag dauert {dauer} Tage: {DelphiDatum.Text(min)} bis {DelphiDatum.Text(max)}");
                    if (letzter)
                    {
                        letzterTexte.Add(texte[^1]);
                    }
                }
            }

            (int Spieltag, double Max)? frueher = maxima.Cast<(int Spieltag, double Max)?>()
                .FirstOrDefault(m => DelphiDatum.Trunc(min) <= DelphiDatum.Trunc(m!.Value.Max));
            if (frueher is { } f)
            {
                ueberlappung++;
                letzteUeberlappung |= letzter;
                if (meldungen)
                {
                    texte.Add($"{i + 1}. Spieltag beginnt {DelphiDatum.Text(min)}, der {f.Spieltag + 1}. endet erst {DelphiDatum.Text(f.Max)}");
                    if (letzter)
                    {
                        letzterTexte.Add(texte[^1]);
                    }
                }
            }

            maxima.Add((i, max));
        }

        return (laenge, ueberlappung, letzteLaenge, letzteUeberlappung, texte, letzterTexte);
    }
}
