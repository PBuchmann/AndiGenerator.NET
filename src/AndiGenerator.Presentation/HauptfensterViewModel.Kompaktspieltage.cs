// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Turniere;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Kompaktspieltage (nicht im Original): Ansicht, Schalter, Punkt der Einrichtung und der Kompaktmodus des Hauptfensters
/// (Planer statt Generierung, eigene Kacheln, ohne Kosten-, Qualitäts- und Meldungsansicht).
/// </summary>
public sealed partial class HauptfensterViewModel
{
    private KompaktspieltageAnsichtViewModel? kompaktAnsicht;
    private IRelayCommand? kompaktCommand;
    private IReadOnlyList<Kompaktvariante> kompaktVarianten = [];
    private IReadOnlyList<string> kompaktPlanerhinweise = [];
    private IReadOnlyList<Planturnier> kompaktFest = [];
    private IReadOnlyList<Kompaktkachel> kompaktKacheln = [];
    private Kompaktvariante? kompaktVariante;
    private Kompaktauswertung? kompaktStand;
    private bool kompaktRechnet;
    private bool kompaktAktivBisher;

    /// <summary>Holt den Befehl „Kompaktspieltage“ (Ansicht als andockbares Dokument).</summary>
    public IRelayCommand KompaktspieltageCommand => kompaktCommand ??= new RelayCommand(KompaktspieltageOeffnen, () => ZeigtArbeitsbereich);

    /// <summary>
    /// Holt einen Wert, der angibt, ob die Ansicht „Kompaktspieltage“ angeboten wird: nur, wenn die Klasse dafür in Frage
    /// kommt (Halbrunde mit überwiegend Spieltagen am Wochenende).
    /// </summary>
    public bool ZeigtKompaktspieltage => sitzung is Plansitzung s && Kompaktplanung.InFrage(s.Staffel, s.Optionen.Rundenplanung, false);

    /// <summary>Holt einen Wert, der angibt, ob die Staffel mit Kompaktspieltagen geplant wird (Kompaktmodus).</summary>
    public bool KompaktAktiv => ZeigtKompaktspieltage && KompaktEingeschaltet;

    /// <summary>Holt einen Wert, der angibt, ob Generierung, Verfahren und ihre Ansichten angeboten werden (nicht im Kompaktmodus).</summary>
    public bool ZeigtGenerierung => !KompaktAktiv;

    /// <summary>Holt die Kacheln oben im Kompaktmodus.</summary>
    public IReadOnlyList<Kompaktkachel> Kompaktkacheln
    {
        get => kompaktKacheln;
        private set => SetProperty(ref kompaktKacheln, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob der Kompaktplaner gerade rechnet.</summary>
    public bool KompaktRechnet
    {
        get => kompaktRechnet;
        private set
        {
            if (SetProperty(ref kompaktRechnet, value))
            {
                GenerierungCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Holt, ob die geöffnete Staffel mit Kompaktspieltagen geplant wird.</summary>
    internal Kompaktmodus Kompaktmodus => sitzung?.DatenBearbeiten().Kompaktspieltage ?? Kompaktmodus.Offen;

    /// <summary>Holt einen Wert, der angibt, ob die Kompaktspieltage eingeschaltet sind (die Ansicht zeigt dann Plan und Karte).</summary>
    internal bool KompaktEingeschaltet => Kompaktmodus is Kompaktmodus.Ein or Kompaktmodus.Bestaetigt;

    /// <summary>Holt die Varianten der letzten Berechnung.</summary>
    internal IReadOnlyList<Kompaktvariante> Kompaktvarianten => kompaktVarianten;

    /// <summary>Holt die gezeigte Variante oder <c>null</c> für den Plan aus click-TT.</summary>
    internal Kompaktvariante? GewaehlteKompaktvariante => kompaktVariante;

    /// <summary>Holt die Hinweise der letzten Berechnung.</summary>
    internal IReadOnlyList<string> KompaktPlanerhinweise => kompaktPlanerhinweise;

    /// <summary>Holt einen Wert, der angibt, ob es einen Plan aus click-TT gibt.</summary>
    internal bool HatClickTtPlan => sitzung?.Staffel.BestehenderSpielplan.Count > 0;

    /// <summary>Die Auswertung des gezeigten Plans (Variante oder Plan aus click-TT).</summary>
    /// <returns>Die Auswertung oder <c>null</c> außerhalb des Kompaktmodus.</returns>
    internal Kompaktauswertung? Kompaktauswertung() => kompaktStand;

    /// <summary>Die Namen der Mannschaften der Staffel.</summary>
    /// <returns>Die Namen.</returns>
    internal List<string> KompaktMannschaften() => sitzung?.Staffel.Mannschaften.Select(m => m.Name).ToList() ?? [];

    /// <summary>Die Fahrtwünsche der Staffel.</summary>
    /// <returns>Die Wünsche.</returns>
    internal IReadOnlyList<Fahrtwunsch> Fahrtwuensche() => sitzung?.DatenBearbeiten().Fahrtwuensche() ?? [];

    /// <summary>Ob ein Spieltag festgehalten ist.</summary>
    /// <param name="spieltag">Der Spieltag.</param>
    /// <returns><c>true</c>, wenn er bei der nächsten Berechnung bleibt.</returns>
    internal bool IstFestgehalten(Kompaktspieltag spieltag) =>
        Kompaktplanung.AlsPlanturnier(spieltag) is Planturnier turnier && kompaktFest.Any(f => GleicherSpieltag(f, turnier));

    /// <summary>Schaltet die Kompaktspieltage ein oder aus und speichert das mit der Staffel.</summary>
    /// <param name="modus">Der neue Zustand.</param>
    /// <returns>Eine Aufgabe.</returns>
    internal async Task KompaktspieltageSchaltenAsync(Kompaktmodus modus)
    {
        if (sitzung is not Plansitzung s)
        {
            return;
        }

        Datenbearbeitung daten = s.DatenBearbeiten();
        daten.KompaktspieltageSetzen(modus);
        if (await UebernehmenAsync(s, daten))
        {
            KompaktAktualisieren();
        }
    }

    /// <summary>Berechnet Varianten (Startknopf im Kompaktmodus) und zeigt die ausgewogene.</summary>
    /// <returns>Eine Aufgabe.</returns>
    internal async Task KompaktPlanenAsync()
    {
        if (KompaktRechnet || sitzung is not Plansitzung s)
        {
            return;
        }

        KompaktRechnet = true;
        kompaktAnsicht?.Laden();
        Kompaktaufgabe aufgabe = Kompaktplanung.Aufgabe(s.Staffel, s.DatenBearbeiten(), s.Optionen.Rundenplanung, DateOnly.FromDateTime(DateTime.Today));
        Statuszeile = string.Create(Deutsch, $"Kompaktspieltage werden geplant (auf {Kompaktplanoptionen.Standard.Threads} Prozessorkernen) …");
        Kompaktergebnis ergebnis;
        try
        {
            ergebnis = await Task.Run(() => Kompaktplaner.Planen(aufgabe, Kompaktplanoptionen.Standard, CancellationToken.None, KompaktFortschritt));
        }
        catch (Exception fehler) when (fehler is not OperationCanceledException)
        {
            // Ein Fehler im Hintergrund darf nicht verloren gehen.
            Statuszeile = "Kompaktspieltage: Berechnung abgebrochen.";
            await oberflaeche.MeldenAsync("Kompaktspieltage", "Die Berechnung ist mit einem Fehler abgebrochen:" + Environment.NewLine + fehler.Message);
            return;
        }
        finally
        {
            KompaktRechnet = false;
        }

        if (!ReferenceEquals(s, sitzung))
        {
            return;
        }

        kompaktVarianten = ergebnis.Varianten;
        kompaktPlanerhinweise = ergebnis.Hinweise;
        Statuszeile = ergebnis.Varianten.Count == 0
            ? "Kompaktspieltage: kein vollständiger Plan gefunden – Hinweise in der Ansicht „Kompaktspieltage“."
            : string.Create(Deutsch, $"Kompaktspieltage: {ergebnis.Varianten.Count} Varianten aus {ergebnis.Geprueft:N0} Versuchen.");
        KompaktspieltageOeffnen();
        KompaktplanWaehlen(ergebnis.Varianten.Count > 0 ? ergebnis.Varianten[0] : kompaktVariante);
    }

    /// <summary>Zeigt einen Plan: eine Variante (sie wird zum aktuellen Plan für alle Ansichten, Druck und Export) oder den Plan aus click-TT.</summary>
    /// <param name="variante">Die Variante oder <c>null</c> für den Plan aus click-TT.</param>
    internal void KompaktplanWaehlen(Kompaktvariante? variante)
    {
        if (sitzung is not Plansitzung s)
        {
            return;
        }

        kompaktVariante = variante;
        if (variante is not null)
        {
            IReadOnlyList<Spiel> spiele = Kompaktplanung.Spiele(variante.Turniere, s.Staffel);
            laufenderStand = new Planstand(spiele, s.Bewerten(spiele));
            StartplaeneAufbauen();
            BefehleAktualisieren();
            AnsichtenAktualisieren(PlanQuellenArt.LaufendeGenerierung);
        }

        KompaktAktualisieren();
    }

    /// <summary>Hält einen Spieltag fest bzw. löst ihn wieder.</summary>
    /// <param name="spieltag">Der Spieltag.</param>
    /// <returns>Eine Aufgabe.</returns>
    internal Task KompaktFesthaltenAsync(Kompaktspieltag spieltag)
    {
        if (sitzung is not Plansitzung s || Kompaktplanung.AlsPlanturnier(spieltag) is not Planturnier turnier)
        {
            return Task.CompletedTask;
        }

        Datenbearbeitung daten = s.DatenBearbeiten();
        List<Planturnier> fest = daten.Festgehalten().ToList();
        if (fest.RemoveAll(f => GleicherSpieltag(f, turnier)) == 0)
        {
            fest.Add(turnier);
        }

        daten.FestgehaltenSetzen(fest);
        return KompaktDatenUebernehmenAsync(s, daten);
    }

    /// <summary>Fügt einen Fahrtwunsch hinzu (ein älterer für dieselbe Paarung wird ersetzt).</summary>
    /// <param name="wunsch">Der Wunsch.</param>
    /// <returns>Eine Aufgabe.</returns>
    internal Task FahrtwunschHinzufuegenAsync(Fahrtwunsch wunsch)
    {
        if (sitzung is not Plansitzung s)
        {
            return Task.CompletedTask;
        }

        Datenbearbeitung daten = s.DatenBearbeiten();
        List<Fahrtwunsch> wuensche = daten.Fahrtwuensche().Where(w => !GleichePaarung(w, wunsch)).ToList();
        wuensche.Add(wunsch);
        daten.FahrtwuenscheSetzen(wuensche);
        return KompaktDatenUebernehmenAsync(s, daten);
    }

    /// <summary>Entfernt einen Fahrtwunsch.</summary>
    /// <param name="wunsch">Der Wunsch.</param>
    /// <returns>Eine Aufgabe.</returns>
    internal Task FahrtwunschEntfernenAsync(Fahrtwunsch wunsch)
    {
        if (sitzung is not Plansitzung s)
        {
            return Task.CompletedTask;
        }

        Datenbearbeitung daten = s.DatenBearbeiten();
        daten.FahrtwuenscheSetzen(daten.Fahrtwuensche().Where(w => w != wunsch));
        return KompaktDatenUebernehmenAsync(s, daten);
    }

    private static bool GleicherSpieltag(Planturnier a, Planturnier b) =>
        a.Tag == b.Tag && a.Form == b.Form && a.Ausrichter == b.Ausrichter
        && a.Mannschaften.Order(StringComparer.Ordinal).SequenceEqual(b.Mannschaften.Order(StringComparer.Ordinal));

    private static bool GleichePaarung(Fahrtwunsch a, Fahrtwunsch b) =>
        (a.Gast == b.Gast && a.Gastgeber == b.Gastgeber) || (a.Gast == b.Gastgeber && a.Gastgeber == b.Gast);

    private static string KmText(double km) => km.ToString("N0", Deutsch) + " km";

    /// <summary>Die vier Kacheln: Fahrten, Meldungen, Heimspieltage, Spieltage.</summary>
    private static List<Kompaktkachel> KachelnBilden(Kompaktauswertung a, Kompaktvariante? variante)
    {
        string plan = variante?.Name ?? "Plan aus click-TT";
        if (a.Spieltage.Count == 0)
        {
            const string Leer = "Noch kein Plan – ▶ berechnet Varianten";
            return
            [
                new Kompaktkachel("Fahrten (Luftlinie)", "–", Leer, false, false),
                new Kompaktkachel("Meldungen", "–", Leer, false, false),
                new Kompaktkachel("Heimspieltage", "–", Leer, false, false),
                new Kompaktkachel("Spieltage", "–", Leer, false, false),
            ];
        }

        Kompaktmannschaft? weiteste = a.Mannschaften.MaxBy(m => m.Km);
        int wenigste = a.Mannschaften.Min(m => m.Ausgerichtet);
        int meiste = a.Mannschaften.Max(m => m.Ausgerichtet);
        string heim = wenigste == meiste ? wenigste.ToString(Deutsch) : string.Create(Deutsch, $"{wenigste}–{meiste}");
        string ohne = a.OhneHeimspieltag == 0 ? "jede richtet aus" : string.Create(Deutsch, $"{a.OhneHeimspieltag} ohne Heimspieltag");
        return
        [
            new Kompaktkachel("Fahrten (Luftlinie)", KmText(a.KmGesamt), weiteste is null ? plan : $"weiteste: {weiteste.Kurzname} {KmText(weiteste.Km)}", false, false),
            new Kompaktkachel("Meldungen", a.Verstoesse == 0 ? "eingehalten" : string.Create(Deutsch, $"{a.Verstoesse} Verstöße"), plan, a.Verstoesse == 0, a.Verstoesse > 0),
            new Kompaktkachel("Heimspieltage je Mannschaft", heim, ohne, false, a.OhneHeimspieltag > 0),
            new Kompaktkachel(
                "Spieltage",
                a.Formen,
                string.Create(Deutsch, $"Stand höchstens {a.GroessterAbstand} Spiele auseinander · bis zu {a.MeisteAmTag} von {a.Mannschaften.Count} an einem Tag"),
                false,
                false),
        ];
    }

    /// <summary>
    /// Punkt „Klasse mit Kompaktspieltagen“ der Einrichtung: nur bei einer Halbrunde („(HR)“ bzw. „(FR)“ im Namen, eingestellt
    /// oder vorgeschlagen) mit überwiegend Spieltagen am Wochenende; noch nicht gefragt → einschalten oder normal planen,
    /// eingeschaltet (noch nicht bestätigt) → beibehalten oder zurücknehmen.
    /// </summary>
    private static Einrichtungsschritt? KompaktschrittErstellen(Plansitzung neu, int nummer, Rundenvorschlag vorschlag)
    {
        Kompaktmodus modus = neu.DatenBearbeiten().Kompaktspieltage;
        bool eingeschaltet = modus == Kompaktmodus.Ein;
        if (modus is Kompaktmodus.Bestaetigt or Kompaktmodus.Aus
            || (!eingeschaltet && !Kompaktplanung.InFrage(neu.Staffel, neu.Optionen.Rundenplanung, vorschlag == Rundenvorschlag.Halbrunde)))
        {
            return null;
        }

        IReadOnlyList<string> details =
        [
            "Optimiert wird nach Luftlinie zwischen den Hallen (Adressen aus den Terminwünschen).",
            "Gilt nur für eine Halbrunde. Lässt sich jederzeit in der Ansicht „Kompaktspieltage“ wieder ausschalten.",
        ];
        string text = eingeschaltet
            ? "Diese Staffel wird mit Kompaktspieltagen geplant. Falls das beim letzten Mal versehentlich eingeschaltet wurde, lässt es sich hier zurücknehmen. "
                + KompaktspieltageAnsichtViewModel.Erklaerung
            : KompaktspieltageAnsichtViewModel.Erklaerung;
        IReadOnlyList<Einrichtungsoption> optionen = eingeschaltet
            ? [new Einrichtungsoption("Kompaktspieltage beibehalten", "wird danach nicht mehr gefragt"), new Einrichtungsoption("Kompaktspieltage ausschalten", "normale Planung")]
            : [new Einrichtungsoption("Mit Kompaktspieltagen planen", "3er- und 4er-Spieltage, Karte und Prüfung"), new Einrichtungsoption("Normal planen", "jedes Spiel einzeln")];
        return new Einrichtungsschritt(nummer, "Klasse mit Kompaktspieltagen", text, details, optionen);
    }

    /// <summary>Nach Öffnen, Schließen oder Änderungen: Auswertung, Kacheln, Ansicht und Navigation aktualisieren; beim Wechsel in den oder aus dem Kompaktmodus die Ansichten umstellen.</summary>
    private void KompaktAktualisieren()
    {
        KompaktStandBerechnen();
        kompaktAnsicht?.Laden();
        OnPropertyChanged(nameof(ZeigtKompaktspieltage));
        OnPropertyChanged(nameof(KompaktAktiv));
        OnPropertyChanged(nameof(ZeigtGenerierung));
        OnPropertyChanged(nameof(GenerierungText));
        KompaktspieltageCommand.NotifyCanExecuteChanged();
        bool aktiv = KompaktAktiv;
        if (aktiv != kompaktAktivBisher)
        {
            kompaktAktivBisher = aktiv;
            AnsichtenFuerModus(aktiv);
        }
    }

    private void KompaktStandBerechnen()
    {
        if (sitzung is not Plansitzung s || !KompaktEingeschaltet)
        {
            kompaktStand = null;
            kompaktFest = [];
            Kompaktkacheln = [];
            return;
        }

        Datenbearbeitung daten = s.DatenBearbeiten();
        DateOnly heute = DateOnly.FromDateTime(DateTime.Today);
        kompaktFest = daten.Festgehalten();
        kompaktStand = kompaktVariante is Kompaktvariante v
            ? Kompaktplanung.Auswerten(s.Staffel, daten, s.Optionen.Rundenplanung, heute, v.Turniere)
            : Kompaktplanung.Auswerten(s.Staffel, daten, s.Optionen.Rundenplanung, heute);
        Kompaktkacheln = KachelnBilden(kompaktStand, kompaktVariante);
    }

    /// <summary>Beim Öffnen und Schließen einer Staffel: Varianten verwerfen.</summary>
    private void KompaktZuruecksetzen()
    {
        kompaktVarianten = [];
        kompaktPlanerhinweise = [];
        kompaktVariante = null;
    }

    /// <summary>Kompaktmodus: die Ansicht „Kompaktspieltage“ ist die Hauptansicht, Kosten, Qualität und Meldungen entfallen; sonst wieder die Kosten.</summary>
    private void AnsichtenFuerModus(bool aktiv)
    {
        if (aktiv)
        {
            KompaktspieltageOeffnen();
            foreach (AnsichtViewModel ansicht in ansichten.Where(a => a is KostenAnsichtViewModel or QualitaetAnsichtViewModel or MeldungenAnsichtViewModel).ToList())
            {
                fabrik.CloseDockable(ansicht);
            }
        }
        else if (sitzung is not null && !ansichten.OfType<KostenAnsichtViewModel>().Any())
        {
            AnsichtOeffnen(new KostenAnsichtViewModel(this, NaechsteId()));
        }
    }

    private void KompaktFortschritt(string stand) =>
        oberflaeche.AufOberflaeche(() =>
        {
            Statuszeile = "Kompaktspieltage werden geplant – " + stand;
        });

    private async Task KompaktDatenUebernehmenAsync(Plansitzung s, Datenbearbeitung daten)
    {
        if (await UebernehmenAsync(s, daten))
        {
            KompaktAktualisieren();
        }
    }

    private void KompaktspieltageOeffnen()
    {
        if (kompaktAnsicht is null)
        {
            kompaktAnsicht = new KompaktspieltageAnsichtViewModel(this);
            fabrik.AnsichtHinzufuegen(kompaktAnsicht);
        }
        else
        {
            fabrik.SetActiveDockable(kompaktAnsicht);
        }
    }

    /// <summary>Übernimmt die Wahl im Punkt „Klasse mit Kompaktspieltagen“.</summary>
    private async Task<bool> KompaktschrittUebernehmenAsync(Plansitzung s, Einrichtungsschritt schritt)
    {
        if (!ReferenceEquals(s, sitzung))
        {
            return false;
        }

        Kompaktmodus bisher = s.DatenBearbeiten().Kompaktspieltage;
        Kompaktmodus neu = (bisher, schritt.Auswahl) switch
        {
            (Kompaktmodus.Ein, 0) => Kompaktmodus.Bestaetigt,
            (_, 0) => Kompaktmodus.Ein,
            _ => Kompaktmodus.Aus,
        };
        await KompaktspieltageSchaltenAsync(neu);
        if (neu != Kompaktmodus.Aus)
        {
            KompaktspieltageOeffnen();
        }

        return true;
    }
}
