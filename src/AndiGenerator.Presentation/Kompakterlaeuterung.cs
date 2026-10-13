// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Presentation;

/// <summary>Erläuterung eines Plans in der Ansicht „Kompaktspieltage“: wofür die Variante steht und wie sie im Vergleich abschneidet.</summary>
internal static class Kompakterlaeuterung
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private static readonly (string Name, string Zweck)[] Zwecke =
    [
        ("Bestes Gesamtpaket", "Trifft alle Ziele zusammen am besten. Je ein Punkt Abzug: 10 % mehr Kilometer als der kürzeste Plan, ein Spieltag Rückstand im Stand, kein Sonntag mit zwei 4er-Spieltagen, ungleich weite Fahrten (je ein Viertel der mittleren Kilometer einer Mannschaft über dem kleinsten Unterschied); drei Punkte je fehlendem Spitzen- oder Abstiegsspieltag (liegt er früh in der Halbrunde, bis zu einem halben Punkt)."),
        ("Gesicherter Plan", "Der zuletzt gesicherte Plan, genau so, wie er gesichert wurde (Uhrzeiten, Hallen, Heimrecht). Gewählt gilt er für Terminplan, Druck und den Export für click-TT."),
        ("Gemerkt", "Ein gemerkter Plan, genau so, wie er gemerkt wurde (Uhrzeiten, Hallen, Heimrecht). Gewählt gilt er für Terminplan, Druck und den Export für click-TT."),
        ("Ausgewogen", "Kompromiss: 10 % mehr Kilometer wiegen so viel wie ein Spieltag Rückstand im Stand oder ein fehlender gemeinsamer Spieltag."),
        ("Kürzeste Fahrten", "Die wenigsten Kilometer (Luftlinie, hin und zurück; Einzelfahrten doppelt gezählt) – gibt es Pläne nur aus 4er-Spieltagen, unter diesen, bei 9 oder 10 Mannschaften nach vollständiger Suche."),
        ("Gleichmäßiger Stand", "Nach jedem Sonntag liegt die Zahl der gespielten Spiele aller Mannschaften so nah beieinander wie möglich."),
        ("Gemeinsamer Spieltag", "Möglichst viele Mannschaften spielen am selben Sonntag."),
        ("Gleich weite Fahrten", "Alle Mannschaften fahren möglichst gleich weit: kleinster Unterschied zwischen der Mannschaft mit den meisten und der mit den wenigsten Kilometern."),
        ("Spitzen- und Abstiegsspieltag", "Die vier stärksten und die vier schwächsten Mannschaften (QTTR-Schnitt) bilden je einen 4er-Spieltag – in Plänen aus 3er-Spieltagen (und bei 6 oder 7 Mannschaften) die drei stärksten und die drei schwächsten je einen 3er-Spieltag –, so spät wie möglich."),
        ("Ohne Verstöße", "Hält alle Meldungen ein (vollständig gesucht, weil die übrigen Pläne Verstöße haben); dafür bleiben, wenn es nicht anders geht, Mannschaften ohne Heimspieltag."),
        ("mit Einzelspielen", "Statt eine Mannschaft ganz ohne Heimspiel zu lassen, werden einzelne Paarungen als Einzelspiel angesetzt, sodass jede wenigstens ein Heimspiel hat."),
        ("Mit 3er-Spieltagen", "Braucht 3er-Spieltage, trifft aber das genannte Ziel besser als jeder Plan nur aus 4er-Spieltagen: kürzer, mehr Mannschaften am selben Sonntag, gleich weitere Fahrten, gleichmäßigerer Stand oder Spitzen- bzw. Abstiegsspieltag zu dritt."),
    ];

    /// <summary>Die Zeilen der Erläuterung.</summary>
    /// <param name="variante">Die gezeigte Variante oder <c>null</c> für den Plan aus click-TT.</param>
    /// <param name="a">Auswertung des gezeigten Plans.</param>
    /// <param name="alle">Alle Varianten der letzten Berechnung (zum Vergleich).</param>
    /// <param name="spitze">Die vier stärksten Mannschaften oder leer.</param>
    /// <param name="keller">Die vier schwächsten Mannschaften oder leer.</param>
    /// <returns>Die Zeilen.</returns>
    public static List<string> Zeilen(Kompaktvariante? variante, Kompaktauswertung a, IReadOnlyList<Kompaktvariante> alle, IReadOnlyList<string> spitze, IReadOnlyList<string> keller)
    {
        var zeilen = new List<string>();
        if (variante is null)
        {
            zeilen.Add("Der Plan, wie er in click-TT eingetragen ist – zum Vergleich mit den Varianten.");
        }
        else
        {
            zeilen.AddRange(Zwecke.Where(z => variante.Name.Contains(z.Name, StringComparison.Ordinal)).Select(z => z.Name + ": " + z.Zweck));
            if (variante.Zugleich.Count > 0)
            {
                zeilen.Add("Zugleich erfüllt: **" + string.Join(", ", variante.Zugleich) + "** – mindestens so gut wie die Variante dafür, die deshalb entfällt.");
            }
        }

        if (a.Spieltage.Count == 0)
        {
            return zeilen;
        }

        zeilen.Add(Fahrten(a, alle));
        zeilen.Add(Stand(a, alle));
        if (Gemeinsam(a) is string gemeinsam)
        {
            zeilen.Add(gemeinsam);
        }

        zeilen.Add(string.Create(Deutsch, $"Spieltage: **{a.Formen}**. **{Heimspieltage(a)}**."));
        if (a.Verstoesse > 0)
        {
            zeilen.Add(string.Create(Deutsch, $"Meldungen: **{a.Verstoesse} Verstöße** (rot umrandet im Raster)."));
        }

        if (spitze.Count >= 3 && spitze.Count == keller.Count)
        {
            zeilen.Add(Gruppe("Spitzenspieltag", a, spitze) + " " + Gruppe("Abstiegsspieltag", a, keller));
        }

        if (Punkte(a, alle, spitze, keller) is string punkte)
        {
            zeilen.Add(punkte);
        }

        return zeilen;
    }

    /// <summary>Wer wie oft ausrichtet, in Worten: „Jede Mannschaft ein Heimspieltag“ oder welche Mannschaften zwei (oder keinen) haben.</summary>
    /// <param name="a">Auswertung des Plans.</param>
    /// <returns>Der Text ohne Schlusspunkt.</returns>
    public static string Heimspieltage(Kompaktauswertung a)
    {
        // Ein Einzel-Heimspiel zählt als Heimspieltag mit (Peter, 13.10.2026); die Pflicht erfüllt nur ein 3er/4er-Spieltag.
        static int Heim(Kompaktmannschaft m) => m.Ausgerichtet + m.EinzelHeim;
        static string Name(Kompaktmannschaft m) => m.EinzelHeim > 0 ? string.Create(Deutsch, $"{m.Kurzname} ({m.EinzelHeim}× Einzel)") : m.Kurzname;
        int wenigste = a.Mannschaften.Min(Heim);
        int meiste = a.Mannschaften.Max(Heim);
        List<string> teile = [];
        if (wenigste == meiste)
        {
            teile.Add(wenigste == 1 ? "Jede Mannschaft ein Heimspieltag" : string.Create(Deutsch, $"Jede Mannschaft {wenigste} Heimspieltage"));
            List<Kompaktmannschaft> mitEinzel = a.Mannschaften.Where(m => m.EinzelHeim > 0).ToList();
            if (mitEinzel.Count > 0)
            {
                teile.Add("davon als Einzelspiel: " + string.Join(", ", mitEinzel.Select(Name)));
            }
        }
        else
        {
            foreach (IGrouping<int, Kompaktmannschaft> gruppe in a.Mannschaften.Where(m => Heim(m) != 1 || m.EinzelHeim > 0).GroupBy(Heim).OrderByDescending(g => g.Key))
            {
                string anzahl = gruppe.Key switch
                {
                    0 => "Kein Heimspieltag",
                    1 => "Ein Heimspieltag",
                    2 => "Zwei Heimspieltage",
                    3 => "Drei Heimspieltage",
                    _ => string.Create(Deutsch, $"{gruppe.Key} Heimspieltage"),
                };
                teile.Add($"{anzahl}: {string.Join(", ", gruppe.Select(Name))}");
            }

            if (a.Mannschaften.Any(m => Heim(m) == 1 && m.EinzelHeim == 0))
            {
                teile.Add("alle anderen einen");
            }
        }

        List<string> ohne = a.Mannschaften.Where(m => m.Ausgerichtet == 0 && Heim(m) > 0).Select(m => m.Kurzname).ToList();
        if (ohne.Count > 0)
        {
            teile.Add("keinen 3er- oder 4er-Spieltag richtet aus: " + string.Join(", ", ohne));
        }

        return string.Join("; ", teile);
    }

    private static string Fahrten(Kompaktauswertung a, IReadOnlyList<Kompaktvariante> alle)
    {
        Kompaktmannschaft? weiteste = a.Mannschaften.MaxBy(m => m.Km);
        string text = string.Create(Deutsch, $"Fahrten: **{a.KmGesamt:N0} km**") + Vergleich4er(a, alle);
        Kompaktmannschaft? wenigste = a.Mannschaften.MinBy(m => m.Km);
        return weiteste is null || wenigste is null
            ? text + "."
            : text + string.Create(Deutsch, $"; am weitesten fährt **{weiteste.Kurzname} ({weiteste.Km:N0} km)**, am wenigsten {wenigste.Kurzname} ({wenigste.Km:N0} km) – **{weiteste.Km - wenigste.Km:N0} km Unterschied**.");
    }

    /// <summary>
    /// Vergleich mit dem kürzesten Plan nur aus 4er-Spieltagen; dazu, ob es nur mit 3er-Spieltagen kürzer geht.
    /// </summary>
    private static string Vergleich4er(Kompaktauswertung a, IReadOnlyList<Kompaktvariante> alle)
    {
        List<double> vierer = alle.Where(v => v.Kennzahlen.Dreier == 0).Select(v => v.Kennzahlen.Km).ToList();
        if (vierer.Count == 0)
        {
            // Ohne Pläne nur aus 4er-Spieltagen (z. B. 6 bis 8 Mannschaften): Vergleich mit dem kürzesten überhaupt.
            double kuerzester = alle.Select(v => v.Kennzahlen.Km).DefaultIfEmpty(a.KmGesamt).Min();
            double mehrAlsKuerzester = a.KmGesamt - kuerzester;
            if (alle.Count == 0)
            {
                return string.Empty;
            }

            return mehrAlsKuerzester > 0.5
                ? string.Create(Deutsch, $", **{mehrAlsKuerzester:N0} km ({100 * mehrAlsKuerzester / kuerzester:N1} %) mehr** als der kürzeste Plan")
                : " – **der kürzeste Plan**";
        }

        double kuerzeste = vierer.Min();
        double mehr = a.KmGesamt - kuerzeste;
        bool mitDreiern = a.Spieltage.Any(s => s.Form == Turnierform.Dreier);
        if (mitDreiern && mehr < -0.5)
        {
            return string.Create(Deutsch, $", **{-mehr:N0} km weniger** als der kürzeste Plan nur aus 4er-Spieltagen – kürzer geht es nur mit 3er-Spieltagen");
        }

        string text = mehr > 0.5
            ? string.Create(Deutsch, $", **{mehr:N0} km ({100 * mehr / kuerzeste:N1} %) mehr** als der kürzeste Plan nur aus 4er-Spieltagen")
            : " – **der kürzeste Plan nur aus 4er-Spieltagen**";
        double dreier = alle.Where(v => v.Kennzahlen.Dreier > 0).Select(v => v.Kennzahlen.Km).DefaultIfEmpty(double.MaxValue).Min();
        return !mitDreiern && dreier < kuerzeste - 0.5
            ? text + string.Create(Deutsch, $"; nur mit 3er-Spieltagen ginge es kürzer (**{dreier:N0} km**)")
            : text;
    }

    /// <summary>Die Punkte wie beim besten Gesamtpaket mit Begründung; bezogen auf die Pläne nur aus 4er-Spieltagen.</summary>
    private static string? Punkte(Kompaktauswertung a, IReadOnlyList<Kompaktvariante> alle, IReadOnlyList<string> spitze, IReadOnlyList<string> keller)
    {
        if (Kompaktpunkte.Bezug(alle) is not Kompaktbezug bezug)
        {
            return null;
        }

        IReadOnlyList<IReadOnlyList<string>> gruppen = spitze.Count >= 3 && spitze.Count == keller.Count ? [spitze, keller] : [];
        (double summe, IReadOnlyList<string> gruende) = Kompaktpunkte.Berechnen(a, bezug, gruppen);
        return gruende.Count == 0
            ? "Punkte: **0** (kleiner ist besser) – trifft jedes Ziel so gut wie der jeweils beste Plan."
            : string.Create(Deutsch, $"Punkte: **{summe:N1}** (kleiner ist besser, wie beim besten Gesamtpaket gezählt) – {string.Join("; ", gruende)}.");
    }

    private static string Stand(Kompaktauswertung a, IReadOnlyList<Kompaktvariante> alle)
    {
        int bester = alle.Select(v => v.Kennzahlen.Abstand).DefaultIfEmpty(a.GroessterAbstand).Min();
        string vergleich = a.GroessterAbstand <= bester ? " – **so gleichmäßig wie die beste Variante**" : string.Create(Deutsch, $" (beste Variante: {bester})");
        return string.Create(Deutsch, $"Stand: nach jedem Spieltermin höchstens **{a.GroessterAbstand} Spiele** Unterschied zwischen zwei Mannschaften{vergleich}.");
    }

    /// <summary>Der Sonntag, an dem die meisten spielen – nur, wenn es mehr sind als an einem einzelnen Spieltag.</summary>
    private static string? Gemeinsam(Kompaktauswertung a)
    {
        int einzeln = a.Spieltage.Max(s => s.Mannschaften.Count);
        var tag = a.Spieltage.GroupBy(s => s.Tag)
            .Select(g => (Tag: g.Key, Anzahl: g.SelectMany(s => s.Mannschaften).Distinct(StringComparer.Ordinal).Count()))
            .OrderByDescending(x => x.Anzahl).ThenBy(x => x.Tag)
            .First();
        return tag.Anzahl <= einzeln
            ? null
            : string.Create(Deutsch, $"Gemeinsamer Spieltag: am {tag.Tag:dd.MM.} spielen **{tag.Anzahl} von {a.Mannschaften.Count} Mannschaften**.");
    }

    private static string Gruppe(string titel, Kompaktauswertung a, IReadOnlyList<string> gruppe)
    {
        Kompaktspieltag? s = a.Spieltage.FirstOrDefault(x => x.Mannschaften.Count == gruppe.Count && gruppe.All(x.Mannschaften.Contains));
        string namen = string.Join(", ", gruppe.Select(Kompaktplanung.Kurzname));
        if (s is not null)
        {
            return string.Create(Deutsch, $"**{titel} am {s.Tag:dd.MM.}** ({namen}).");
        }

        // Wenigstens zu dritt: die drei Stärksten bzw. Schwächsten an einem 3er-Spieltag.
        List<string> drei = gruppe.Take(3).ToList();
        Kompaktspieltag? dreier = a.Spieltage.FirstOrDefault(x => x.Form == Turnierform.Dreier && drei.All(x.Mannschaften.Contains));
        return dreier is null
            ? $"**Kein {titel}** ({namen})."
            : string.Create(Deutsch, $"**{titel} zu dritt am {dreier.Tag:dd.MM.}** ({string.Join(", ", drei.Select(Kompaktplanung.Kurzname))}).");
    }
}
