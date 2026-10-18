// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Kern;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Schranke;

/// <summary>
/// Zu lange und überlappende Spieltage (nur für die Teilneuplanung): wie <c>KernPlan.ZusatzZaehlen</c> genau, solange alle
/// Spiele einen Termin haben.
/// </summary>
public sealed partial class Ligaschranke
{
    /// <summary>Die Ziele der Spieltage, sobald sie einmal angelegt sind (je Kriterium).</summary>
    private Spieltagsziele? spieltagsziele;

    /// <summary>Ob die Spieltage schon angelegt wurden (oder sich nicht anlegen ließen).</summary>
    private bool spieltageAngelegt;

    /// <summary>Die Ziele der Spieltage; beim ersten Aufruf angelegt (<c>null</c>, wenn das Modell sie nicht abbilden kann).</summary>
    private Spieltagsziele? Spieltage()
    {
        if (!spieltageAngelegt)
        {
            spieltageAngelegt = true;
            spieltagsziele = SpieltageAnlegen();
        }

        return spieltagsziele;
    }

    /// <summary>
    /// Wie <c>KernPlan.Spieltage</c>: Der i-te Spieltag sind die i-ten Spiele aller Mannschaften. Zu lang ist er, wenn zwischen
    /// seinem ersten und letzten Tag mindestens K Tage liegen (K aus 6 Tagen, auf 10 Mannschaften bezogen); er überlappt, wenn er
    /// an oder vor dem letzten Tag des vorigen beginnt.
    /// <para>
    /// Im Modell zählt je Mannschaft t eine Zahl C(t, a) ihre Spiele bis zum Tag a. Der i-te Spieltag ist zu lang, wenn eine
    /// Mannschaft ihr i-tes Spiel an einem Tag a hat (C(t, a) ≥ i) und eine andere bis a + K − 1 noch nicht
    /// (C(u, a + K − 1) ≤ i − 1); er überlappt, wenn eine Mannschaft ihr i-tes Spiel an a hat und eine andere ihr (i−1)-tes erst
    /// an a oder später (C(u, a − 1) ≤ i − 2). Dafür je Tag das Höchste M(a) über die Mannschaften, die an a spielen können, und
    /// das Kleinste m(x) über alle, dazu Ja/Nein-Spalten „M(a) ≥ i“ und „m(x) ≥ i“ (nur, wo es offen ist; sonst fest) und je
    /// Spieltag eine Spalte „zu lang“ bzw. „überlappt“.
    /// </para>
    /// Nur mit den <see cref="Spielzaehler"/>.
    /// </summary>
    private Spieltagsziele? SpieltageAnlegen()
    {
        if (Zaehler() is not { } z)
        {
            return null;
        }

        int g = z.Spiele;
        int f0 = z.Fest;

        // Ab so vielen Tagen zwischen erstem und letztem Tag ist ein Spieltag zu lang (wie im Original: Dauer · N / 10 > 6).
        int k = 0;
        while (k * d.N / 10.0 <= KernPlan.SpieltagTage)
        {
            k++;
        }

        var zaehler = new Spieltagszaehler(this, z);
        int[] tage = termine.Select(t => d.WunschTag[t.Wunsch]).Distinct().Order().ToArray();

        // Je Spieltag (1-basiert) „zu lang“ und „überlappt“; der erste geplante Spieltag kann nicht überlappen (davor liegt kein
        // geplanter).
        var laenge = new Dictionary<int, int>();
        var ueberlappung = new Dictionary<int, int>();
        for (int i = f0 + 1; i <= g; i++)
        {
            laenge[i] = modell.Spalte(0);
            if (i >= f0 + 2)
            {
                ueberlappung[i] = modell.Spalte(0);
            }
        }

        foreach (int a in tage)
        {
            int xLaenge = Spieltagszaehler.Stichtag(tage, a + k - 1);
            int xVorher = Spieltagszaehler.Stichtag(tage, a - 1);
            for (int i = f0 + 1; i <= g; i++)
            {
                Spieltagswert hoch = zaehler.Hoechstens(a, i);
                if (hoch.Fest == 0)
                {
                    continue;
                }

                Verletzung(laenge[i], hoch, zaehler.Mindestens(xLaenge, i));
                if (ueberlappung.TryGetValue(i, out int u))
                {
                    Verletzung(u, hoch, zaehler.Mindestens(xVorher, i - 1));
                }
            }
        }

        return new Spieltagsziele(
            laenge.Values.Select(c => (c, 1.0)).ToList(),
            ueberlappung.Values.Select(c => (c, 1.0)).ToList(),
            [(laenge[g], 1.0)],
            ueberlappung.TryGetValue(g, out int letzte) ? [(letzte, 1.0)] : []);
    }

    /// <summary>
    /// v ≥ „eine hat ihr Spiel schon“ − „alle haben es schon“ (jeweils fest 0/1 oder eine Ja/Nein-Spalte). Sind beide fest und
    /// ist es verletzt, ist v fest 1.
    /// </summary>
    private void Verletzung(int v, Spieltagswert hoch, Spieltagswert tief)
    {
        if (hoch.Fest == 0 || tief.Fest == 1)
        {
            return;
        }

        var eintraege = new List<(int Spalte, double Faktor)> { (v, 1.0) };
        double unten = 0;
        if (hoch.Spalte >= 0)
        {
            eintraege.Add((hoch.Spalte, -1.0));
        }
        else
        {
            unten += 1;
        }

        if (tief.Spalte >= 0)
        {
            eintraege.Add((tief.Spalte, 1.0));
        }

        modell.Zeile(eintraege, unten, double.PositiveInfinity);
    }

    /// <summary>„Ein Zähler ≥ i“: fest (0 oder 1, dann <see cref="Spalte"/> = −1) oder offen (eine Ja/Nein-Spalte, Fest = −1).</summary>
    /// <param name="Fest">0 oder 1, wenn es feststeht; sonst −1.</param>
    /// <param name="Spalte">Die Ja/Nein-Spalte, wenn es offen ist; sonst −1.</param>
    private readonly record struct Spieltagswert(int Fest, int Spalte);

    /// <summary>Die Ziele der Spieltage je Kriterium (Spalten mit Faktor).</summary>
    /// <param name="Laenge">Zu lange Spieltage.</param>
    /// <param name="Ueberlappung">Überlappende Spieltage.</param>
    /// <param name="LetzteLaenge">Der letzte Spieltag ist zu lang.</param>
    /// <param name="LetzteUeberlappung">Der letzte Spieltag überlappt.</param>
    private sealed record Spieltagsziele(
        List<(int Spalte, double Faktor)> Laenge,
        List<(int Spalte, double Faktor)> Ueberlappung,
        List<(int Spalte, double Faktor)> LetzteLaenge,
        List<(int Spalte, double Faktor)> LetzteUeberlappung);

    /// <summary>
    /// Die Zähler der Spieltage: je Tag M(a) und m(x) mit ihren Ja/Nein-Spalten (über den <see cref="Spielzaehler"/>) – erst bei
    /// Bedarf angelegt.
    /// </summary>
    private sealed class Spieltagszaehler(Ligaschranke s, Spielzaehler z)
    {
        /// <summary>Steht für einen Stichtag vor allen geplanten Tagen.</summary>
        private const int VorAllem = int.MinValue;

        private readonly Dictionary<int, (int Spalte, int Unten, int Oben)> hoechste = [];
        private readonly Dictionary<int, (int Spalte, int Unten, int Oben)> kleinste = [];
        private readonly Dictionary<(int Tag, int I), int> hoechstensSpalten = [];
        private readonly Dictionary<(int Tag, int I), int> mindestensSpalten = [];

        /// <summary>Der Tag, ab dem der Stand von <paramref name="x"/> gilt: der letzte geplante Tag bis dahin.</summary>
        public static int Stichtag(int[] tage, int x)
        {
            int i = Array.BinarySearch(tage, x);
            if (i >= 0)
            {
                return tage[i];
            }

            i = ~i - 1;
            return i >= 0 ? tage[i] : VorAllem;
        }

        /// <summary>„Eine Mannschaft, die am Tag a spielen kann, hat bis a mindestens i Spiele“ (M(a) ≥ i).</summary>
        public Spieltagswert Hoechstens(int a, int i)
        {
            int g = z.Spiele;
            if (!hoechste.TryGetValue(a, out (int Spalte, int Unten, int Oben) m))
            {
                // M(a) ≥ C(t, a) für jede Mannschaft, die an a spielen kann; das Ziel drückt M auf das Höchste.
                m = (s.modell.Zahl(0, g), 0, 0);
                for (int t = 0; t < s.d.N; t++)
                {
                    int c = z.Am(t, a);
                    if (c >= 0)
                    {
                        s.modell.Zeile([(m.Spalte, 1.0), (c, -1.0)], 0, double.PositiveInfinity);
                        m = (m.Spalte, Math.Max(m.Unten, z.Unten(t, a)), Math.Max(m.Oben, z.Oben(t, a)));
                    }
                }

                hoechste[a] = m;
            }

            if (i <= m.Unten)
            {
                return new Spieltagswert(1, -1);
            }

            if (i > m.Oben)
            {
                return new Spieltagswert(0, -1);
            }

            if (!hoechstensSpalten.TryGetValue((a, i), out int spalte))
            {
                // M(a) ≥ i erzwingt die Spalte: M(a) ≤ i − 1 + (g − i + 1) · Spalte.
                spalte = s.modell.Spalte(0);
                s.modell.Zeile([(m.Spalte, 1.0), (spalte, -(g - i + 1))], double.NegativeInfinity, i - 1);
                hoechstensSpalten[(a, i)] = spalte;
            }

            return new Spieltagswert(-1, spalte);
        }

        /// <summary>„Alle Mannschaften haben bis zum Stichtag x mindestens i Spiele“ (m(x) ≥ i).</summary>
        public Spieltagswert Mindestens(int x, int i)
        {
            if (x == VorAllem)
            {
                return new Spieltagswert(i <= z.Fest ? 1 : 0, -1);
            }

            if (!kleinste.TryGetValue(x, out (int Spalte, int Unten, int Oben) m))
            {
                // m(x) ≤ C(u, x) für jede Mannschaft; das Ziel hebt m auf das Kleinste.
                m = (s.modell.Zahl(0, z.Spiele), int.MaxValue, int.MaxValue);
                for (int t = 0; t < s.d.N; t++)
                {
                    int c = z.Bis(t, x);
                    if (c >= 0)
                    {
                        s.modell.Zeile([(m.Spalte, 1.0), (c, -1.0)], double.NegativeInfinity, 0);
                    }
                    else
                    {
                        s.modell.Zeile([(m.Spalte, 1.0)], double.NegativeInfinity, z.Fest);
                    }

                    m = (m.Spalte, Math.Min(m.Unten, z.Unten(t, x)), Math.Min(m.Oben, z.Oben(t, x)));
                }

                kleinste[x] = m;
            }

            if (i <= m.Unten)
            {
                return new Spieltagswert(1, -1);
            }

            if (i > m.Oben)
            {
                return new Spieltagswert(0, -1);
            }

            if (!mindestensSpalten.TryGetValue((x, i), out int spalte))
            {
                // Die Spalte nur, wenn m(x) ≥ i: m(x) ≥ i · Spalte.
                spalte = s.modell.Spalte(0);
                s.modell.Zeile([(m.Spalte, 1.0), (spalte, -i)], 0, double.PositiveInfinity);
                mindestensSpalten[(x, i)] = spalte;
            }

            return new Spieltagswert(-1, spalte);
        }
    }
}
