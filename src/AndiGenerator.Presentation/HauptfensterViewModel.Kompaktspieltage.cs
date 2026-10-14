// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
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
    private bool kompaktMeldungenBearbeiten;
    private Datenbearbeitung? kompaktLesekopie;
    private IRelayCommand? kompaktCommand;
    private IReadOnlyList<Kompaktvariante> kompaktVarianten = [];
    private Dictionary<Kompaktvariante, double> kompaktPunkte = [];
    private double? kompaktClickTtPunkte;
    private IReadOnlyList<string> kompaktPlanerhinweise = [];
    private IReadOnlyList<Kompaktkachel> kompaktKacheln = [];
    private Kompaktvariante? kompaktVariante;
    private Kompaktauswertung? kompaktStand;
    private bool kompaktRechnet;
    private bool kompaktPausiert;
    private Kompaktsteuerung? kompaktSteuerung;
    private Kompaktvariante? kompaktBehalten;
    private bool kompaktAktivBisher;

    /// <summary>Holt den Befehl „Kompaktspieltage“ (Ansicht als andockbares Dokument).</summary>
    public IRelayCommand KompaktspieltageCommand => kompaktCommand ??= new RelayCommand(KompaktspieltageOeffnen, () => ZeigtArbeitsbereich);

    /// <summary>
    /// Holt einen Wert, der angibt, ob die Ansicht „Kompaktspieltage“ angeboten wird: nur, wenn die Klasse dafür in Frage
    /// kommt (Halbrunde mit überwiegend Spieltagen am Wochenende) oder wenn die Kompaktspieltage schon eingeschaltet sind.
    /// </summary>
    public bool ZeigtKompaktspieltage => sitzung is Plansitzung s && (Kompaktplanung.InFrage(s.Staffel, s.Optionen.Rundenplanung, false) || KompaktEingeschaltet);

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

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob die Meldungen im Raster gerade bearbeitet werden; solange lässt sich keine
    /// Berechnung starten (Peter, 13.10.2026).
    /// </summary>
    public bool KompaktMeldungenBearbeiten
    {
        get => kompaktMeldungenBearbeiten;
        set
        {
            if (kompaktMeldungenBearbeiten != value)
            {
                kompaktMeldungenBearbeiten = value;
                GenerierungCommand.NotifyCanExecuteChanged();
            }
        }
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
                StoppenCommand.NotifyCanExecuteChanged();
                GenerierungGeaendert();
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob die Planung der Kompaktspieltage angehalten ist (dann zeigt die Ansicht die bisher besten Pläne).</summary>
    public bool KompaktPausiert
    {
        get => kompaktPausiert;
        private set
        {
            if (SetProperty(ref kompaktPausiert, value))
            {
                GenerierungGeaendert();
            }
        }
    }

    /// <summary>Holt, ob die geöffnete Staffel mit Kompaktspieltagen geplant wird.</summary>
    internal Kompaktmodus Kompaktmodus => sitzung?.DatenBearbeiten().Kompaktspieltage ?? Kompaktmodus.Offen;

    /// <summary>Holt einen Wert, der angibt, ob die Kompaktspieltage eingeschaltet sind (die Ansicht zeigt dann Plan und Karte).</summary>
    internal bool KompaktEingeschaltet => Kompaktmodus is Kompaktmodus.Ein or Kompaktmodus.Bestaetigt;

    /// <summary>Holt die Varianten der letzten Berechnung.</summary>
    internal IReadOnlyList<Kompaktvariante> Kompaktvarianten => kompaktVarianten;

    /// <summary>Holt die Punkte des Plans aus click-TT (wie beim besten Gesamtpaket) oder <c>null</c>, wenn nicht berechnet.</summary>
    internal double? KompaktClickTtPunkte => kompaktClickTtPunkte;

    /// <summary>Holt die gezeigte Variante oder <c>null</c> für den Plan aus click-TT.</summary>
    internal Kompaktvariante? GewaehlteKompaktvariante => kompaktVariante;

    /// <summary>Holt die Hinweise der letzten Berechnung.</summary>
    internal IReadOnlyList<string> KompaktPlanerhinweise => kompaktPlanerhinweise;

    /// <summary>Holt einen Wert, der angibt, ob es einen Plan aus click-TT gibt.</summary>
    internal bool HatClickTtPlan => sitzung?.Staffel.BestehenderSpielplan.Count > 0;

    /// <summary>Die Punkte einer Variante (wie beim besten Gesamtpaket, kleiner ist besser).</summary>
    /// <param name="variante">Die Variante.</param>
    /// <returns>Die Punkte oder <c>null</c>, wenn nicht berechnet.</returns>
    internal double? KompaktPunkte(Kompaktvariante variante) => kompaktPunkte.TryGetValue(variante, out double p) ? p : null;

    /// <summary>Die Auswertung des gezeigten Plans (Variante oder Plan aus click-TT).</summary>
    /// <returns>Die Auswertung oder <c>null</c> außerhalb des Kompaktmodus.</returns>
    internal Kompaktauswertung? Kompaktauswertung() => kompaktStand;

    /// <summary>Die Namen der Mannschaften der Staffel.</summary>
    /// <returns>Die Namen.</returns>
    internal List<string> KompaktMannschaften() => sitzung?.Staffel.Mannschaften.Select(m => m.Name).ToList() ?? [];

    /// <summary>Die vier stärksten und die vier schwächsten Mannschaften nach QTTR-Schnitt (leer, wenn nicht bekannt).</summary>
    /// <returns>Spitze und Keller.</returns>
    internal (IReadOnlyList<string> Spitze, IReadOnlyList<string> Keller) KompaktGruppen()
    {
        if (sitzung is not Plansitzung s)
        {
            return ([], []);
        }

        (IReadOnlyList<string> spitze, IReadOnlyList<string> keller, _) =
            Kompaktplanung.Staerkegruppen(s.Staffel.Mannschaften.Select(m => m.Name).ToList(), s.DatenBearbeiten().Mannschaftsstaerken());
        return (spitze, keller);
    }

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
            KompaktGespeichertAufbauen();
            KompaktAktualisieren();
        }
    }

    /// <summary>Ändert die Meldungen (Raster im Bearbeitungsmodus), speichert sie wie die Spielplandaten und zeigt sie neu.</summary>
    /// <param name="aenderung">Die Änderung an der Arbeitskopie.</param>
    /// <returns>Eine Aufgabe.</returns>
    internal async Task KompaktMeldungAendernAsync(Action<Datenbearbeitung> aenderung)
    {
        if (sitzung is not Plansitzung s)
        {
            return;
        }

        Datenbearbeitung daten = s.DatenBearbeiten();
        try
        {
            aenderung(daten);
        }
        catch (ArgumentException fehler)
        {
            await oberflaeche.MeldenAsync("Meldungen", fehler.Message);
            return;
        }

        if (await UebernehmenAsync(s, daten))
        {
            KompaktAktualisieren();
        }
    }

    /// <summary>Liest etwas aus den Spielplandaten (z. B. den Heimtermin unter dem Rahmen des Rasters).</summary>
    /// <typeparam name="T">Art des Ergebnisses.</typeparam>
    /// <param name="lesen">Was gelesen wird.</param>
    /// <param name="ersatz">Ergebnis ohne geöffnete Staffel.</param>
    /// <returns>Das Ergebnis.</returns>
    internal T KompaktDatenLesen<T>(Func<Datenbearbeitung, T> lesen, T ersatz)
    {
        if (sitzung is not Plansitzung s)
        {
            return ersatz;
        }

        // Eine Lesekopie für alle Bewegungen des Rahmens; sie wird bei jeder Änderung neu angelegt.
        kompaktLesekopie ??= s.DatenBearbeiten();
        return lesen(kompaktLesekopie);
    }

    /// <summary>Verwirft die Lesekopie der Spielplandaten (nach Änderungen von außen).</summary>
    internal void KompaktLesekopieVerwerfen() => kompaktLesekopie = null;

    /// <summary>Berechnet Varianten (Startknopf im Kompaktmodus) und zeigt die ausgewogene.</summary>
    /// <returns>Eine Aufgabe.</returns>
    internal async Task KompaktPlanenAsync()
    {
        if (KompaktRechnet || sitzung is not Plansitzung s)
        {
            return;
        }

        using var steuerung = new Kompaktsteuerung();
        kompaktSteuerung = steuerung;
        kompaktBehalten = null;
        KompaktRechnet = true;
        kompaktAnsicht?.FortschrittZeigen(0, "Wird vorbereitet …");
        kompaktAnsicht?.Laden();
        Kompaktaufgabe aufgabe = Kompaktplanung.Aufgabe(s.Staffel, s.DatenBearbeiten(), s.Optionen.Rundenplanung, DateOnly.FromDateTime(DateTime.Today));
        Kompaktplanoptionen optionen = Kompaktplanung.Optionen();
        Statuszeile = string.Create(Deutsch, $"Kompaktspieltage werden geplant (auf {optionen.Threads} Prozessorkernen) …");
        Kompaktergebnis ergebnis;
        try
        {
            // Beenden läuft über die Steuerung: der Planer liefert dann noch die bis dahin besten Pläne.
            ergebnis = await Task.Run(() => Kompaktplaner.Planen(aufgabe, optionen, steuerung, KompaktFortschritt), CancellationToken.None);
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
            kompaktSteuerung = null;
            KompaktPausiert = false;
            KompaktRechnet = false;
        }

        if (!ReferenceEquals(s, sitzung))
        {
            return;
        }

        KompaktErgebnisZeigen(s, ergebnis);
        kompaktBehalten = null;
        Statuszeile = ergebnis.Varianten.Count == 0
            ? "Kompaktspieltage: kein vollständiger Plan gefunden – Hinweise in der Ansicht „Kompaktspieltage“."
            : string.Create(Deutsch, $"Kompaktspieltage: {ergebnis.Varianten.Count} Varianten aus {ergebnis.Geprueft:N0} Versuchen.");
    }

    /// <summary>
    /// Hält die laufende Planung an und zeigt die bisher besten Pläne (sie werden ersetzt, wenn die Planung später
    /// bessere findet) bzw. setzt sie fort.
    /// </summary>
    /// <returns>Eine Aufgabe.</returns>
    internal async Task KompaktPauseUmschaltenAsync()
    {
        if (kompaktSteuerung is not Kompaktsteuerung steuerung || sitzung is not Plansitzung s)
        {
            return;
        }

        if (KompaktPausiert)
        {
            // Von den Plänen der Pause bleibt nur der gewählte; er steht am Ende neben den neuen.
            kompaktBehalten = kompaktVariante;
            kompaktVarianten = kompaktBehalten is null ? [] : [kompaktBehalten];
            kompaktPunkte = kompaktPunkte.Where(p => ReferenceEquals(p.Key, kompaktBehalten)).ToDictionary();
            KompaktPausiert = false;
            steuerung.Fortsetzen();
            KompaktAktualisieren();
            Statuszeile = "Kompaktspieltage werden weiter geplant …";
            return;
        }

        steuerung.Pausieren();
        KompaktPausiert = true;
        Statuszeile = "Kompaktspieltage: angehalten – gezeigt werden die bisher besten Pläne.";
        Kompaktergebnis? zwischenstand = await Task.Run(() => steuerung.Zwischenstand(), CancellationToken.None);
        if (zwischenstand is not null && KompaktPausiert && ReferenceEquals(s, sitzung))
        {
            KompaktErgebnisZeigen(s, zwischenstand);
            kompaktAnsicht?.PauseZeigen("Angehalten – die bisher besten Pläne stehen zur Auswahl. Weiterrechnen behält nur den gewählten.");
        }
    }

    /// <summary>Beendet die laufende Planung; sie liefert die bis dahin besten Pläne.</summary>
    internal void KompaktStoppen() => kompaktSteuerung?.Stoppen();

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
            // Gespeicherte Pläne mit genau ihren Spielen (Uhrzeiten, Hallen, Heimrecht), Varianten aus ihren Spieltagen.
            if (GespeicherterStand(s, variante) is Planstand gespeichert)
            {
                laufenderStand = gespeichert;
            }
            else
            {
                IReadOnlyList<Spiel> spiele = Kompaktplanung.Spiele(variante.Turniere, s.Staffel);
                laufenderStand = new Planstand(spiele, s.Bewerten(spiele));
            }

            StartplaeneAufbauen();
            BefehleAktualisieren();
            AnsichtenAktualisieren(PlanQuellenArt.LaufendeGenerierung);
        }

        KompaktAktualisieren();
    }

    /// <summary>Ein Plan als Text (Tag, Form, Mannschaften je Spieltag) zum Erkennen gleicher Pläne.</summary>
    private static string Planschluessel(Kompaktvariante v) =>
        string.Join(';', v.Turniere.Select(t => string.Create(CultureInfo.InvariantCulture, $"{t.Tag:yyyy-MM-dd}|{t.Form}|{string.Join(',', t.Mannschaften)}")));

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
        return
        [
            new Kompaktkachel("Fahrten (Luftlinie)", KmText(a.KmGesamt), weiteste is null ? plan : $"weiteste: {weiteste.Name} {KmText(weiteste.Km)} · Unterschied {KmText(a.Mannschaften.Max(m => m.Km) - a.Mannschaften.Min(m => m.Km))}", false, false),
            new Kompaktkachel("Meldungen", a.Verstoesse == 0 ? "eingehalten" : string.Create(Deutsch, $"{a.Verstoesse} Verstöße"), plan, a.Verstoesse == 0, a.Verstoesse > 0),
            new Kompaktkachel("Heimspieltage je Mannschaft", heim, Kompakterlaeuterung.Heimspieltage(a), false, a.OhneHeimspieltag > 0),
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
        kompaktLesekopie = null;
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

    /// <summary>Übernimmt Varianten und Hinweise (am Ende oder in der Pause) und zeigt den Plan mit den wenigsten Punkten.</summary>
    private void KompaktErgebnisZeigen(Plansitzung s, Kompaktergebnis ergebnis)
    {
        List<Kompaktvariante> varianten = [.. ergebnis.Varianten];
        if (kompaktBehalten is Kompaktvariante behalten && !varianten.Exists(v => Planschluessel(v) == Planschluessel(behalten)))
        {
            const string Zusatz = " (aus der Pause)";
            varianten.Add(behalten.Name.EndsWith(Zusatz, StringComparison.Ordinal) ? behalten : behalten with { Name = behalten.Name + Zusatz });
        }

        kompaktVarianten = NachPunkten(s, varianten);
        kompaktPlanerhinweise = ergebnis.Hinweise;
        KompaktspieltageOeffnen();
        KompaktplanWaehlen(kompaktVarianten.Count > 0 ? kompaktVarianten[0] : kompaktVariante);
    }

    /// <summary>
    /// Die Varianten aufsteigend nach Punkten (wie in der Erläuterung, bei Gleichstand in der Reihenfolge des Planers);
    /// merkt sich die Punkte, auch die des Plans aus click-TT.
    /// </summary>
    private List<Kompaktvariante> NachPunkten(Plansitzung s, IReadOnlyList<Kompaktvariante> varianten)
    {
        kompaktPunkte = [];
        kompaktClickTtPunkte = null;
        if (Kompaktpunkte.Bezug(varianten) is not Kompaktbezug bezug)
        {
            return varianten.ToList();
        }

        Datenbearbeitung daten = s.DatenBearbeiten();
        DateOnly heute = DateOnly.FromDateTime(DateTime.Today);
        (IReadOnlyList<string> spitze, IReadOnlyList<string> keller) = KompaktGruppen();
        IReadOnlyList<IReadOnlyList<string>> gruppen = spitze.Count >= 3 && spitze.Count == keller.Count ? [spitze, keller] : [];
        foreach (Kompaktvariante v in varianten)
        {
            Kompaktauswertung a = Kompaktplanung.Auswerten(s.Staffel, daten, s.Optionen.Rundenplanung, heute, v.Turniere);
            kompaktPunkte[v] = Kompaktpunkte.Berechnen(a, bezug, gruppen).Summe;
        }

        if (HatClickTtPlan)
        {
            Kompaktauswertung clickTt = Kompaktplanung.Auswerten(s.Staffel, daten, s.Optionen.Rundenplanung, heute);
            kompaktClickTtPunkte = clickTt.Spieltage.Count == 0 ? null : Kompaktpunkte.Berechnen(clickTt, bezug, gruppen).Summe;
        }

        GespeichertePunkte(bezug);
        return varianten.OrderBy(v => kompaktPunkte[v]).ToList();
    }

    /// <summary>Vergleich für „Plan sichern“ im Kompaktmodus: beide Pläne nach den Kennzahlen der Kompaktspieltage.</summary>
    private Planvergleich KompaktVergleich(Plansitzung s, string anlass, IReadOnlyList<Spiel> aktuell, IReadOnlyList<Spiel> gesichert, DateTime gesichertAm)
    {
        Datenbearbeitung daten = s.DatenBearbeiten();
        DateOnly heute = DateOnly.FromDateTime(DateTime.Today);
        Kompaktauswertung neu = Kompaktplanung.SpieleAuswerten(s.Staffel, daten, s.Optionen.Rundenplanung, heute, aktuell);
        Kompaktauswertung alt = Kompaktplanung.SpieleAuswerten(s.Staffel, daten, s.Optionen.Rundenplanung, heute, gesichert);
        (IReadOnlyList<string> spitze, IReadOnlyList<string> keller) = KompaktGruppen();

        // Gleicher Bezug wie in der Erläuterung der Pläne: kürzester 4er-Plan und kleinster Unterschied der Varianten.
        return Planvergleich.Kompakt(anlass, neu, alt, [spitze, keller], Kompaktpunkte.Bezug(kompaktVarianten), gesichertAm);
    }

    private void KompaktStandBerechnen()
    {
        if (sitzung is not Plansitzung s || !KompaktEingeschaltet)
        {
            kompaktStand = null;
            Kompaktkacheln = [];
            return;
        }

        Datenbearbeitung daten = s.DatenBearbeiten();
        DateOnly heute = DateOnly.FromDateTime(DateTime.Today);
        if (GespeicherterStand(s, kompaktVariante) is Planstand gespeichert)
        {
            kompaktStand = Kompaktplanung.SpieleAuswerten(s.Staffel, daten, s.Optionen.Rundenplanung, heute, gespeichert.Spiele);
        }
        else
        {
            kompaktStand = kompaktVariante is Kompaktvariante v
                ? Kompaktplanung.Auswerten(s.Staffel, daten, s.Optionen.Rundenplanung, heute, v.Turniere)
                : Kompaktplanung.Auswerten(s.Staffel, daten, s.Optionen.Rundenplanung, heute);
        }

        Kompaktkacheln = KachelnBilden(kompaktStand, kompaktVariante);
    }

    /// <summary>Beim Öffnen und Schließen einer Staffel: Varianten verwerfen.</summary>
    private void KompaktZuruecksetzen()
    {
        kompaktVarianten = [];
        kompaktGespeichert = [];
        kompaktGespeichertJe = [];
        kompaktPunkte = [];
        kompaktClickTtPunkte = null;
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

    private void KompaktFortschritt(Kompaktfortschritt stand) =>
        oberflaeche.AufOberflaeche(() =>
        {
            Statuszeile = "Kompaktspieltage werden geplant – " + stand.Text;
            kompaktAnsicht?.FortschrittZeigen(stand.Anteil * 100, stand.Text);
        });

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
