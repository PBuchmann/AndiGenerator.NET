// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Plant eine Staffel mit Kompaktspieltagen (nicht im Original): sucht Pläne aus 3er- und 4er-Spieltagen (bei gerader
/// Mannschaftszahl mit je einer Einzelbegegnung), in denen jede Paarung genau einmal vorkommt, jede Mannschaft höchstens
/// einmal am Tag spielt und mindestens einen Spieltag ausrichtet, möglichst ohne Verstöße gegen die Meldungen. Daraus
/// werden Varianten gewählt: ausgewogen, kürzeste Fahrten (Luftlinie), gleichmäßiger Stand und gemeinsamer Spieltag.
/// </summary>
public static class Kompaktplaner
{
    /// <summary>Name der Variante mit Spitzen- und Abstiegsspieltag.</summary>
    public const string SpitzeUndKeller = "Spitzen- und Abstiegsspieltag";

    /// <summary>Anfang des Namens der Varianten mit 3er-Spieltagen, z. B. „Mit 3er-Spieltagen: kürzer“.</summary>
    public const string MitDreiern = "Mit 3er-Spieltagen: ";

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Plant.</summary>
    /// <param name="aufgabe">Die Aufgabe.</param>
    /// <param name="optionen">Dauer, Threads und Startwert.</param>
    /// <param name="fortschritt">Wird gelegentlich (aus einem Hintergrund-Thread) mit dem Stand aufgerufen (Text und Anteil bis 100 %).</param>
    /// <param name="abbruch">Bricht die Suche ab; die bis dahin besten Pläne werden zurückgegeben.</param>
    /// <returns>Varianten und Hinweise.</returns>
    public static Kompaktergebnis Planen(Kompaktaufgabe aufgabe, Kompaktplanoptionen optionen, Action<Kompaktfortschritt>? fortschritt, CancellationToken abbruch)
    {
        using var steuerung = new Kompaktsteuerung(abbruch);
        return Planen(aufgabe, optionen, steuerung, fortschritt);
    }

    /// <summary>Plant; die Steuerung kann anhalten, fortsetzen, beenden und den bisher besten Stand abfragen.</summary>
    /// <param name="aufgabe">Die Aufgabe.</param>
    /// <param name="optionen">Dauer, Threads und Startwert.</param>
    /// <param name="steuerung">Pause, Fortsetzen, Beenden und Zwischenstand.</param>
    /// <param name="fortschritt">Wird gelegentlich (aus einem Hintergrund-Thread) mit dem Stand aufgerufen (Text und Anteil bis 100 %).</param>
    /// <returns>Varianten und Hinweise.</returns>
    public static Kompaktergebnis Planen(Kompaktaufgabe aufgabe, Kompaktplanoptionen optionen, Kompaktsteuerung steuerung, Action<Kompaktfortschritt>? fortschritt)
    {
        ArgumentNullException.ThrowIfNull(aufgabe);
        ArgumentNullException.ThrowIfNull(optionen);
        ArgumentNullException.ThrowIfNull(steuerung);
        var modell = new Kompaktmodell(aufgabe);
        if (modell.N < 3 || modell.T == 0)
        {
            return new Kompaktergebnis([], 0, [.. modell.Hinweise, "Zu wenige Mannschaften oder keine Spieltage – es gibt nichts zu planen."]);
        }

        var lauf = new Kompaktlauf(modell, optionen, steuerung, fortschritt);
        steuerung.Zwischenstandquelle = () => Ergebnis(lauf, modell);
        try
        {
            lauf.Ausfuehren();
        }
        finally
        {
            steuerung.Zwischenstandquelle = null;
        }

        return Ergebnis(lauf, modell);
    }

    /// <summary>
    /// Fasst die Varianten mit dem besten Gesamtpaket (die erste) zusammen: Ein Ziel einer anderen Variante, das das
    /// Gesamtpaket mindestens ebenso gut trifft (bei gleich vielen oder weniger Verstößen und Mannschaften ohne
    /// Heimspieltag), entfällt dort und wird beim Gesamtpaket unter <see cref="Kompaktvariante.Zugleich"/> genannt; eine
    /// Variante ohne verbleibendes Ziel entfällt ganz.
    /// </summary>
    /// <param name="varianten">Die Varianten, das beste Gesamtpaket zuerst.</param>
    /// <param name="gesamtMitGruppen">Das Gesamtpaket hat Spitzen- und Abstiegsspieltag.</param>
    /// <returns>Die verbleibenden Varianten, das Gesamtpaket zuerst.</returns>
    internal static List<Kompaktvariante> Zusammenfassen(List<Kompaktvariante> varianten, bool gesamtMitGruppen)
    {
        if (varianten.Count == 0)
        {
            return varianten;
        }

        Kompaktvariante gesamt = varianten[0];
        var zugleich = new List<string>();
        var rest = new List<Kompaktvariante>();
        foreach (Kompaktvariante v in varianten.Skip(1))
        {
            string[] ziele = v.Name.Split(" · ");
            string[] erfuellt = ziele.Where(z => Erfuellt(gesamt.Kennzahlen, v.Kennzahlen, z, gesamtMitGruppen)).ToArray();
            zugleich.AddRange(erfuellt.Where(z => !zugleich.Contains(z)));
            if (erfuellt.Length < ziele.Length)
            {
                rest.Add(v with { Name = string.Join(" · ", ziele.Except(erfuellt)) });
            }
        }

        rest.Insert(0, gesamt with { Zugleich = zugleich });
        return rest;
    }

    /// <summary>
    /// Varianten mit 3er-Spieltagen, höchstens drei: nur Pläne, die ein Ziel besser treffen als jeder Plan nur aus
    /// 4er-Spieltagen – kürzere Fahrten, mehr Mannschaften am selben Sonntag, deutlich gleich weitere Fahrten (mindestens
    /// 10 % kleinerer Unterschied), gleichmäßigerer Stand oder Spitzen- bzw. Abstiegsspieltag zu dritt, wenn es mit
    /// 4er-Spieltagen keinen gibt.
    /// </summary>
    /// <param name="dreier">Pläne mit 3er-Spieltagen.</param>
    /// <param name="vierer">Pläne nur aus 4er-Spieltagen.</param>
    /// <param name="viererMitGruppen">Ein 4er-Plan hat schon Spitzen- und Abstiegsspieltag.</param>
    /// <returns>Die Varianten.</returns>
    internal static List<Kompaktvariante> Dreiervarianten(IReadOnlyList<Kompaktloesung> dreier, IReadOnlyList<Kompaktloesung> vierer, bool viererMitGruppen)
    {
        if (dreier.Count == 0 || vierer.Count == 0)
        {
            return [];
        }

        var wahl = new List<(string Ziel, Kompaktloesung Loesung)>();
        Kompaktloesung kurz = dreier.OrderBy(l => l.Km).ThenBy(l => l.Abstand).First();
        if (kurz.Km < vierer.Min(l => l.Km) - 0.5)
        {
            wahl.Add(("kürzer", kurz));
        }

        Kompaktloesung gemeinsam = dreier.OrderByDescending(l => l.Gemeinsam).ThenBy(l => l.Km).First();
        if (gemeinsam.Gemeinsam > vierer.Max(l => l.Gemeinsam))
        {
            wahl.Add((string.Create(Deutsch, $"{gemeinsam.Gemeinsam} am selben Sonntag"), gemeinsam));
        }

        Kompaktloesung gleich = dreier.OrderBy(l => l.KmSpanne).ThenBy(l => l.Km).First();
        if (gleich.KmSpanne < 0.9 * vierer.Min(l => l.KmSpanne))
        {
            wahl.Add(("gleich weite Fahrten", gleich));
        }

        Kompaktloesung stand = dreier.OrderBy(l => l.Abstand).ThenBy(l => l.Km).First();
        if (stand.Abstand < vierer.Min(l => l.Abstand))
        {
            wahl.Add(("gleichmäßiger Stand", stand));
        }

        Kompaktloesung? gruppen = dreier.Where(l => l.GruppenTage > 0).OrderByDescending(l => l.GruppenTage).ThenBy(l => l.GruppenFrueh).ThenBy(l => l.Km).FirstOrDefault();
        if (!viererMitGruppen && gruppen is not null)
        {
            wahl.Add(("Spitze/Abstieg zu dritt", gruppen));
        }

        return wahl
            .GroupBy(w => w.Loesung)
            .Take(3)
            .Select(g => new Kompaktvariante(MitDreiern + string.Join(" · ", g.Select(w => w.Ziel)), g.Key.Planturniere(), g.Key.Kennzahlen()))
            .ToList();
    }

    /// <summary>Die Varianten und Hinweise aus dem Stand eines Laufs (am Ende oder zwischendurch in der Pause).</summary>
    private static Kompaktergebnis Ergebnis(Kompaktlauf lauf, Kompaktmodell modell)
    {
        var hinweise = new List<string>(modell.Hinweise);

        // Einmal lesen: in der Pause kann ein Suchschritt den Wert gerade setzen.
        Kompaktloesung? gesamtpaket = lauf.Gesamtloesung;
        List<Kompaktloesung> front = lauf.Front.Alle();

        // In der Pause während der vollständigen 4er-Suche: deren bisher kürzester Plan steht noch nicht in der Front.
        if (front.Count == 0 && lauf.Baum?.Beste is IReadOnlyList<Kompaktkandidat> bisher)
        {
            front.Add(Kompaktverbesserung.EinzelAusgleichen(modell, new Kompaktloesung(modell, bisher)));
        }

        hinweise.AddRange(lauf.Optimierungshinweise());
        hinweise.AddRange(Viererhinweise(lauf.Baum));
        hinweise.AddRange(Ergebnishinweise(front, modell));
        List<Kompaktvariante> varianten = Kompaktauswahl.Varianten(front, gesamtpaket is null);
        if (gesamtpaket is Kompaktloesung gesamt)
        {
            varianten.Insert(0, new Kompaktvariante("Bestes Gesamtpaket", gesamt.Planturniere(), gesamt.Kennzahlen()));
            hinweise.Add(Gesamthinweis(gesamt, front, modell));
        }

        if (lauf.Gruppenloesung is Kompaktloesung gruppen)
        {
            varianten.Add(new Kompaktvariante(SpitzeUndKeller, gruppen.Planturniere(), gruppen.Kennzahlen()));
        }

        // Was das beste Gesamtpaket schon erfüllt, braucht keine eigene Variante; es steht auf seiner Karte (Peter, 13.10.2026).
        bool gesamtMitGruppen = gesamtpaket is { GruppenFehlen: 0 } && modell.Gruppen.Count > 0;
        if (gesamtpaket is not null)
        {
            varianten = Zusammenfassen(varianten, gesamtMitGruppen);
        }

        hinweise.AddRange(Gruppenhinweise(lauf, modell, front, gesamtMitGruppen));
        DreierAnhaengen(lauf, modell, front, varianten, hinweise);
        OhneVerstossAnhaengen(lauf, modell, varianten, hinweise);
        return new Kompaktergebnis(varianten, lauf.Geprueft, hinweise);
    }

    /// <summary>Hängt die Varianten mit 3er-Spieltagen an (Peter, 13.10.2026: nur, wenn sie etwas können, was kein Plan nur aus 4er-Spieltagen kann).</summary>
    private static void DreierAnhaengen(Kompaktlauf lauf, Kompaktmodell modell, List<Kompaktloesung> front, List<Kompaktvariante> varianten, List<string> hinweise)
    {
        if (front.Count > 0 && front.TrueForAll(l => l.Dreier == 0))
        {
            List<Kompaktloesung> vierer = [.. front, .. new[] { lauf.Gesamtloesung, lauf.Gruppenloesung }.OfType<Kompaktloesung>()];
            bool viererMitGruppen = modell.Gruppen.Count > 0 && vierer.Exists(l => l.GruppenFehlen == 0);
            List<Kompaktvariante> dreier = Dreiervarianten(lauf.Dreierfront.Alle(), vierer, viererMitGruppen);
            varianten.AddRange(dreier);
            if (dreier.Count == 0)
            {
                hinweise.Add("Mit 3er-Spieltagen ging nichts besser als mit Plänen nur aus 4er-Spieltagen – keine Fahrten kürzer, nicht mehr Mannschaften am selben Sonntag, nicht gleich weiter, kein gleichmäßigerer Stand.");
            }
        }
        else if (modell.Gruppen.Count > 0
            && lauf.Dreierfront.Alle().Where(l => l.GruppenTage > 0).OrderByDescending(l => l.GruppenTage).ThenBy(l => l.GruppenFrueh).ThenBy(l => l.Km).FirstOrDefault() is Kompaktloesung zuDritt)
        {
            // Ohne reine 4er-Pläne: Spitzen- und Abstiegsspieltag als 4er-Spieltag der Gruppe oder wenigstens zu dritt;
            // die Spieltage so verlegt, dass die Gruppenspieltage möglichst spät liegen.
            Kompaktloesung spaet = Kompaktverbesserung.TageVerteilen(modell, zuDritt, (a, b) => (a.GruppenFrueh, a.Abstand).CompareTo((b.GruppenFrueh, b.Abstand)));
            varianten.Add(new Kompaktvariante(SpitzeUndKeller, spaet.Planturniere(), spaet.Kennzahlen()));
        }
    }

    /// <summary>Hängt die Pläne der Nachsuche ganz ohne Verstöße an und sagt, was sie dafür in Kauf nehmen.</summary>
    private static void OhneVerstossAnhaengen(Kompaktlauf lauf, Kompaktmodell modell, List<Kompaktvariante> varianten, List<string> hinweise)
    {
        if (lauf.OhneVerstossHinweis is string hinweis)
        {
            hinweise.Add(hinweis);
        }

        foreach ((string name, Kompaktloesung l) in lauf.OhneVerstossPlaene())
        {
            varianten.Add(new Kompaktvariante(name, l.Planturniere(), l.Kennzahlen()));
            List<string> ohne = Enumerable.Range(0, modell.N)
                .Where(t => !Array.Exists(l.Turniere, k => !k.IstEinzel && k.Ausrichter == t))
                .Select(t => modell.Namen[t])
                .ToList();
            int extra = l.Einzel - (modell.N % 2 == 0 ? modell.N / 2 : 0);
            string text = ohne.Count == 0
                ? "jede Mannschaft richtet einen Spieltag aus"
                : "ohne Heimspieltag: " + string.Join(", ", ohne);
            if (extra > 0)
            {
                text += string.Create(Deutsch, $"; dafür {extra} Einzelspiel(e), sodass jede Mannschaft wenigstens ein Heimspiel hat");
            }

            hinweise.Add(string.Create(Deutsch, $"{name}: {l.Km:N0} km, {l.Vierer}× 4er, {l.Dreier}× 3er – {text}."));
        }
    }

    /// <summary>Beschreibt das beste Gesamtpaket: Mehr-Kilometer, Stand, gemeinsamer Spieltag, Spitzen- und Abstiegsspieltag.</summary>
    private static string Gesamthinweis(Kompaktloesung l, List<Kompaktloesung> front, Kompaktmodell modell)
    {
        double kmMin = front.Count == 0 ? l.Km : Math.Min(l.Km, front.Min(x => x.Km));
        double prozent = kmMin > 0 ? 100 * ((l.Km / kmMin) - 1) : 0;
        string gruppen = string.Empty;
        if (modell.Gruppen.Count > 0)
        {
            gruppen = l.GruppenFehlen == 0 ? ", mit Spitzen- und Abstiegsspieltag" : string.Create(Deutsch, $", {2 - l.GruppenFehlen} von 2 Gruppenspieltagen");
        }

        return string.Create(
            Deutsch,
            $"Bestes Gesamtpaket: {l.Km:N0} km (+{prozent:N1} %), zwischen den Mannschaften {l.KmSpanne:N0} km Unterschied, Stand höchstens {l.Abstand} Spiele auseinander, bis zu {l.Gemeinsam} von {modell.N} an einem Tag{gruppen}.");
    }

    /// <summary>Hinweise zu Spitzen- und Abstiegsspieltag: wann sie liegen und was sie an Kilometern kosten.</summary>
    private static List<string> Gruppenhinweise(Kompaktlauf lauf, Kompaktmodell modell, List<Kompaktloesung> front, bool gesamtMitGruppen)
    {
        if (modell.Gruppen.Count == 0)
        {
            return [];
        }

        if (lauf.Gruppenbaum is null && !lauf.Optimiert)
        {
            return [modell.Gruppen[0].Length == 3
                ? "Spitzen- und Abstiegsspieltag: Bei 6 oder 7 Mannschaften bilden die drei Stärksten bzw. Schwächsten je einen 3er-Spieltag; gesucht wird unter den gefundenen Plänen (Variante „Spitzen- und Abstiegsspieltag“). Bei 7 Mannschaften mit nur 3er-Spieltagen geht immer nur einer von beiden – zwei 3er-Spieltage haben dort stets eine Mannschaft gemeinsam."
                : "Spitzen- und Abstiegsspieltag werden vollständig nur bei 9 oder 10 Mannschaften gesucht; sonst unter den gefundenen Plänen, notfalls als 3er-Spieltag der drei Stärksten bzw. Schwächsten."];
        }

        Kompaktloesung? gezeigt = gesamtMitGruppen ? lauf.Gesamtloesung : lauf.Gruppenloesung;
        if (gezeigt is not Kompaktloesung loesung)
        {
            if (lauf.Gruppenbaum is null)
            {
                return ["Ein Plan mit Spitzen- und Abstiegsspieltag wurde nicht gefunden."];
            }

            return [lauf.Gruppenbaum.Vollstaendig
                ? "Mit diesen Meldungen gibt es keinen Plan nur aus 4er-Spieltagen mit Spitzen- und Abstiegsspieltag."
                : "Ein Plan mit Spitzen- und Abstiegsspieltag wurde in der verfügbaren Zeit nicht gefunden."];
        }

        string Tag(int[] gruppe)
        {
            Kompaktkandidat? k = Array.Find(loesung.Turniere, x => x.Form == Turnierform.Vierer && Kompaktoptimierung.IstGruppentag(x, gruppe))
                ?? Array.Find(loesung.Turniere, x => Kompaktoptimierung.IstGruppentag(x, gruppe));
            return k is null ? "?" : modell.Tage[k.Tag].ToString("dd.MM.", Deutsch);
        }

        double mehr = front.Count == 0 ? 0 : loesung.Km - front.Min(l => l.Km);
        string kosten = mehr > 0.5 ? string.Create(Deutsch, $"{mehr:N0} km mehr als der kürzeste Plan") : "ohne Mehr-Kilometer";
        string wo = gesamtMitGruppen ? "im besten Gesamtpaket, " : string.Empty;
        return [$"Spitzenspieltag am {Tag(modell.Gruppen[0])}, Abstiegsspieltag am {Tag(modell.Gruppen[1])} ({wo}{kosten})."];
    }

    /// <summary>Ob das Gesamtpaket das Ziel einer anderen Variante mindestens ebenso gut trifft.</summary>
    private static bool Erfuellt(Kompaktkennzahlen g, Kompaktkennzahlen k, string ziel, bool gesamtMitGruppen)
    {
        if (g.Verstoesse > k.Verstoesse || g.OhneHeimspieltag > k.OhneHeimspieltag)
        {
            return false;
        }

        return ziel switch
        {
            Kompaktauswahl.KuerzesteFahrten => g.Km <= k.Km + 0.5,
            Kompaktauswahl.GleichmaessigerStand => g.Abstand <= k.Abstand,
            Kompaktauswahl.GemeinsamerSpieltag => g.Gemeinsam >= k.Gemeinsam,
            Kompaktauswahl.GleichWeit => g.KmSpanne <= k.KmSpanne + 0.5,
            Kompaktauswahl.Ausgewogen => true,
            SpitzeUndKeller => gesamtMitGruppen,
            _ => false,
        };
    }

    private static List<string> Viererhinweise(Viererbaum? baum)
    {
        if (baum is null)
        {
            return [];
        }

        string knoten = baum.Knoten.ToString("N0", Deutsch);
        if (baum.Beste is not IReadOnlyList<Kompaktkandidat> beste)
        {
            return [baum.Vollstaendig
                ? $"Es gibt keinen Plan nur aus 4er-Spieltagen, der alle Meldungen einhält ({knoten} Teilpläne vollständig durchsucht)."
                : $"Die Suche nach Plänen nur aus 4er-Spieltagen wurde nach {knoten} Teilplänen abgebrochen, ohne einen zu finden."];
        }

        string km = beste.Sum(k => k.Km).ToString("N0", Deutsch);
        return [baum.Vollstaendig
            ? $"Alle Pläne nur aus 4er-Spieltagen vollständig durchsucht ({knoten} Teilpläne): Der kürzeste hat {km} km."
            : $"Pläne nur aus 4er-Spieltagen nicht vollständig durchsucht (Zeit abgelaufen, {knoten} Teilpläne): Der kürzeste gefundene hat {km} km."];
    }

    private static List<string> Ergebnishinweise(List<Kompaktloesung> front, Kompaktmodell modell)
    {
        if (front.Count == 0)
        {
            return ["Es wurde kein vollständiger Plan gefunden. Zu wenige mögliche Sonntage in den Meldungen."];
        }

        var liste = new List<string>();
        Kompaktloesung beste = front[0];
        if (beste.Verstoesse > 0)
        {
            liste.Add($"Ohne Verstöße gegen die Meldungen ließ sich kein Plan finden; die Varianten haben {beste.Verstoesse} Verstoß/Verstöße.");
        }

        if (beste.OhneHeimspieltag > 0)
        {
            string grund = beste.OhneHeimspieltag <= modell.MindestensOhneAusrichtung
                ? $"Bei {modell.N} Mannschaften gibt es dafür zu wenige Spieltage."
                : "Mit den Meldungen ließ sich das nicht anders lösen.";
            liste.Add($"{beste.OhneHeimspieltag} Mannschaft(en) richten keinen Spieltag aus. {grund}");
        }

        return liste;
    }
}
