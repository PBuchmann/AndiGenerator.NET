// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Rendering;

/// <summary>Das Diagramm „Spielverteilung“ als taggenaue Abstände (nicht im Original).</summary>
public sealed partial class Diagrammzeichner
{
    private const double Verteilungshoehe = 44;
    private const int KurzBis = 3;
    private const int PauseAb = 22;

    private static readonly Farbe Grundlinie = new(0x9A, 0xA6, 0xB2);
    private static readonly Farbe Pausenorange = new(0xD9, 0x77, 0x06);
    private static readonly Farbe Pausentext = new(0x9A, 0x34, 0x12);
    private static readonly Farbe Doppelgruen = new(0x1D, 0x6B, 0x3A);
    private static readonly Farbe Doppelflaeche = new(0xE3, 0xF1, 0xE8);

    /// <summary>Kompaktspieltage: Mitte der Kapsel je Mannschaft und Tag (für die Marken der Hinweise).</summary>
    private readonly Dictionary<(string Name, DateOnly Tag), (double X, double Y)> kapseln = [];

    /// <summary>Kompaktspieltage: die Hinweise, die noch die Marken ihres Spieltags bekommen.</summary>
    private readonly List<(int Hinweis, Ausrichtertag Tag)> markieren = [];

    /// <summary>Die Spiele einer Mannschaft mit Termin, nach Tagen gruppiert (mehrere an einem Tag = Doppelspieltag).</summary>
    /// <param name="m">Die Mannschaft.</param>
    /// <returns>Je Tag die Spiele, nach Zeit.</returns>
    public static IReadOnlyList<(DateOnly Tag, IReadOnlyList<Diagrammspiel> Spiele)> Spieltermine(Diagrammmannschaft m)
    {
        ArgumentNullException.ThrowIfNull(m);
        return m.Spiele
            .Where(s => s.Zeitpunkt is not null)
            .OrderBy(s => s.Zeitpunkt)
            .GroupBy(s => DateOnly.FromDateTime(s.Zeitpunkt!.Value))
            .Select(g => (g.Key, (IReadOnlyList<Diagrammspiel>)g.ToList()))
            .ToList();
    }

    private static DateOnly Montagvon(DateOnly tag) => tag.AddDays(-(((int)tag.DayOfWeek + 6) % 7));

    private static string Tagetext(int tage) => tage == 1 ? "1 Tag" : string.Create(CultureInfo.InvariantCulture, $"{tage} Tage");

    /// <summary>Der Hinweis zu einem Spiel: Tag und Zeit, Gegner, Halle (Kompaktspieltage) und Abstand zum vorigen Spiel.</summary>
    private static string Spielhinweis(string name, IReadOnlyList<(DateOnly Tag, IReadOnlyList<Diagrammspiel> Spiele)> tage, int k, int j, Ausrichtertag? turnier)
    {
        Diagrammspiel s = tage[k].Spiele[j];
        DateTime zeit = s.Zeitpunkt!.Value;
        string kopf = zeit.ToString("dddd, dd.MM.yyyy", Deutsch) + (zeit.TimeOfDay == TimeSpan.Zero ? string.Empty : zeit.ToString(", HH:mm 'Uhr'", Deutsch));
        string art = s.Heimspiel ? "Heimspiel" : "Auswärtsspiel";
        string verbindung = s.Heimspiel ? " gegen " : " bei ";
        string gegen = s.Gegner.Length == 0 ? art : art + verbindung + s.Gegner;
        var zeilen = new List<string> { kopf, gegen };
        if (turnier is not null)
        {
            // Bei Kompaktspieltagen zählt die Halle, nicht das Heimrecht laut Plan.
            zeilen[1] = s.Gegner.Length == 0 ? "Spiel" : "gegen " + s.Gegner;
            zeilen.Add(turnier.Ausrichter switch
            {
                null => "Halle offen",
                string n when n == name => "in der eigenen Halle (Ausrichter)",
                string n => "in der Halle von " + n,
            });
        }

        if (s.Koppel)
        {
            zeilen.Add("Koppeltermin");
        }

        if (j > 0)
        {
            zeilen.Add("Doppelspieltag: " + (j + 1).ToString(CultureInfo.InvariantCulture) + ". Spiel des Tages");
        }
        else if (k > 0)
        {
            zeilen.Add(Tagetext(tage[k].Tag.DayNumber - tage[k - 1].Tag.DayNumber) + " nach dem vorigen Spiel");
        }

        return string.Join(Environment.NewLine, zeilen);
    }

    /// <summary>
    /// Je Mannschaft eine Linie mit einem Punkt je Spiel am genauen Tag (gefüllt Heim, hohl Auswärts); zwei Spiele am selben
    /// Tag (Doppelspieltag) stehen übereinander in einer grünen Kapsel. Rot sind Spiele kurz nacheinander (1 bis 3 Tage),
    /// orange lange Pausen (über drei Wochen, nicht über den Wechsel der Runde). Rechts Doppelspieltage, kürzester Abstand und
    /// längste Pause. Jeder Punkt hat einen Hinweis mit den Angaben des Spiels.
    /// </summary>
    private Zeichengroesse Spielverteilung(double y)
    {
        Titel("Spielverteilung", y);
        y += 30;
        Datumszeile(y);
        List<IReadOnlyList<(DateOnly Tag, IReadOnlyList<Diagrammspiel> Spiele)>> alle = d.Mannschaften.Select(Spieltermine).ToList();
        DateTime letzter = alle.SelectMany(t => t).Select(t => t.Tag.ToDateTime(TimeOnly.MinValue)).Append(d.Letzter.Date).Max();
        DateTime ende = ErsterMontag();
        while (ende <= letzter)
        {
            ende = ende.AddDays(7);
        }

        double rechts = X(ende) + 20;
        Text("≤ 3 Tage", rechts, y, Normal, Beschriftung);
        Text("2 je Woche", rechts + 70, y, Normal, Beschriftung);
        Text("Pausen", rechts + 140, y, Normal, Beschriftung);
        double breite = rechts + 200;
        y += 26;
        double oben = y;
        double unten = oben + (d.Mannschaften.Count * Verteilungshoehe);
        for (int i = 1; i < d.Mannschaften.Count; i += 2)
        {
            Rechteck(Band, 0, oben + (i * Verteilungshoehe), breite, oben + ((i + 1) * Verteilungshoehe));
        }

        for (DateTime tag = ErsterMontag(); tag <= ende; tag = tag.AddDays(7))
        {
            Linie(LinieGrau, X(tag), oben, X(tag), unten);
        }

        for (int i = 0; i < d.Mannschaften.Count; i++)
        {
            Verteilungszeile(d.Mannschaften[i].Name, alle[i], oben + (i * Verteilungshoehe) + (Verteilungshoehe / 2), rechts);
        }

        // Kompaktspieltage: Beim Überfahren werden alle Spiele desselben Spieltags (dieselbe Halle) hervorgehoben.
        foreach ((int index, Ausrichtertag tag) in markieren)
        {
            List<Diagrammmarke> marken = tag.Spiele
                .SelectMany(s => new[] { s.Heim, s.Gast })
                .Distinct()
                .Where(n => kapseln.ContainsKey((n, tag.Tag)))
                .Select(n => new Diagrammmarke(kapseln[(n, tag.Tag)].X, kapseln[(n, tag.Tag)].Y, 13))
                .ToList();
            hinweise[index] = hinweise[index] with { Marken = marken };
        }

        y = unten + 16;
        double x = 0;
        f?.Kreis(x + 6, y + 8, 5, Heimspiel, Weiss, 1.5);
        bool kompakt = d.Kompakttage is { Count: > 0 };
        x = Legendentext(kompakt ? "in der eigenen Halle (Ausrichter)" : "Heimspiel", x + 16, y);
        f?.Kreis(x + 6, y + 8, 5, Weiss, Beschriftung, 1.5);
        x = Legendentext(kompakt ? "in der Halle des Ausrichters" : "Auswärtsspiel", x + 16, y);
        Doppel(x + 7, y + 8, [true, true]);
        x = Legendentext("Doppelspieltag (zwei Spiele am selben Tag)", x + 20, y);
        f?.Linie(x, y + 8, x + 30, y + 8, Schlecht, 4);
        x = Legendentext("höchstens 3 Tage Abstand", x + 36, y);
        f?.Linie(x, y + 8, x + 30, y + 8, Pausenorange, 4);
        x = Legendentext(d.Verstoesse is null ? "Pause über drei Wochen" : "zu lange Pause", x + 36, y);
        x = Legendentext("Rechts die Verstöße wie in der Qualitätsansicht", x, y);
        return new Zeichengroesse(Math.Max(breite, x), y + 26);
    }

    /// <summary>
    /// Ein Wert der rechten Spalten: mit Bewertung deren Zahl („–“, wenn sie das Kriterium nicht berücksichtigt), sonst die
    /// eigene Zählung.
    /// </summary>
    private string Spaltenwert(int? bewertet, int eigene) =>
        d.Verstoesse is null ? eigene.ToString(CultureInfo.InvariantCulture) : bewertet?.ToString(CultureInfo.InvariantCulture) ?? "–";

    private void Verteilungszeile(string name, IReadOnlyList<(DateOnly Tag, IReadOnlyList<Diagrammspiel> Spiele)> tage, double mitte, double rechts)
    {
        Name(name, mitte - 8);
        if (tage.Count == 0)
        {
            return;
        }

        Mannschaftsverstoesse? v = d.Verstoesse?.Von(name);
        bool engZaehlt = d.Verstoesse is null || v?.EngeTermine is not null;
        Linie(Grundlinie, Tagesx(tage[0].Tag), mitte, Tagesx(tage[^1].Tag), mitte, 1.5);
        int eng = 0;
        int woche = 0;
        double obenFrei = double.MinValue;
        double untenFrei = double.MinValue;

        // Zu lange Pausen: mit Bewertung genau ihre, sonst über drei Wochen innerhalb einer Runde.
        var pausen = new List<(DateOnly Von, DateOnly Bis)>();
        if (d.Verstoesse is not null)
        {
            pausen.AddRange((v?.Pausenzeiten ?? []).Select(p => (DateOnly.FromDateTime(p.Von), DateOnly.FromDateTime(p.Bis))));
        }

        for (int k = 1; k < tage.Count; k++)
        {
            int abstand = tage[k].Tag.DayNumber - tage[k - 1].Tag.DayNumber;
            bool gekoppelt = tage[k - 1].Spiele[^1].Koppel && tage[k].Spiele[0].Koppel;
            if (abstand <= KurzBis && !gekoppelt)
            {
                eng++;
            }

            if (Montagvon(tage[k].Tag) == Montagvon(tage[k - 1].Tag) && !gekoppelt)
            {
                woche++;
            }

            bool rundenwechsel = tage[k].Spiele[0].Runde != tage[k - 1].Spiele[^1].Runde;
            if (d.Verstoesse is null && abstand >= PauseAb && !rundenwechsel)
            {
                pausen.Add((tage[k - 1].Tag, tage[k].Tag));
            }

            if (abstand <= KurzBis && !gekoppelt && engZaehlt)
            {
                double x1 = Tagesx(tage[k - 1].Tag);
                double x2 = Tagesx(tage[k].Tag);
                f?.Linie(x1, mitte, x2, mitte, Schlecht, 4);
                Beschriften(string.Create(CultureInfo.InvariantCulture, $"{abstand} T."), (x1 + x2) / 2, mitte, Schlecht, ref obenFrei, ref untenFrei);
            }
        }

        foreach ((DateOnly von, DateOnly bis) in pausen)
        {
            double x1 = Math.Max(Tagesx(von), Tagesx(tage[0].Tag) - 30);
            double x2 = Math.Min(Tagesx(bis), Tagesx(tage[^1].Tag) + 30);
            f?.Linie(x1, mitte, x2, mitte, Pausenorange, 4);
            Beschriften(string.Create(CultureInfo.InvariantCulture, $"{bis.DayNumber - von.DayNumber} T."), (x1 + x2) / 2, mitte, Pausentext, ref obenFrei, ref untenFrei);
        }

        for (int k = 0; k < tage.Count; k++)
        {
            Termin(name, tage, k, mitte, engZaehlt);
        }

        // Rechts dieselben Zahlen wie in der Qualitätsansicht (Doppelspieltage zählen dort als Spiele kurz nacheinander).
        int eigenEng = eng + tage.Sum(t => Math.Max(0, t.Spiele.Count - 1));
        int eigeneWoche = woche + tage.Sum(t => Math.Max(0, t.Spiele.Count - 1));
        string[] werte = [Spaltenwert(v?.EngeTermine, eigenEng), Spaltenwert(v?.ZweiSpieleProWoche, eigeneWoche), Spaltenwert(v?.Pausen, pausen.Count)];
        for (int i = 0; i < werte.Length; i++)
        {
            bool rot = werte[i] is not ("0" or "–");
            f?.Text(werte[i], rechts + (i * 70), mitte - 8, Normal, rot ? Schlecht : Tinte, rot);
        }
    }

    /// <summary>
    /// Schreibt eine Abstandsangabe über die Linie; ist dort kein Platz mehr (die vorige ragt herein), darunter, sonst gar
    /// nicht – der Hinweis des Punkts nennt den Abstand ohnehin. So überschreiben sich keine Texte.
    /// </summary>
    private void Beschriften(string text, double x, double mitte, Farbe farbe, ref double obenFrei, ref double untenFrei)
    {
        double b = Schaetzen(text, Klein) + 4;
        double links = x - (b / 2);
        if (links >= obenFrei)
        {
            Text(text, links + 2, mitte - 22, Klein, farbe);
            obenFrei = links + b;
        }
        else if (links >= untenFrei)
        {
            Text(text, links + 2, mitte + 9, Klein, farbe);
            untenFrei = links + b;
        }
    }

    /// <summary>Ein Spieltag der Mannschaft: ein Punkt, bei mehreren Spielen die Kapsel; dazu je Spiel der Hinweis.</summary>
    /// <remarks>
    /// Bei Kompaktspieltagen zählt, wo gespielt wird: gefüllt in der eigenen Halle (Ausrichter), hohl in der Halle eines
    /// anderen – das Heimrecht laut Plan spielt keine Rolle (eine Gastmannschaft kann in fremder Halle Heimrecht haben).
    /// </remarks>
    private void Termin(string name, IReadOnlyList<(DateOnly Tag, IReadOnlyList<Diagrammspiel> Spiele)> tage, int k, double mitte, bool engZaehlt)
    {
        (DateOnly tag, IReadOnlyList<Diagrammspiel> spiele) = tage[k];
        double x = Tagesx(tag);
        Ausrichtertag? turnier = Kompakttag(name, tag);
        bool eigeneHalle = turnier?.Ausrichter == name;
        List<bool> gefuellt = spiele.Select(s => turnier?.Ausrichter is null ? s.Heimspiel : eigeneHalle).ToList();
        int erster = hinweise.Count;
        if (spiele.Count == 1)
        {
            f?.Kreis(x, mitte, 5, gefuellt[0] ? Heimspiel : Weiss, gefuellt[0] ? Weiss : Beschriftung, 1.5);
            hinweise.Add(new Diagrammhinweis(x, mitte, 6, Spielhinweis(name, tage, k, 0, turnier)));
        }
        else
        {
            // Zwei Spiele am selben Tag zählen als Spiele kurz nacheinander, außer an einem Koppeltermin: dann ist die Kapsel rot.
            bool zaehlt = engZaehlt && !spiele.All(s => s.Koppel);
            IReadOnlyList<double> ys = Doppel(x, mitte, gefuellt, zaehlt);
            for (int j = 0; j < spiele.Count; j++)
            {
                hinweise.Add(new Diagrammhinweis(x, ys[j], 5, Spielhinweis(name, tage, k, j, turnier)));
            }
        }

        if (turnier is not null)
        {
            kapseln[(name, tag)] = (x, mitte);
            for (int i = erster; i < hinweise.Count; i++)
            {
                markieren.Add((i, turnier));
            }
        }
    }

    /// <summary>Der Kompaktspieltag, an dem die Mannschaft an diesem Tag spielt; <c>null</c> außerhalb des Kompaktmodus.</summary>
    private Ausrichtertag? Kompakttag(string name, DateOnly tag) =>
        d.Kompakttage?.FirstOrDefault(t => t.Tag == tag && t.Spiele.Any(x => x.Heim == name || x.Gast == name));

    /// <summary>Mehrere Spiele an einem Tag übereinander in einer grünen Kapsel.</summary>
    /// <returns>Die Mitten der Punkte von oben nach unten.</returns>
    private IReadOnlyList<double> Doppel(double x, double mitte, IReadOnlyList<bool> heim, bool zaehlt = false)
    {
        const double Abstand = 9;
        double hoehe = (heim.Count * Abstand) + 3;
        double oben = mitte - (hoehe / 2);
        Rechteck(Doppelflaeche, x - 7, oben, x + 7, oben + hoehe);
        Rahmen(x - 7, oben, x + 7, oben + hoehe, zaehlt ? Schlecht : Doppelgruen, zaehlt ? 2 : 1.5);
        var ys = new List<double>();
        for (int j = 0; j < heim.Count; j++)
        {
            double yj = oben + 1.5 + (Abstand / 2) + (j * Abstand);
            f?.Kreis(x, yj, 3.6, heim[j] ? Heimspiel : Weiss, heim[j] ? null : Beschriftung, 1.2);
            ys.Add(yj);
        }

        return ys;
    }

    /// <summary>Mitte eines Tages auf der Zeitachse.</summary>
    private double Tagesx(DateOnly tag) => X(tag.ToDateTime(new TimeOnly(12, 0)));
}
