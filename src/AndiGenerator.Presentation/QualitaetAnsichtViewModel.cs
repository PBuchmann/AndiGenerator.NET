// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Schranke;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Qualitätsansicht: Verstöße je Kriterium in der Rangfolge aus MIGRATIONSPLAN E14. Im Automodus legt der Staffelleiter
/// hier die Einteilung in die Stufen A, B und C und die Reihenfolge darin fest – mit Knöpfen oder durch Ziehen und Ablegen
/// mit Vorschau (gespeichert mit den Spielplandaten); der Automodus lenkt danach. Kosten zeigt die Ansicht nur bei der
/// Kostenoptimierung.
/// </summary>
public sealed class QualitaetAnsichtViewModel : AnsichtViewModel
{
    /// <summary>Name der harten Fehler „Spiele ohne Termin“ (wie <see cref="Planqualitaet.Kriterien"/>).</summary>
    private const string OhneTermin = "Spiele ohne Termin";
    private const string OhneZwang = "kein Verstoß zwingend";

    private IReadOnlyList<Qualitaetszeile> zeilen = [];
    private string zusammenfassung = string.Empty;
    private bool pflichtErfuellt = true;
    private bool zeigtKosten = true;
    private bool bearbeitbar;
    private bool zeigtSchranke;
    private bool zeigtFestlegung;
    private string schrankeHinweis = string.Empty;
    private Planstand? letzter;
    private Qualitaetszeile? schwebend;
    private Kostenkriterium? gezogen;
    private Stufeneinteilung? ausgangslage;
    private Stufeneinteilung? vorschau;
    private string? gewaehlt;
    private (Planstand? Stand, string? Name) detailsFuer;
    private IReadOnlyList<Meldungsgruppe> details = [];
    private string detailTitel = string.Empty;
    private string folgeTitel = string.Empty;
    private IReadOnlyList<Unterschied> folge = [];

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="hauptfenster">Hauptfenster.</param>
    /// <param name="id">Eindeutige Kennung im Layout.</param>
    public QualitaetAnsichtViewModel(HauptfensterViewModel hauptfenster, string id)
        : base(hauptfenster, "Qualität", id)
    {
        DetailsSchliessenCommand = new RelayCommand(() => Waehlen(null));
        AufhebenCommand = new RelayCommand(() => Hauptfenster.FestlegungAufheben());
    }

    /// <summary>Holt die Kriterien.</summary>
    public IReadOnlyList<Qualitaetszeile> Zeilen
    {
        get => zeilen;
        private set => SetProperty(ref zeilen, value);
    }

    /// <summary>Holt die Verstöße des gewählten Kriteriums je Mannschaft (rechts neben der Tabelle); leer = nichts anzeigen.</summary>
    public IReadOnlyList<Meldungsgruppe> Details
    {
        get => details;
        private set
        {
            if (SetProperty(ref details, value))
            {
                OnPropertyChanged(nameof(ZeigtDetails));
            }
        }
    }

    /// <summary>
    /// Holt unter den Verstößen eines B-Kriteriums, was es kostet, es so gut wie möglich zu machen (aus den B-Varianten), z. B.
    /// <c>Mit Vorrang (bestmöglich): 0 statt 8 Verstöße – dafür:</c>; leer, wenn es nichts zu zeigen gibt.
    /// </summary>
    public string FolgeTitel
    {
        get => folgeTitel;
        private set
        {
            if (SetProperty(ref folgeTitel, value))
            {
                OnPropertyChanged(nameof(ZeigtFolge));
                OnPropertyChanged(nameof(ZeigtDetails));
            }
        }
    }

    /// <summary>Holt die Folgen je anderem A- und B-Kriterium (rot schlechter, grün besser) und für C.</summary>
    public IReadOnlyList<Unterschied> Folge
    {
        get => folge;
        private set => SetProperty(ref folge, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob die Folgen gezeigt werden.</summary>
    public bool ZeigtFolge => FolgeTitel.Length > 0;

    /// <summary>Holt einen Wert, der angibt, ob rechts Verstöße (oder die Folgen) angezeigt werden.</summary>
    public bool ZeigtDetails => Details.Count > 0 || ZeigtFolge;

    /// <summary>Holt die Überschrift der Verstöße, z. B. <c>B3 · Sperrtermine</c>.</summary>
    public string DetailTitel
    {
        get => detailTitel;
        private set => SetProperty(ref detailTitel, value);
    }

    /// <summary>Holt den Befehl, der die Festlegung aufhebt.</summary>
    public IRelayCommand AufhebenCommand { get; }

    /// <summary>Holt den Befehl, der die Verstöße rechts schließt (Auswahl aufheben).</summary>
    public IRelayCommand DetailsSchliessenCommand { get; }

    /// <summary>Holt die Zusammenfassung, z. B. <c>Pflichtstufe A erfüllt · 3 Kriterien mit Verstößen</c>.</summary>
    public string Zusammenfassung
    {
        get => zusammenfassung;
        private set => SetProperty(ref zusammenfassung, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob alle Kriterien der Pflichtstufe A erfüllt sind.</summary>
    public bool PflichtErfuellt
    {
        get => pflichtErfuellt;
        private set => SetProperty(ref pflichtErfuellt, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob die Kosten gezeigt werden (nur bei der Kostenoptimierung).</summary>
    public bool ZeigtKosten
    {
        get => zeigtKosten;
        private set => SetProperty(ref zeigtKosten, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob die Einteilung bearbeitet werden kann (nur im Automodus).</summary>
    public bool Bearbeitbar
    {
        get => bearbeitbar;
        private set => SetProperty(ref bearbeitbar, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob die Spalte „Bestmöglich“ gezeigt wird (im Automodus, wenn HiGHS rechnen kann).</summary>
    public bool ZeigtSchranke
    {
        get => zeigtSchranke;
        private set => SetProperty(ref zeigtSchranke, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob die Spalte „Festgelegt“ gezeigt wird (eine B-Variante ist übernommen).</summary>
    public bool ZeigtFestlegung
    {
        get => zeigtFestlegung;
        private set => SetProperty(ref zeigtFestlegung, value);
    }

    /// <summary>
    /// Holt den Grund, warum es im Automodus keine Spalte „Bestmöglich“ gibt (z. B. nur Rückrunde geplant); sonst leer.
    /// </summary>
    public string SchrankeHinweis
    {
        get => schrankeHinweis;
        private set
        {
            if (SetProperty(ref schrankeHinweis, value))
            {
                OnPropertyChanged(nameof(HatSchrankeHinweis));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob ein Grund für die fehlende Spalte „Bestmöglich“ gezeigt wird.</summary>
    public bool HatSchrankeHinweis => SchrankeHinweis.Length > 0;

    /// <summary>
    /// Holt die gezogene Zeile, wie sie am Mauszeiger hängt (mit der Stufe, die sie beim Ablegen bekäme); <c>null</c>,
    /// solange nicht gezogen wird. In der Tabelle steht an ihrer Stelle eine Lücke.
    /// </summary>
    public Qualitaetszeile? Schwebend
    {
        get => schwebend;
        private set => SetProperty(ref schwebend, value);
    }

    /// <summary>
    /// Wählt eine Zeile: rechts daneben stehen ihre Verstöße je Mannschaft. Ein zweiter Klick auf dieselbe Zeile oder
    /// <c>null</c> hebt die Auswahl auf. Gibt es keine Verstöße, bleibt die rechte Tabelle zu.
    /// </summary>
    /// <param name="zeile">Die Zeile oder <c>null</c>.</param>
    public void Waehlen(Qualitaetszeile? zeile)
    {
        gewaehlt = zeile is null || zeile.Name == gewaehlt ? null : zeile.Name;
        AuswahlZeigen();
    }

    /// <summary>Beginnt das Ziehen einer Zeile (nur im Automodus, nicht bei den harten Fehlern).</summary>
    /// <param name="zeile">Die Zeile.</param>
    /// <returns><c>true</c>, wenn gezogen wird.</returns>
    public bool ZiehenBeginnen(Qualitaetszeile zeile)
    {
        ArgumentNullException.ThrowIfNull(zeile);
        if (!Bearbeitbar || zeile.Kriterium is not Kostenkriterium k)
        {
            return false;
        }

        gezogen = k;
        ausgangslage = Hauptfenster.Einteilung.Vervollstaendigt();
        vorschau = ausgangslage;
        Neu();
        return true;
    }

    /// <summary>
    /// Die gezogene Zeile ist über <paramref name="ziel"/>: Vorschau, wie die Tabelle nach dem Ablegen aussähe (Stufe und
    /// Nummer ergeben sich aus der Stelle). Über der oberen Hälfte einer Zeile kommt sie davor, sonst dahinter.
    /// </summary>
    /// <param name="ziel">Die Zeile unter dem Zeiger.</param>
    /// <param name="davor"><c>true</c> = obere Hälfte.</param>
    public void ZiehenUeber(Qualitaetszeile ziel, bool davor)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        if (gezogen is not Kostenkriterium k || ausgangslage is null || ziel.Kriterium is not Kostenkriterium nachbar || nachbar == k)
        {
            return;
        }

        Stufeneinteilung neu = ausgangslage.Platzieren(k, nachbar, davor);
        if (neu.Text() != vorschau?.Text())
        {
            vorschau = neu;
            Neu();
        }
    }

    /// <summary>Beendet das Ziehen: übernimmt die Vorschau (Ablegen) oder verwirft sie (Abbrechen).</summary>
    /// <param name="uebernehmen"><c>true</c> = ablegen.</param>
    public void ZiehenBeenden(bool uebernehmen)
    {
        Stufeneinteilung? neu = vorschau;
        Stufeneinteilung? alt = ausgangslage;
        gezogen = null;
        vorschau = null;
        ausgangslage = null;
        if (uebernehmen && neu is not null && alt is not null && neu.Text() != alt.Text())
        {
            _ = Hauptfenster.EinteilungAendernAsync(_ => neu);
        }

        Neu();
    }

    /// <summary>HiGHS meldet Fortschritt: nur den Hinweis erneuern.</summary>
    internal void FortschrittZeigen()
    {
        if (Hauptfenster.Automodus && Hauptfenster.SchrankeFortschritt.Length > 0)
        {
            SchrankeHinweis = $"HiGHS rechnet: {Hauptfenster.SchrankeFortschritt} …";
        }
    }

    /// <summary>Das Verfahren hat gewechselt: Kosten und Bearbeitung zeigen oder ausblenden.</summary>
    internal void VerfahrenGeaendert() => Neu();

    /// <inheritdoc/>
    protected override void Anzeigen(Planstand? stand)
    {
        letzter = stand;
        ZeigtKosten = !Hauptfenster.Automodus;
        Bearbeitbar = Hauptfenster.Automodus;
        if (stand is null)
        {
            Zeilen = [];
            Zusammenfassung = string.Empty;
            PflichtErfuellt = true;
            AuswahlZeigen();
            return;
        }

        Stufeneinteilung einteilung = vorschau ?? Hauptfenster.Einteilung.Vervollstaendigt();
        IReadOnlyList<Qualitaetskriterium> kriterien = Planqualitaet.Kriterien(stand.Bewertung, einteilung);

        // Gleiche Zeilen (Stufe und Kriterium) werden nur aktualisiert, damit eine offene Auswahl nicht verschwindet.
        bool gleich = Zeilen.Count == kriterien.Count && Zeilen.Zip(kriterien).All(p => p.First.Stufe == p.Second.Stufe && p.First.Name == p.Second.Name);
        if (!gleich)
        {
            Zeilen = kriterien.Select((k, i) => NeueZeile(k, Nachbar(kriterien, i, -1), Nachbar(kriterien, i, +1))).ToList();
        }

        foreach ((Qualitaetszeile zeile, Qualitaetskriterium k) in Zeilen.Zip(kriterien))
        {
            Werte(zeile, k);
        }

        Schwebend = gezogen is null ? null : SchwebendeZeile(kriterien);
        AuswahlZeigen();
        (bool bestA, bool bestB) = SchrankeZeigen(kriterien);

        int pflicht = kriterien.Count(k => k.IstPflicht && !k.Erfuellt);
        int uebrige = kriterien.Count(k => !k.IstPflicht && !k.Erfuellt);
        PflichtErfuellt = pflicht == 0 || bestA;
        string a = (pflicht, bestA) switch
        {
            (0, _) => "Pflichtstufe A erfüllt",
            (_, true) => "Pflichtstufe A: Optimum erreicht (Verstöße unvermeidbar)",
            _ => $"Pflichtstufe A: {pflicht} Kriterien verletzt",
        };
        bool verstoesseB = kriterien.Any(k => k.Stufe[0] == 'B' && !k.Erfuellt);
        string b = bestB && verstoesseB ? " · Stufe B: Optimum erreicht" : string.Empty;
        Zusammenfassung = ZeigtKosten
            ? $"{a}{b} · {uebrige} weitere Kriterien mit Verstößen · Gesamtkosten {Kostenanzeige.Kurz(stand.Bewertung.Gesamtkosten)}"
            : $"{a}{b} · {uebrige} weitere Kriterien mit Verstößen";
    }

    /// <summary>Text und Erreicht-Marke der Schranke für ein Kriterium.</summary>
    /// <remarks>Bei einer Untergrenze 0 erzwingt nichts einen Verstoß: dann dieser Text statt „nie unter 0“.</remarks>
    private static (string Text, bool Erreicht) Schrankentext(Qualitaetskriterium k, int wert) => wert <= 0
        ? (OhneZwang, false)
        : ("nie unter " + wert.ToString(CultureInfo.InvariantCulture), Planwert(k) == wert);

    /// <summary>Verstöße des Plans wie in der Schranke.</summary>
    private static int? Planwert(Qualitaetskriterium k) => k.Anzahl;

    /// <summary>Das sichtbare Nachbarkriterium in derselben Stufe (−1 darüber, +1 darunter); <c>null</c> am Rand der Stufe.</summary>
    private static Kostenkriterium? Nachbar(IReadOnlyList<Qualitaetskriterium> kriterien, int index, int richtung)
    {
        int j = index + richtung;
        if (kriterien[index].Kriterium is null || j < 0 || j >= kriterien.Count || kriterien[j].Kriterium is not Kostenkriterium nachbar)
        {
            return null;
        }

        return kriterien[j].Stufe[0] == kriterien[index].Stufe[0] ? nachbar : null;
    }

    /// <summary>Verschiebt <paramref name="k"/> an <paramref name="nachbar"/> vorbei (dazwischen liegen ggf. ausgeblendete Kriterien).</summary>
    private static Stufeneinteilung Vorbei(Stufeneinteilung e, Kostenkriterium k, Kostenkriterium nachbar, int richtung)
    {
        Stufeneinteilung neu = e;
        for (int i = 0; i < Stufeneinteilung.AnzahlKriterien && Math.Sign(neu.Position(nachbar) - neu.Position(k)) == richtung; i++)
        {
            neu = neu.Verschieben(k, richtung);
        }

        return neu;
    }

    private static void Werte(Qualitaetszeile zeile, Qualitaetskriterium k)
    {
        zeile.Verstoesse = k.Anzahl switch
        {
            null => "–",
            int anzahl when k.Gesamtzahl is int von => $"{anzahl} von {von}",
            int anzahl => anzahl.ToString(CultureInfo.InvariantCulture),
        };
        zeile.Kosten = Kostenanzeige.Kurz(k.Kosten);
        zeile.Zustand = (k.Erfuellt, k.IstPflicht) switch
        {
            (true, _) => Qualitaetszustand.Erfuellt,
            (false, true) => Qualitaetszustand.PflichtVerletzt,
            _ => Qualitaetszustand.Verletzt,
        };
    }

    /// <summary>
    /// Füllt die Spalte „Bestmöglich“ (nur im Automodus, Stufen A und B) und meldet je Stufe, ob der Plan in allen ihren
    /// Kriterien die Schranke erreicht oder sie erfüllt – dann geht es dort nicht besser (B nur, wenn auch A bestmöglich ist).
    /// </summary>
    private (bool A, bool B) SchrankeZeigen(IReadOnlyList<Qualitaetskriterium> kriterien)
    {
        Schrankenbericht? bericht = Hauptfenster.Automodus ? Hauptfenster.SchrankeHolen() : null;
        IReadOnlyList<Schrankenstufe>? schranke = Hauptfenster.Automodus ? bericht?.Stufen : [];
        ZeigtSchranke = schranke is not { Count: 0 };
        SchrankeHinweis = bericht is { Stufen.Count: 0, Hinweise.Count: > 0 } ? "Bestmöglich nicht berechnet: " + string.Join(" ", bericht.Hinweise) : string.Empty;
        if (Hauptfenster.Automodus && schranke is null)
        {
            SchrankeHinweis = Hauptfenster.SchrankeFortschritt.Length > 0 ? $"HiGHS rechnet: {Hauptfenster.SchrankeFortschritt} …" : "HiGHS rechnet …";
        }

        // Die Stufen zählen als Summe (wie im Automodus): A ist bestmöglich, wenn der Plan keine harten Fehler hat und die Summe
        // seiner A-Verstöße die Schranke erreicht; B ebenso, bei bestmöglichem A.
        int? schrankeA = bericht?.SchrankeA;
        int? schrankeB = bericht?.SchrankeB;
        int harte = kriterien.Where(k => k.Kriterium is null).Sum(k => Planwert(k) ?? 0);
        int Summe(char stufe) => kriterien.Where(k => k.Stufe[0] == stufe && k.Kriterium is not null).Sum(k => Planwert(k) ?? 0);
        bool a = schranke is { Count: > 0 } && schrankeA is int sa && harte == 0 && Summe('A') <= sa;
        bool b = a && schrankeB is int sb && Summe('B') <= sb;
        if (bericht?.SummeA is not null)
        {
            SchrankeHinweis = "Je Stufe: Grenze = so wenige Verstöße hat jeder Plan in der Stufe mindestens (HiGHS). Je Kriterium: Untergrenze = weniger hat kein Plan, selbst wenn nur dieses Kriterium zählte – ob sie erreichbar ist, ist offen.";
        }

        KoepfeSetzen(kriterien, bericht, schranke is null, harte, (a, b));
        AlternativenZeigen(kriterien);

        foreach ((Qualitaetszeile zeile, Qualitaetskriterium k) in Zeilen.Zip(kriterien))
        {
            bool ab = k.Stufe[0] is 'A' or 'B';

            // Ungültige Termine und spielfreie Tage braucht kein Plan (das Spiel bliebe sonst ohne Termin): Schranke 0.
            Schrankenstufe? stufe = schranke?.FirstOrDefault(s => k.Kriterium is null ? s.Kriterium is null : s.Kriterium == k.Kriterium);
            if (k.Kriterium is null && k.Name != OhneTermin && stufe is not null)
            {
                stufe = stufe with { Schranke = 0, Einzeln = 0 };
            }

            zeile.BestmoeglichRechnet = schranke is null && ab;

            // A und B: der Wert des Kriteriums allein; C: der bestmögliche Wert bei bestmöglichem A und B – nur, wenn der Plan
            // diese erreicht (sonst unbekannt). In einem Zug gesetzt (kein kurzes Leeren dazwischen).
            (zeile.Bestmoeglich, zeile.BestmoeglichErreicht) = stufe is not null && (ab || (k.Stufe[0] == 'C' && b))
                ? Schrankentext(k, stufe.Schranke)
                : (string.Empty, false);

            // Erreicht der Plan die Grenze der Stufe A, sind die übrigen Verstöße dort unvermeidbar: kein rotes Kreuz.
            if (a && k.Stufe[0] == 'A' && zeile.Zustand == Qualitaetszustand.PflichtVerletzt)
            {
                zeile.Zustand = Qualitaetszustand.Verletzt;
            }
        }

        return (a, b);
    }

    /// <summary>
    /// Setzt über die erste Zeile jeder Stufe ihre Kopfzeile (nur im Automodus): Verstöße des Plans in der Stufe, die Grenze aus
    /// HiGHS und wie viel besser der Plan dort höchstens noch werden kann. Die Grenze ist auch ohne Beweis eine gültige untere
    /// Schranke; die von B gilt nur bei bestmöglichem A.
    /// </summary>
    private void KoepfeSetzen(IReadOnlyList<Qualitaetskriterium> kriterien, Schrankenbericht? bericht, bool rechnet, int harte, (bool A, bool B) best)
    {
        CultureInfo deutsch = CultureInfo.GetCultureInfo("de-DE");
        string Zahl(int n) => n.ToString(deutsch);
        int Summe(char stufe) => kriterien.Where(k => k.Stufe[0] == stufe && k.Kriterium is not null).Sum(k => Planwert(k) ?? 0);

        Stufenkopf Kopf(char stufe)
        {
            string titel = stufe switch
            {
                'A' => "Stufe A · muss",
                'B' => "Stufe B · soll",
                _ => "Stufe C · wird geglättet",
            };
            int plan = Summe(stufe);
            string planText = "Plan " + Zahl(plan);
            Schrankenstufe? summe = stufe switch
            {
                'A' => bericht?.SummeA,
                'B' => bericht?.SummeB,
                _ => null,
            };
            if (stufe == 'C')
            {
                return new Stufenkopf(titel, planText, string.Empty, best.B ? "Grenze je Kriterium in der Spalte „allein“" : "keine Grenze – erst wenn A und B bestmöglich sind", Spielraumzustand.Unbekannt);
            }

            if (summe is null)
            {
                return new Stufenkopf(titel, planText, string.Empty, rechnet ? "HiGHS rechnet …" : "keine Grenze", Spielraumzustand.Unbekannt);
            }

            string grenze = "Grenze ≥ " + Zahl(summe.Schranke);
            double anteil = plan <= 0 ? 100 : Math.Clamp(100.0 * summe.Schranke / plan, 0, 100);
            if (stufe == 'A' && harte > 0)
            {
                return new Stufenkopf(titel, planText, grenze, $"erst die harten Fehler beheben ({Zahl(harte)})", Spielraumzustand.Offen);
            }

            if (stufe == 'B' && !best.A)
            {
                return new Stufenkopf(titel, planText, grenze, "die Grenze gilt erst, wenn A bestmöglich ist", Spielraumzustand.Unbekannt);
            }

            // Erreicht der Plan die Grenze, ist er optimal (auch ohne Beweis von HiGHS: die Grenze gilt immer). Sonst liegt das
            // Optimum zwischen Grenze und Plan – bewiesen ist es erst, wenn HiGHS einen Plan mit der Grenze kennt.
            int luft = plan - summe.Schranke;
            if (luft <= 0)
            {
                return new Stufenkopf(titel, planText, grenze, "Optimum erreicht – besser geht es nicht", Spielraumzustand.Bestmoeglich);
            }

            string text = summe.Bewiesen
                ? $"Optimum ist {Zahl(summe.Schranke)}: noch {Zahl(luft)} Verstöße weniger möglich"
                : $"Optimum zwischen {Zahl(summe.Schranke)} und {Zahl(plan)}: höchstens {Zahl(luft)} Verstöße weniger möglich";
            return new Stufenkopf(titel, planText, grenze, text, Spielraumzustand.Offen, anteil);
        }

        // Nur im Automodus und wenn HiGHS rechnen kann (sonst gibt es keine Grenzen).
        bool zeigen = Hauptfenster.Automodus && ZeigtSchranke;
        char? vorige = null;
        foreach ((Qualitaetszeile zeile, Qualitaetskriterium k) in Zeilen.Zip(kriterien))
        {
            char stufe = k.Stufe[0];
            zeile.Kopf = zeigen && stufe != vorige ? Kopf(stufe) : null;
            vorige = stufe;
        }
    }

    /// <summary>
    /// Die B-Varianten (Automodus, aus HiGHS, im Hintergrund bewertet): ein Knopf in der Kopfzeile der Stufe B öffnet den Dialog
    /// zum Vergleichen und Wählen. Ist eine Variante übernommen, stehen je A- und B-Kriterium die Obergrenzen in der Spalte
    /// „Festgelegt“, und der Knopf hebt die Festlegung auf.
    /// </summary>
    private void AlternativenZeigen(IReadOnlyList<Qualitaetskriterium> kriterien)
    {
        CultureInfo deutsch = CultureInfo.GetCultureInfo("de-DE");
        foreach (Qualitaetszeile zeile in Zeilen)
        {
            zeile.Festgelegt = string.Empty;
        }

        Qualitaetszeile? kopfzeileB = Zeilen.FirstOrDefault(z => z.Klasse == "B" && z.Kopf is not null);

        // Festgelegt: je A- und B-Kriterium die Obergrenze, in den Kopfzeilen was gilt und der Knopf zum Aufheben.
        if (Hauptfenster.Automodus && Hauptfenster.Festlegung is { } festlegung)
        {
            foreach ((Qualitaetszeile zeile, Qualitaetskriterium k) in Zeilen.Zip(kriterien))
            {
                if (k.Kriterium is Kostenkriterium kriterium && festlegung.Grenzen.TryGetValue(kriterium, out int grenze))
                {
                    zeile.Festgelegt = string.Create(deutsch, $"höchstens {grenze}");
                }
            }

            bool eingehalten = kriterien.All(k => k.Kriterium is not Kostenkriterium x || !festlegung.Grenzen.TryGetValue(x, out int g) || (k.Anzahl ?? 0) <= g);
            foreach (Qualitaetszeile erste in Zeilen.Where(z => z.Kopf is not null && z.Klasse is "A" or "B"))
            {
                erste.Kopf = erste.Kopf! with
                {
                    Spielraum = eingehalten ? $"festgelegt: {festlegung.Name} – bleibt so, C wird geglättet" : $"festgelegt: {festlegung.Name} – wird übernommen …",
                    Zustand = eingehalten ? Spielraumzustand.Bestmoeglich : Spielraumzustand.Offen,
                    Unvermeidbar = null,
                    Knopf = erste == kopfzeileB ? "Festlegung aufheben" : string.Empty,
                    KnopfBefehl = erste == kopfzeileB ? AufhebenCommand : null,
                };
            }

            ZeigtFestlegung = true;
            return;
        }

        ZeigtFestlegung = false;
        IReadOnlyList<(Planalternative Alternative, Planbewertung Bewertung)> liste = Hauptfenster.Automodus && gezogen is null ? Hauptfenster.AlternativenHolen() : [];
        if (kopfzeileB is { Kopf: { } kopf } && liste.Count > 0)
        {
            // Zuerst ausgewählt: der Plan mit der niedrigsten Summe in B (A und B zusammen bestmöglich).
            Planalternative start = liste.FirstOrDefault(e => e.Alternative.Kriterium is null).Alternative ?? liste[0].Alternative;
            kopfzeileB.Kopf = kopf with
            {
                Knopf = string.Create(deutsch, $"B-Varianten wählen ({liste.Count}) …"),
                KnopfBefehl = new AsyncRelayCommand(() => Hauptfenster.AlternativenZeigenAsync(start, letzter)),
            };
        }
    }

    /// <summary>Kopie der gezogenen Zeile für den Mauszeiger (nicht bearbeitbar, mit ihren Werten).</summary>
    private Qualitaetszeile? SchwebendeZeile(IReadOnlyList<Qualitaetskriterium> kriterien)
    {
        if (kriterien.FirstOrDefault(k => k.Kriterium == gezogen) is not Qualitaetskriterium k)
        {
            return null;
        }

        var zeile = new Qualitaetszeile(k.Stufe, k.Name, k.Kriterium);
        Werte(zeile, k);
        return zeile;
    }

    /// <summary>Markiert die gewählte Zeile und bildet ihre Verstöße (nur neu, wenn sich Plan oder Auswahl geändert haben).</summary>
    private void AuswahlZeigen()
    {
        Qualitaetszeile? zeile = Zeilen.FirstOrDefault(z => z.Name == gewaehlt);
        foreach (Qualitaetszeile z in Zeilen)
        {
            z.IstGewaehlt = z == zeile;
        }

        DetailTitel = zeile is null ? string.Empty : $"{zeile.Stufe} · {zeile.Name}";
        if (detailsFuer != (letzter, zeile?.Name))
        {
            detailsFuer = (letzter, zeile?.Name);
            Details = zeile is null ? [] : Hauptfenster.Kriteriumsdetails(zeile.Kriterium is null, zeile.Kriterium, letzter);
        }

        FolgeZeigen(zeile);
    }

    /// <summary>
    /// Was es kostet, ein B-Kriterium so gut wie möglich zu machen: die Variante mit Vorrang für es (sonst der Plan für die Summe
    /// in B, wenn es dort schon so gut ist), verglichen mit dem angezeigten Plan – nur, wenn sie dort weniger Verstöße hat.
    /// </summary>
    private void FolgeZeigen(Qualitaetszeile? zeile)
    {
        (string titel, IReadOnlyList<Unterschied> liste) = (string.Empty, []);
        IReadOnlyList<(Planalternative Alternative, Planbewertung Bewertung)> varianten =
            Hauptfenster.Automodus ? Hauptfenster.AlternativenHolen() : [];
        if (zeile is { Kriterium: Kostenkriterium kriterium } && zeile.Stufe.StartsWith('B') && letzter is { } plan
            && varianten
                .Where(v => v.Alternative.Kriterium == kriterium)
                .Concat(varianten.Where(v => v.Alternative is { Kriterium: null, AUndB: false }))
                .FirstOrDefault() is { Alternative: not null } paar)
        {
            Stufeneinteilung einteilung = Hauptfenster.Einteilung.Vervollstaendigt();
            IReadOnlyList<Qualitaetskriterium> jetzt = Planqualitaet.Kriterien(plan.Bewertung, einteilung);
            IReadOnlyList<Qualitaetskriterium> dann = Planqualitaet.Kriterien(paar.Bewertung, einteilung);
            int i = jetzt.ToList().FindIndex(k => k.Kriterium == kriterium);
            int vorher = jetzt[i].Anzahl ?? 0;
            int nachher = dann[i].Anzahl ?? 0;
            if (nachher < vorher)
            {
                CultureInfo deutsch = CultureInfo.GetCultureInfo("de-DE");
                string name = HauptfensterViewModel.Alternativname(paar.Alternative);
                titel = string.Create(deutsch, $"Mit „{name}“: {nachher} statt {vorher} Verstöße – dafür:");
                var folgen = new List<Unterschied>();
                for (int j = 0; j < jetzt.Count; j++)
                {
                    int a = jetzt[j].Anzahl ?? 0;
                    int b = dann[j].Anzahl ?? 0;
                    if (j != i && jetzt[j].Stufe[0] is 'A' or 'B' && jetzt[j].Kriterium is not null && a != b)
                    {
                        folgen.Add(new Unterschied(string.Create(deutsch, $"{jetzt[j].Stufe} · {jetzt[j].Name}: {a} → {b} ({b - a:+0;−0})"), Math.Sign(b - a)));
                    }
                }

                int cJetzt = jetzt.Where(k => k.Stufe[0] == 'C').Sum(k => k.Anzahl ?? 0);
                int cDann = dann.Where(k => k.Stufe[0] == 'C').Sum(k => k.Anzahl ?? 0);
                folgen.Add(new Unterschied(string.Create(deutsch, $"C zusammen: {cJetzt} → {cDann} (vor dem Glätten)"), 0));
                liste = folgen;
            }
        }

        Folge = liste;
        FolgeTitel = titel;
    }

    /// <summary>Baut die Zeilen neu auf (Bearbeitbarkeit oder Vorschau geändert).</summary>
    private void Neu()
    {
        Zeilen = [];
        Anzeigen(letzter);
    }

    private Qualitaetszeile NeueZeile(Qualitaetskriterium k, Kostenkriterium? oben, Kostenkriterium? unten)
    {
        if (k.Kriterium is not Kostenkriterium kriterium || !Bearbeitbar)
        {
            return new Qualitaetszeile(k.Stufe, k.Name, k.Kriterium);
        }

        // Hoch/runter innerhalb der Stufe; am Rand der Stufe in die Nachbarstufe: nach unten an deren erste Stelle,
        // nach oben ans Ende (Stufeneinteilung.Einstufen). Spieltage ohne Anzahl bleiben in C.
        Stufe klasse = (Stufe)(k.Stufe[0] - 'A');
        Action? hoch = oben switch
        {
            Kostenkriterium o => () => _ = Hauptfenster.EinteilungAendernAsync(e => Vorbei(e, kriterium, o, -1)),
            null when klasse > Stufe.A && Stufeneinteilung.HatAnzahl(kriterium) => () => _ = Hauptfenster.EinteilungAendernAsync(e => e.Einstufen(kriterium, klasse - 1)),
            _ => null,
        };
        Action? runter = unten switch
        {
            Kostenkriterium u => () => _ = Hauptfenster.EinteilungAendernAsync(e => Vorbei(e, kriterium, u, +1)),
            null when klasse < Stufe.C => () => _ = Hauptfenster.EinteilungAendernAsync(e => e.Einstufen(kriterium, klasse + 1)),
            _ => null,
        };
        var bearbeitung = new Zeilenbearbeitung((x, stufe) => _ = Hauptfenster.EinteilungAendernAsync(e => e.Einstufen(x, stufe)), hoch, runter);
        return new Qualitaetszeile(k.Stufe, k.Name, kriterium, bearbeitung, kriterium == gezogen);
    }
}
