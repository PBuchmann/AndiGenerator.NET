// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Schranke;
using Dock.Model.Mvvm.Controls;

namespace AndiGenerator.Presentation;

/// <summary>
/// Leitstand der Optimierung: was die Rechenkerne gerade schaffen, wie die Inseln suchen (beste Kosten,
/// Weg bis zum Neustart, Suchstrategien; im Automodus statt der Kosten die Verstöße A, B und C), was die Spezial-Insel
/// tut und – im Automodus – welche Gewichte er gerade gegenüber den eigenen angehoben hat. Hängt nicht vom Plan ab,
/// sondern nur von der laufenden Generierung.
/// </summary>
public sealed class LeitstandAnsichtViewModel : Document
{
    /// <summary>Breite eines Kostenbalkens bei 100 % (Pixel).</summary>
    private const double Balkenbreite = 90;

    /// <summary>Höchste Gewichtsstufe der Stufenleiste („extrem hoch“; „nicht berücksichtigen“ = keine Stufe).</summary>
    private const int Stufen = (int)Gewichtung.ExtremHoch;

    /// <summary>So viele Schritte des Automodus zeigt das Protokoll.</summary>
    private const int ProtokollZeilen = 6;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private readonly HauptfensterViewModel hauptfenster;
    private readonly ObservableCollection<Inselanzeige> inseln = [];
    private string ueberschrift = "Keine Generierung aktiv";
    private bool automodus;
    private IReadOnlyList<Gewichtsgruppe> gewichte = [];
    private string gewichteSchluessel = string.Empty;
    private string verfahren = string.Empty;
    private IReadOnlyList<string> protokoll = [];
    private bool zeigtTeilneuplanung;

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="hauptfenster">Hauptfenster mit der Anzeige der Rechenkerne.</param>
    public LeitstandAnsichtViewModel(HauptfensterViewModel hauptfenster)
    {
        ArgumentNullException.ThrowIfNull(hauptfenster);
        this.hauptfenster = hauptfenster;
        Id = "Leitstand";
        Title = "Leitstand";
        CanClose = true;
        CanFloat = true;
        Anzeige = hauptfenster.Anzeige;
    }

    /// <summary>Holt die Erklärung der Teilneuplanung mit HiGHS.</summary>
    public static string TeilneuplanungErklaerung =>
        "So arbeiten Inseln und HiGHS zusammen – es gibt immer nur einen besten Plan: Jede Verbesserung der Inseln und jeder "
        + "bessere Plan aus HiGHS wird sofort bester Plan, wenn er nach den Regeln des Automodus besser ist. Die untere Schranke "
        + "rechnet in Schritten (A, dann B, dann C); nach jedem Schritt wird ihr Plan sofort angeboten. Nach der Basisoptimierung "
        + "plant die Teilneuplanung (im Schaubild rechts) fortlaufend die Spiele einiger Mannschaften des besten Plans neu "
        + "(der Ausschnitt, zuerst zwei Mannschaften; bringt eine Größe eine Weile nichts, eine mehr): A und B dürfen nicht "
        + "schlechter werden, C soll besser werden (Ungleich und Wechsel H/A, 3-Tage-Abstand, 2 Spiele pro Woche, gleiche "
        + "Heimtermine, 60-km-Regel, genähert auch Abstand H/A, Setzliste und Pausen der Spielverteilung). Ein besserer Ausschnitt "
        + "geht sofort an den Automodus und wird bester Plan (ein orangefarbener Punkt wandert dorthin), die schlechteste Insel rechnet "
        + "von ihm aus weiter. Haben die Inseln inzwischen einen besseren Plan, plant HiGHS ab dem nächsten Ausschnitt von diesem "
        + "aus weiter. Je Ausschnitt ein Kästchen: grün besser, rot im Modell besser, nach der echten Bewertung aber nicht, grau "
        + "nichts Besseres, blau Plan der Inseln übernommen; umrandet der, den HiGHS gerade plant. Die Balken je Größe zeigen, wie oft "
        + "Ausschnitte mit 2, 3, … Mannschaften besser waren. HiGHS hat keinen eigenen Kern: "
        + "Es rechnet auf einem eigenen Faden und teilt sich den Prozessor mit der Suche – wie viel, zeigt die Anzeige "
        + "Prozessor links (orange).";

    /// <summary>Holt die Erklärung des Inselmodells.</summary>
    public static string Erklaerung =>
        "Jeder Rechenkern rechnet abwechselnd an mehreren Suchplätzen. Die Suchplätze gehören zu Inseln: Jede Insel sucht für sich "
        + "und hat ihren eigenen besten Plan; die Balken zeigen, welche Kriterien in ihm die meisten Kosten verursachen. Im Schaubild "
        + "steht je Insel grob, was sie gerade tut; ein Punkt wandert nur, wenn der beste Plan besser wird (grün von einer Insel, orange von HiGHS). Findet die "
        + "schlechteste Insel lange nichts Besseres, beginnt sie neu – mit einem leeren Plan oder mit dem Plan der Spezial-Insel, "
        + "die eine Kostenart extrem gewichtet.";

    /// <summary>Holt die Anzeige mit den Rechenkernen.</summary>
    public Generierungsanzeige Anzeige { get; }

    /// <summary>Holt das Schaubild der Maschine mit seinen Ereignissen.</summary>
    public Schaubild Schaubild { get; } = new();

    /// <summary>Holt die Inseln (zuletzt die Spezial-Insel).</summary>
    public IReadOnlyList<Inselanzeige> Inseln => inseln;

    /// <summary>Holt die Überschrift: Kerne, Inseln und Tempo.</summary>
    public string Ueberschrift
    {
        get => ueberschrift;
        private set => SetProperty(ref ueberschrift, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob der Automodus lenkt (dann gibt es den Abschnitt der Gewichte).</summary>
    public bool Automodus
    {
        get => automodus;
        private set => SetProperty(ref automodus, value);
    }

    /// <summary>Holt die Gewichte, die der Automodus gerade angehoben hat, je Kriterium in der Reihenfolge der Einteilung.</summary>
    public IReadOnlyList<Gewichtsgruppe> Gewichte
    {
        get => gewichte;
        private set
        {
            if (SetProperty(ref gewichte, value))
            {
                OnPropertyChanged(nameof(KeineGewichte));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob der Automodus noch kein Gewicht geändert hat.</summary>
    public bool KeineGewichte => Gewichte.Count == 0;

    /// <summary>
    /// Holt, was der Automodus aus HiGHS weiß und wie er danach mit den Gewichten arbeitet (Schranke, Schutz von A und B);
    /// leer, solange es keine Schranke gibt.
    /// </summary>
    public string Verfahren
    {
        get => verfahren;
        private set
        {
            if (SetProperty(ref verfahren, value))
            {
                OnPropertyChanged(nameof(HatVerfahren));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es den Text zum Verfahren HiGHS gibt.</summary>
    public bool HatVerfahren => Verfahren.Length > 0;

    /// <summary>Holt die letzten Schritte des Automodus, der neueste zuerst.</summary>
    public IReadOnlyList<string> Protokoll
    {
        get => protokoll;
        private set
        {
            if (!protokoll.SequenceEqual(value))
            {
                protokoll = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HatProtokoll));
            }
        }
    }

    /// <summary>
    /// Holt einen Wert, der angibt, ob die Teilneuplanung läuft (ihr Stand steht im Schaubild, die Erklärung darunter).
    /// </summary>
    public bool ZeigtTeilneuplanung
    {
        get => zeigtTeilneuplanung;
        private set => SetProperty(ref zeigtTeilneuplanung, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob es Schritte des Automodus gibt.</summary>
    public bool HatProtokoll => Protokoll.Count > 0;

    /// <summary>Zeigt im Schaubild, woran HiGHS für die untere Schranke gerade rechnet.</summary>
    /// <param name="fortschritt">Der Schritt; <c>null</c>, wenn HiGHS fertig ist.</param>
    public void HighsZeigen(Schrankenfortschritt? fortschritt) => Schaubild.SchrankeZeigen(fortschritt);

    /// <summary>Übernimmt den Stand der Generierung (im Takt der Statusabfrage).</summary>
    /// <param name="stand">Der Stand.</param>
    public void Uebernehmen(Optimierungsstand stand)
    {
        ArgumentNullException.ThrowIfNull(stand);
        if (stand.Maschinenraum is not Maschinenraum raum)
        {
            Ueberschrift = "Keine Generierung aktiv";
            inseln.Clear();
            Automodus = false;
            ZeigtTeilneuplanung = false;
            Verfahren = string.Empty;
            Protokoll = [];
            GewichteZeigen([]);
            Schaubild.Uebernehmen(stand, inseln);
            return;
        }

        InselnUebernehmen(raum);
        Automodus = stand.Lenkung is not null;
        ZeigtTeilneuplanung = stand.Teilneuplanung is not null;
        Verfahren = stand.Lenkung is { } lenkung ? Verfahrenstext(lenkung) : string.Empty;
        Protokoll = stand.Lenkung?.Aenderungen.TakeLast(ProtokollZeilen).Reverse().ToArray() ?? [];
        GewichteZeigen(stand.Lenkung?.Gewichte ?? []);
        Schaubild.Uebernehmen(stand, inseln);

        // HiGHS hat keine eigenen Kerne: Es teilt sich den Prozessor mit der Suche (gemessener Anteil).
        int normale = raum.Inseln.Count(i => !i.Spezial);
        string highs = stand.Last is { Highs: > 0.001 } last
            ? string.Create(Deutsch, $" · HiGHS {last.Highs:P0} des Prozessors")
            : string.Empty;
        string zustand = stand.Pausiert ? " · angehalten" : string.Empty;
        Ueberschrift = string.Create(
            Deutsch,
            $"{raum.Kerne.Count} Rechenkerne der Suche · {normale} Inseln{(normale < raum.Inseln.Count ? " und Spezial-Insel" : string.Empty)}{highs} · {stand.PlaeneProSekunde:N0} Pläne/s{zustand}");
    }

    /// <summary>
    /// Text unter dem Balken einer Insel: wie viele Pläne sie schon nichts Besseres gefunden hat und was das bedeutet. Neu
    /// beginnt nur die schlechteste Insel, und erst über der Grenze; die anderen rechnen auch darüber hinaus weiter.
    /// </summary>
    /// <param name="insel">Die Insel.</param>
    /// <param name="grenze">Pläne ohne Verbesserung, ab denen die schlechteste Insel neu beginnt.</param>
    /// <returns>Der Text.</returns>
    private static string Fortschrittstext(Inselstand insel, long grenze)
    {
        string plaene = string.Create(Deutsch, $"Seit {Kostenanzeige.Kurz(insel.OhneVerbesserung)} Plänen nichts Besseres");
        if (insel.OhneVerbesserung <= grenze)
        {
            return string.Create(Deutsch, $"{plaene} – Neustart frühestens nach {Kostenanzeige.Kurz(grenze)}");
        }

        return insel.Schlechteste
            ? plaene + " – beginnt gleich neu (schlechteste Insel)"
            : plaene + " – rechnet weiter, neu beginnt nur die schlechteste Insel";
    }

    /// <summary>Skala eines Gewichts: Ring bei der Gewichtung des Anwenders, Punkt bei der aktuellen, Linie dazwischen.</summary>
    /// <param name="vorher">Gewichtung des Anwenders.</param>
    /// <param name="jetzt">Gewichtung des Automodus.</param>
    /// <returns>Die Stufen „sehr wenig“ bis „extrem hoch“.</returns>
    private static Gewichtssegment[] Segmente(Gewichtung vorher, Gewichtung jetzt)
    {
        int von = Math.Min((int)vorher, (int)jetzt);
        int bis = Math.Max((int)vorher, (int)jetzt);
        return Enumerable.Range(1, Stufen)
            .Select(stufe => new Gewichtssegment(
                stufe == (int)vorher && vorher != jetzt,
                stufe == (int)jetzt,
                stufe > von && stufe <= bis,
                stufe >= von && stufe < bis))
            .ToArray();
    }

    /// <summary>Stufe mit Nummer wie in der Qualitätsansicht: A2 … (A1 sind die harten Fehler), B1 …, C1 ….</summary>
    /// <param name="einteilung">Die vollständige Einteilung.</param>
    /// <param name="k">Das Kriterium.</param>
    /// <returns>Z. B. <c>A2</c>.</returns>
    private static string StufeMitNummer(Stufeneinteilung einteilung, Kostenkriterium k)
    {
        Stufe stufe = einteilung.StufeVon(k);
        int nummer = einteilung.Position(k) + (stufe == Stufe.A ? 2 : 1);
        return stufe.ToString() + nummer.ToString(Deutsch);
    }

    /// <summary>
    /// Zeilen eines Kriteriums: je Mannschaft; dieselbe Änderung bei mehr als drei Mannschaften als eine Zeile (etwa wenn eine
    /// bestmögliche Stufe geschützt wird).
    /// </summary>
    /// <param name="anhebungen">Die geänderten Gewichte des Kriteriums.</param>
    /// <returns>Die Zeilen.</returns>
    private static Gewichtszeile[] Zeilen(IEnumerable<Gewichtsanhebung> anhebungen) =>
        anhebungen
            .GroupBy(a => (a.Vorher, a.Jetzt))
            .SelectMany(g => g.Count() > 3
                ? [new Gewichtsanhebung(g.First().Kriterium, string.Create(Deutsch, $"{g.Count()} Mannschaften"), g.Key.Vorher, g.Key.Jetzt)]
                : g.AsEnumerable())
            .OrderBy(a => a.Mannschaft is not null)
            .ThenBy(a => a.Mannschaft, StringComparer.CurrentCulture)
            .Select(a => new Gewichtszeile(
                a.Mannschaft ?? "alle Mannschaften",
                Segmente(a.Vorher, a.Jetzt),
                $"{Gewichtungsanzeige.Name(a.Vorher)} → {Gewichtungsanzeige.Name(a.Jetzt)}"))
            .ToArray();

    private static string Spezialhinweis(Maschinenraum raum, Inselstand insel)
    {
        if (!insel.Laeuft || raum.SpezialKostenart is not { } art)
        {
            return "Wartet noch: Sie beginnt, wenn die Inseln eine Weile gerechnet haben.";
        }

        string verwendet = raum.SpezialVerwendet == 1 ? "einmal" : raum.SpezialVerwendet.ToString(Deutsch) + "-mal";
        return $"Gewichtet „{Gewichtungsanzeige.Name(art)}“ extrem hoch. Ihr Plan hat bisher {verwendet} eine Insel neu gestartet.";
    }

    /// <summary>Baut die Gewichte nur neu auf, wenn sie sich geändert haben (sonst flackerte die Ansicht im Takt).</summary>
    /// <param name="anhebungen">Die geänderten Gewichte.</param>
    private void GewichteZeigen(IReadOnlyList<Gewichtsanhebung> anhebungen)
    {
        Stufeneinteilung einteilung = hauptfenster.Einteilung.Vervollstaendigt();
        string schluessel = string.Join(';', anhebungen.Select(a => $"{a.Kriterium},{a.Mannschaft},{a.Vorher},{a.Jetzt}"));
        if (schluessel == gewichteSchluessel)
        {
            return;
        }

        gewichteSchluessel = schluessel;
        Kostenkriterium[] reihenfolge = [.. einteilung.A, .. einteilung.B, .. einteilung.C];
        Gewichte = anhebungen
            .GroupBy(a => a.Kriterium)
            .OrderBy(g => Array.IndexOf(reihenfolge, g.Key))
            .Select(g => new Gewichtsgruppe(
                StufeMitNummer(einteilung, g.Key),
                Planqualitaet.Name(g.Key),
                Zeilen(g)))
            .ToArray();
    }

    /// <summary>
    /// Was der Automodus aus HiGHS weiß und wie er danach arbeitet: Schranke je Stufe gegen den besten Plan, geschützte
    /// Stufen.
    /// </summary>
    /// <param name="lenkung">Der Stand des Automodus.</param>
    /// <returns>Der Text; leer ohne Schranke.</returns>
    private string Verfahrenstext(Lenkungsstand lenkung)
    {
        Stufenwert b = lenkung.Bester;
        var teile = new List<string>();
        if (lenkung.SchrankeA is not null)
        {
            int? a = lenkung.SchrankeA;
            int? bb = lenkung.SchrankeB;
            string Teil(string stufe, int? schranke, int wert)
            {
                if (schranke is not int s)
                {
                    return stufe + " noch offen";
                }

                string erreicht = wert <= s ? " – bestmöglich" : string.Empty;
                return string.Create(Deutsch, $"{stufe} ≥ {s} (bester Plan {wert}{erreicht})");
            }

            teile.Add($"Untere Schranke aus HiGHS (Summe je Stufe): {Teil("A", a, b.A)}, {Teil("B", bb, b.B)}.");
            teile.Add("Ist eine Stufe bestmöglich, hört der Automodus dort auf zu drängen und setzt ihre Gewichte auf „extrem hoch“, "
                + "damit die Suche sie hält; danach glättet er C.");
        }

        if (lenkung.Festgelegt is { } festgelegt)
        {
            teile.Add($"Festgelegt vom Staffelleiter: {festgelegt} – A und B bleiben höchstens so, der Automodus glättet nur noch C.");
        }

        return string.Join(" ", teile);
    }

    private void InselnUebernehmen(Maschinenraum raum)
    {
        if (inseln.Count != raum.Inseln.Count)
        {
            inseln.Clear();
            foreach (Inselstand insel in raum.Inseln)
            {
                inseln.Add(new Inselanzeige(insel.Spezial ? "Spezial-Insel" : "Insel " + insel.Nummer.ToString(Deutsch), insel.Spezial));
            }
        }

        double beste = raum.Inseln.Where(i => !i.Spezial && i.Kosten >= 0).Select(i => i.Kosten).DefaultIfEmpty(-1).Min();
        Stufenwert? besterWert = null;
        foreach (Stufenwert wert in raum.Inseln.Select(i => i.Verstoesse).OfType<Stufenwert>())
        {
            if (besterWert is not { } bisher || wert.IstBesserAls(bisher))
            {
                besterWert = wert;
            }
        }

        for (int i = 0; i < raum.Inseln.Count; i++)
        {
            Inselstand insel = raum.Inseln[i];
            Inselanzeige anzeige = inseln[i];
            anzeige.Laeuft = insel.Laeuft;
            anzeige.Kosten = insel.Kosten < 0 ? "–" : Kostenanzeige.Kurz(insel.Kosten);
            anzeige.IstBeste = insel.Verstoesse is { } eigener
                ? besterWert is { } b && !b.IstBesserAls(eigener)
                : besterWert is null && !insel.Spezial && insel.Kosten >= 0 && insel.Kosten <= beste;
            anzeige.ZeigtVerstoesse = insel.Verstoesse is not null;
            if (insel.Verstoesse is { } verstoesse)
            {
                anzeige.VerstossA = verstoesse.A.ToString(Deutsch);
                anzeige.VerstossB = verstoesse.B.ToString(Deutsch);
                anzeige.VerstossC = verstoesse.CAnzahl.ToString(Deutsch);
            }

            anzeige.Strategien = "Suchstrategien der Suchplätze: " + string.Join(" · ", insel.Strategien);
            anzeige.Kostentreiber = (insel.Kostentreiber ?? [])
                .Select(k => new Kostentreiberzeile(
                    Planqualitaet.Name(k.Kriterium),
                    Balkenbreite * k.Anteil,
                    k.Verstoesse is int verstoesse ? verstoesse.ToString(Deutsch) : k.Anteil.ToString("P0", Deutsch)))
                .ToArray();
            anzeige.Neustarts = insel.Neustarts == 1 ? "1 Neustart" : insel.Neustarts.ToString(Deutsch) + " Neustarts";
            if (insel.Spezial)
            {
                anzeige.Hinweis = Spezialhinweis(raum, insel);
                continue;
            }

            long grenze = Math.Max(1, raum.DurchlaeufeVorNeustart);
            anzeige.Fortschritt = Math.Clamp(100.0 * insel.OhneVerbesserung / grenze, 0, 100);
            anzeige.FortschrittText = Fortschrittstext(insel, grenze);
        }
    }
}
