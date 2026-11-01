// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Einfach;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Application;

/// <summary>
/// Die Probleme eines Plans in einfachen Worten für den normalen Modus (Peter, 29.10.2026: „Mannschaft ABC und Mannschaft
/// DEF haben zu unregelmäßige Heimspielwechsel, Mannschaft GHI spielt nicht am Pflichtspieltag, 8 Sperrterminverletzungen
/// bei TSG JKL“): je Kriterium mit Verstößen ein Satz mit den am stärksten betroffenen Mannschaften, sortiert nach Muss (A),
/// Soll (B) und Feinschliff (C); in C die mit der größten Wirkung auf die Güte zuerst. Gezählt wird wie im Automodus
/// (<see cref="Stufeneinteilung.Anzahlen"/>, der Wechsel H/A steigend).
/// </summary>
public static class Planprobleme
{
    /// <summary>So viele Mannschaften nennt ein Satz höchstens (dann „und N weitere“).</summary>
    public const int MannschaftenJeSatz = 3;

    /// <summary>Kriterien mit kleinerem Faktor gelten als unwichtig und erscheinen nicht unter den Problemen.</summary>
    public const double NichtWichtigUnter = 0.5;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Die Probleme eines Plans, die wichtigsten zuerst.</summary>
    /// <param name="bewertung">Die Bewertung des Plans (mit <see cref="Planbewertung.Zusatz"/>).</param>
    /// <param name="massstab">Die Wichtigkeit der Kriterien, oder <c>null</c> (alle normal): Unwichtige (Faktor unter 0,5) erscheinen nicht.</param>
    /// <returns>Je Kriterium mit Verstößen ein Problem; Muss, dann Soll, dann Feinschliff.</returns>
    public static IReadOnlyList<Planproblem> Erstellen(Planbewertung bewertung, Massstab? massstab = null)
    {
        ArgumentNullException.ThrowIfNull(bewertung);
        (double[] anzahl, _) = Stufeneinteilung.Anzahlen(bewertung);
        int mannschaften = Math.Max(1, bewertung.Mannschaften.Count);
        var probleme = new List<Planproblem>();
        int harte = Planguete.Bewerten(bewertung).Harte;
        if (harte > 0)
        {
            probleme.Add(new Planproblem(Stufe.A, null, string.Empty, $"{Zahl(harte, "Spiel", "Spiele")} ohne gültigen Termin", harte));
        }

        foreach (Kostenkriterium k in Enum.GetValues<Kostenkriterium>())
        {
            int gesamt = (int)Math.Round(anzahl[(int)k]);
            if (gesamt <= 0 || (massstab is not null && massstab.Faktor(k) < NichtWichtigUnter))
            {
                continue;
            }

            (string wer, bool viele) = Wer(bewertung, k);
            probleme.Add(new Planproblem(StufeVon(k), k, wer, Was(k, gesamt, viele), gesamt));
        }

        // Muss und Soll nach Zahl, Feinschliff nach Wirkung auf die Güte.
        double Gewicht(Planproblem p) => p.Stufe == Stufe.C && p.Kriterium is Kostenkriterium k
            ? (massstab ?? Massstab.Standard).Kriteriumsgewicht(k, mannschaften) * p.Anzahl
            : p.Anzahl;
        return [.. probleme.OrderBy(p => p.Stufe).ThenBy(p => p.Kriterium is null ? 0 : 1).ThenByDescending(Gewicht)];
    }

    /// <summary>Die Stufe eines Kriteriums im normalen Modus: Pflicht A, Wünsche der Vereine B, sonst C.</summary>
    /// <param name="k">Das Kriterium.</param>
    /// <returns>Die Stufe.</returns>
    public static Stufe StufeVon(Kostenkriterium k)
    {
        if (Planguete.Pflicht.Contains(k))
        {
            return Stufe.A;
        }

        return Planguete.Wunschkriterien.Contains(k) ? Stufe.B : Stufe.C;
    }

    /// <summary>
    /// Die betroffenen Mannschaften, z. B. „TSG Linden:“ oder „TV Bergheim und SG Rotbach:“ – nur, wenn es wenige sind; sind es
    /// mehr als <see cref="MannschaftenJeSatz"/>, ist es ein allgemeines Problem der Staffel, und der Satz nennt keine Namen.
    /// </summary>
    private static (string Wer, bool Viele) Wer(Planbewertung bewertung, Kostenkriterium k)
    {
        List<(string Name, int Anzahl)> betroffen = [.. bewertung.Mannschaften
            .Select((m, t) => (m.Name, Anzahl: JeMannschaft(bewertung, m, t, k)))
            .Where(e => e.Anzahl > 0)
            .OrderByDescending(e => e.Anzahl)
            .ThenBy(e => e.Name, StringComparer.CurrentCulture)];
        if (betroffen.Count == 0)
        {
            return (string.Empty, false);
        }

        if (betroffen.Count > MannschaftenJeSatz)
        {
            return (string.Empty, true);
        }

        List<string> namen = [.. betroffen.Select(e => e.Name)];
        return ((namen.Count == 1 ? namen[0] : string.Join(", ", namen[..^1]) + " und " + namen[^1]) + ":", false);
    }

    /// <summary>Die Verstöße einer Mannschaft gegen ein Kriterium; 0 für Kriterien des ganzen Plans.</summary>
    private static int JeMannschaft(Planbewertung bewertung, Mannschaftsbewertung m, int t, Kostenkriterium k)
    {
        if (k >= Kostenkriterium.VereinsinterneSpieleAmAnfang)
        {
            return 0;
        }

        int anzahl = Math.Max(0, m.JeKostenart[(int)k].Anzahl);
        return k switch
        {
            Kostenkriterium.Spielverteilung => bewertung.Zusatz?.Spielverteilung is { } v && t < v.Count ? v[t] : anzahl,
            Kostenkriterium.WechselHeimAuswaerts => bewertung.WechselAnzahl(t),
            _ => anzahl,
        };
    }

    /// <summary>Was nicht passt, mit der Gesamtzahl – bei wenigen Mannschaften nach deren Namen, bei vielen als Satz über die Staffel.</summary>
    private static string Was(Kostenkriterium k, int n, bool viele) => viele ? WasAllgemein(k, n) : WasEinzeln(k, n);

    private static string WasEinzeln(Kostenkriterium k, int n) => k switch
    {
        Kostenkriterium.Hallenbelegung => $"{n}× zu viele Spiele gleichzeitig in der Halle",
        Kostenkriterium.ParalleleSpiele => $"{n}× Spiele zur selben Zeit wie bei einer Nachbarmannschaft",
        Kostenkriterium.Pflichtspieltage => $"{Zahl(n, "Pflichtspieltag", "Pflichtspieltage")} nicht eingehalten",
        Kostenkriterium.VereinsinterneSpieleAmAnfang => $"{Zahl(n, "vereinsinternes Spiel liegt", "vereinsinterne Spiele liegen")} nicht am Anfang der Runde",
        Kostenkriterium.Sperrtermine => $"{Zahl(n, "Spiel", "Spiele")} auf Sperrterminen",
        Kostenkriterium.Ausweichtermine => $"{Zahl(n, "Spiel", "Spiele")} auf Ausweichterminen",
        Kostenkriterium.Heimkoppel => $"{n}× Heimkoppel nicht erfüllt (zwei Heimspiele am selben Termin)",
        Kostenkriterium.Auswaertskoppel => $"{n}× Auswärtskoppel nicht erfüllt (gemeinsam auswärts)",
        Kostenkriterium.GleicheHeimtermine => $"{n}× nicht am gewünschten gemeinsamen Heimtermin",
        Kostenkriterium.SechzigKilometerRegel => $"{n}× gegen die 60-km-Regel",
        Kostenkriterium.UngleichHeimAuswaerts => "ungleich viele Heim- und Auswärtsspiele",
        Kostenkriterium.WechselHeimAuswaerts => "spielen zu oft mehrmals hintereinander zu Hause oder auswärts",
        Kostenkriterium.AbstandHeimAuswaerts => $"{n}× Hin- und Rückspiel liegen zu dicht beieinander",
        Kostenkriterium.DreiTageAbstand => $"{n}× zwei Spiele innerhalb von drei Tagen",
        Kostenkriterium.ZweiSpieleProWoche => $"{n}× zwei Spiele in einer Woche",
        Kostenkriterium.Spielverteilung => $"{Zahl(n, "zu lange Pause", "zu lange Pausen")} zwischen zwei Spielen",
        Kostenkriterium.Setzliste => $"{Zahl(n, "Spiel", "Spiele")} gegen ähnlich starke Gegner zu früh in der Runde (diese Duelle sollen am Ende stehen)",
        Kostenkriterium.Spieltaglaenge => $"{Zahl(n, "Spieltag zieht", "Spieltage ziehen")} sich zu lange hin",
        Kostenkriterium.Spieltagueberlappung => $"{Zahl(n, "Spieltag überschneidet", "Spieltage überschneiden")} sich mit dem nächsten",
        Kostenkriterium.LetzterSpieltagLaenge => "Der letzte Spieltag zieht sich zu lange hin",
        Kostenkriterium.LetzterSpieltagUeberlappung => "Der letzte Spieltag überschneidet sich mit dem vorletzten",
        _ => $"{n}× {Massstab.Kriteriumsname(k)}",
    };

    private static string WasAllgemein(Kostenkriterium k, int n) => k switch
    {
        Kostenkriterium.Hallenbelegung => $"Bei vielen Mannschaften ist die Halle doppelt belegt ({n}×)",
        Kostenkriterium.ParalleleSpiele => $"Viele Mannschaften spielen zur selben Zeit wie eine Nachbarmannschaft ({n}×)",
        Kostenkriterium.Pflichtspieltage => $"Viele Mannschaften verfehlen ihre Pflichtspieltage ({n}×)",
        Kostenkriterium.Sperrtermine => $"Viele Mannschaften haben Spiele auf Sperrterminen ({n} Spiele)",
        Kostenkriterium.Ausweichtermine => $"Viele Mannschaften haben Spiele auf Ausweichterminen ({n} Spiele)",
        Kostenkriterium.Heimkoppel => $"Bei vielen Mannschaften sind Heimkoppel nicht erfüllt ({n}×)",
        Kostenkriterium.Auswaertskoppel => $"Bei vielen Mannschaften sind Auswärtskoppel nicht erfüllt ({n}×)",
        Kostenkriterium.GleicheHeimtermine => $"Viele Mannschaften spielen nicht am gewünschten gemeinsamen Heimtermin ({n}×)",
        Kostenkriterium.SechzigKilometerRegel => $"Bei vielen Mannschaften verstößt ein Spiel gegen die 60-km-Regel ({n}×)",
        Kostenkriterium.UngleichHeimAuswaerts => "Viele Mannschaften haben ungleich viele Heim- und Auswärtsspiele",
        Kostenkriterium.WechselHeimAuswaerts => "Bei vielen Mannschaften wechseln Heim- und Auswärtsspiele unregelmäßig (mehrmals hintereinander zu Hause oder auswärts)",
        Kostenkriterium.AbstandHeimAuswaerts => $"Bei vielen Mannschaften liegen Hin- und Rückspiel zu dicht beieinander ({n}×)",
        Kostenkriterium.DreiTageAbstand => $"Viele Mannschaften spielen zweimal innerhalb von drei Tagen ({n}×)",
        Kostenkriterium.ZweiSpieleProWoche => $"Viele Mannschaften spielen zweimal in einer Woche ({n}×)",
        Kostenkriterium.Spielverteilung => $"Bei vielen Mannschaften sind die Pausen zwischen zwei Spielen zu lang ({n}×)",
        Kostenkriterium.Setzliste => $"Duelle ähnlich starker Mannschaften liegen zu früh in der Runde ({n} Spiele) – sie sollen am Ende stehen",
        _ => $"Bei vielen Mannschaften: {n}× {Massstab.Kriteriumsname(k)}",
    };

    private static string Zahl(int n, string einzahl, string mehrzahl) => string.Create(Deutsch, $"{n} {(n == 1 ? einzahl : mehrzahl)}");
}
