// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Xml;
using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Persistence.ClickTt;
using AndiGenerator.Persistence.Gemeinsam;
using AndiGenerator.Persistence.Plandaten;
using AndiGenerator.Persistence.Tabellen;
using AndiGenerator.Rendering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace AndiGenerator.Presentation;

/// <summary>
/// Hauptfenster: geöffnete Staffel, Steuerung der Generierung, Statuszeile und die Ansichten im Andock-Layout
/// (MIGRATIONSPLAN E15). Mehrere Ansichten können gleichzeitig sichtbar sein, jede mit eigener Planquelle.
/// </summary>
public sealed class HauptfensterViewModel : ObservableObject, IDisposable
{
    /// <summary>Dateiname der Anleitung neben dem Programm.</summary>
    public const string Anleitungsdatei = "Anleitung.pdf";

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private static readonly Dateifilter PlanDateien = new("click-TT- und Plandateien", ["*.xml"]);

    private readonly IOberflaeche oberflaeche;
    private readonly Optimierungsdienst dienst;
    private readonly DockFabrik fabrik;
    private readonly List<AnsichtViewModel> ansichten = [];
    private readonly Dictionary<string, Planstand?> gemerkteStaende = new(StringComparer.Ordinal);
    private readonly Timer statusTakt;
    private readonly string eigeneBasis;
    private readonly string? uebernahmeAus;
    private Plansitzung? sitzung;
    private Planstand? laufenderStand;
    private Planstand? clickTtStand;
    private IReadOnlyList<PlanQuelle> planquellen = [PlanQuelle.Laufend];
    private IReadOnlyList<Startplan> startplaene = [Startplan.Leer];
    private Startplan startplan = Startplan.Leer;
    private OptionenAnsichtViewModel? optionenAnsicht;
    private TerminwunschAnsichtViewModel? terminwunschAnsicht;
    private NachbarterminAnsichtViewModel? nachbarterminAnsicht;
    private string titel = UeberViewModel.Programm;
    private string statuszeile = "Bitte eine click-TT-Datei oder Plandatei öffnen.";
    private bool laeuft;
    private bool pausiert;
    private int naechsteAnsicht = 1;
    private string staffelname = string.Empty;
    private string staffelinfo = string.Empty;
    private IReadOnlyList<Zuletztzeile> zuletzt = [];
    private string aktiveAnsicht = string.Empty;
    private int startzoom = 100;
    private EinrichtungViewModel? einrichtung;

    /// <summary>Initialisiert das Hauptfenster mit einer Kosten- und einer Terminplanansicht.</summary>
    /// <param name="oberflaeche">Dienste der Oberfläche.</param>
    /// <param name="fensterErzeugen">Erzeugt Fenster für herausgelöste Ansichten.</param>
    public HauptfensterViewModel(IOberflaeche oberflaeche, Func<IHostWindow> fensterErzeugen)
        : this(oberflaeche, fensterErzeugen, Plansitzung.EigeneBasisVorbereiten(), Plansitzung.OriginalBasis())
    {
    }

    /// <summary>Initialisiert das Hauptfenster mit eigenen Ablageorten (für Tests: nichts unter <c>%LOCALAPPDATA%</c>).</summary>
    /// <param name="oberflaeche">Dienste der Oberfläche.</param>
    /// <param name="fensterErzeugen">Erzeugt Fenster für herausgelöste Ansichten.</param>
    /// <param name="eigeneBasis">Basis der Staffelordner (Optionen, gemerkte Pläne).</param>
    /// <param name="uebernahmeAus">Basis des Originals, aus der beim ersten Öffnen kopiert wird, oder <c>null</c>.</param>
    public HauptfensterViewModel(IOberflaeche oberflaeche, Func<IHostWindow> fensterErzeugen, string eigeneBasis, string? uebernahmeAus)
    {
        ArgumentNullException.ThrowIfNull(oberflaeche);
        ArgumentException.ThrowIfNullOrEmpty(eigeneBasis);
        this.oberflaeche = oberflaeche;
        this.eigeneBasis = eigeneBasis;
        this.uebernahmeAus = uebernahmeAus;
        dienst = new Optimierungsdienst();
        dienst.Verbessert += BeiVerbesserung;

        // Zu Beginn nur die Kostenansicht; weitere Ansichten erst, wenn sie links angeklickt werden.
        ansichten.Add(new KostenAnsichtViewModel(this, NaechsteId()));
        fabrik = new DockFabrik(fensterErzeugen, ansichten.ToList());
        fabrik.ActiveDockableChanged += (_, e) =>
        {
            if (Ansichtsart(e.Dockable) is string art)
            {
                AktiveAnsicht = art;
            }
        };
        fabrik.DockableClosed += (_, e) =>
        {
            ansichten.RemoveAll(a => ReferenceEquals(a, e.Dockable));
            if (ReferenceEquals(e.Dockable, optionenAnsicht))
            {
                optionenAnsicht = null;
            }

            if (ReferenceEquals(e.Dockable, terminwunschAnsicht))
            {
                terminwunschAnsicht = null;
            }

            if (ReferenceEquals(e.Dockable, nachbarterminAnsicht))
            {
                nachbarterminAnsicht = null;
            }
        };
        IRootDock layout = fabrik.CreateLayout();
        fabrik.InitLayout(layout);
        Layout = layout;
        AlleAnsichtenAktualisieren();

        OeffnenCommand = new AsyncRelayCommand(OeffnenAsync);
        NeuCommand = new AsyncRelayCommand(NeueDateiAsync);
        SchliessenCommand = new AsyncRelayCommand(SchliessenAsync, () => IstGeoeffnet);
        GenerierungCommand = new RelayCommand(GenerierungUmschalten, () => IstGeoeffnet);
        ZuletztOeffnenCommand = new AsyncRelayCommand<string>(ZuletztOeffnenAsync);
        ZuletztEntfernenCommand = new RelayCommand<string>(ZuletztEntfernen);
        ZoomGroesserCommand = new RelayCommand(() => ZoombareAnsicht()?.Zoom.Groesser());
        ZoomKleinerCommand = new RelayCommand(() => ZoombareAnsicht()?.Zoom.Kleiner());
        ZoomZuruecksetzenCommand = new RelayCommand(() => ZoombareAnsicht()?.Zoom.Zuruecksetzen());
        StartenCommand = new RelayCommand(Starten, () => IstGeoeffnet);
        PauseCommand = new RelayCommand(PauseUmschalten, () => Laeuft);
        StoppenCommand = new RelayCommand(Stoppen, () => Laeuft);
        PlanMerkenCommand = new AsyncRelayCommand(PlanMerkenAsync, () => laufenderStand is not null);
        CsvExportierenCommand = new AsyncRelayCommand(CsvExportierenAsync, () => laufenderStand is not null);
        DruckenCommand = new AsyncRelayCommand(DruckenAsync, () => ZeigtArbeitsbereich);
        AnleitungCommand = new AsyncRelayCommand(AnleitungAsync);
        UeberCommand = new AsyncRelayCommand(() => oberflaeche.UeberAnzeigenAsync(new UeberViewModel(oberflaeche, UeberViewModel.VersionVon(typeof(HauptfensterViewModel).Assembly), this.eigeneBasis)));
        KostenansichtCommand = new RelayCommand(() => AnsichtZeigen(() => new KostenAnsichtViewModel(this, NaechsteId())), () => HatPlan && ZeigtArbeitsbereich);
        TerminplanansichtCommand = new RelayCommand(() => AnsichtZeigen(() => new TerminplanAnsichtViewModel(this, NaechsteId())), () => HatPlan && ZeigtArbeitsbereich);
        QualitaetsansichtCommand = new RelayCommand(() => AnsichtZeigen(() => new QualitaetAnsichtViewModel(this, NaechsteId())), () => HatPlan && ZeigtArbeitsbereich);
        MeldungsansichtCommand = new RelayCommand(() => AnsichtZeigen(() => new MeldungenAnsichtViewModel(this, NaechsteId())), () => HatPlan && ZeigtArbeitsbereich);
        DiagrammansichtCommand = new RelayCommand(() => AnsichtZeigen(() => new DiagrammAnsichtViewModel(this, NaechsteId())), () => HatPlan && ZeigtArbeitsbereich);
        EinstellungenCommand = new RelayCommand(EinstellungenOeffnen, () => ZeigtArbeitsbereich);
        DatenCommand = new AsyncRelayCommand(DatenBearbeitenAsync, () => ZeigtArbeitsbereich);
        TerminwunschansichtCommand = new RelayCommand(TerminwuenscheOeffnen, () => ZeigtArbeitsbereich);
        NachbarterminansichtCommand = new RelayCommand(NachbartermineOeffnen, () => ZeigtArbeitsbereich);

        Zuletzt = Zeilen(Zuletztlesen());
        statusTakt = new Timer(_ => StatusAbfragen(), null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
    }

    /// <summary>Holt das Andock-Layout.</summary>
    public IRootDock Layout { get; }

    /// <summary>Holt den Fenstertitel.</summary>
    public string Titel
    {
        get => titel;
        private set => SetProperty(ref titel, value);
    }

    /// <summary>Holt den Namen der geöffneten Staffel (Kopf des Hauptfensters).</summary>
    public string Staffelname
    {
        get => staffelname;
        private set => SetProperty(ref staffelname, value);
    }

    /// <summary>Holt die Kurzangaben zur geöffneten Staffel, z. B. „click-TT-Datei · 7 Mannschaften · staffel.xml“.</summary>
    public string Staffelinfo
    {
        get => staffelinfo;
        private set => SetProperty(ref staffelinfo, value);
    }

    /// <summary>Holt die Kacheln mit dem Stand der Generierung.</summary>
    public Generierungsanzeige Anzeige { get; } = new();

    /// <summary>Holt die zuletzt geöffneten Staffeln für die Startseite.</summary>
    public IReadOnlyList<Zuletztzeile> Zuletzt
    {
        get => zuletzt;
        private set
        {
            if (SetProperty(ref zuletzt, value))
            {
                OnPropertyChanged(nameof(HatZuletzt));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es zuletzt geöffnete Staffeln gibt.</summary>
    public bool HatZuletzt => Zuletzt.Count > 0;

    /// <summary>
    /// Holt einen Wert, der angibt, ob es einen Plan zum Anzeigen gibt (laufende Generierung, Plan aus click-TT oder
    /// gemerkter Plan). Ohne Plan sind die Planansichten links deaktiviert.
    /// </summary>
    public bool HatPlan =>
        sitzung is Plansitzung s && (laufenderStand is not null || s.Staffel.BestehenderSpielplan.Count > 0 || Planquellen.Any(q => q.Art == PlanQuellenArt.GemerkterPlan));

    /// <summary>
    /// Holt die Art der vorn liegenden Ansicht (z. B. „Kosten“), für die Hervorhebung links; leer, solange keine Staffel
    /// geöffnet ist oder die Einrichtung läuft (dann ist links nichts hervorgehoben).
    /// </summary>
    public string AktiveAnsicht
    {
        get => ZeigtArbeitsbereich ? aktiveAnsicht : string.Empty;
        private set => SetProperty(ref aktiveAnsicht, value);
    }

    /// <summary>Holt die Einrichtungsseite nach dem Öffnen einer Staffel oder <c>null</c>, wenn es nichts (mehr) zu klären gibt.</summary>
    public EinrichtungViewModel? Einrichtung
    {
        get => einrichtung;
        private set
        {
            if (SetProperty(ref einrichtung, value))
            {
                // Während der Einrichtung sind Ansichten, Spielplandaten, Einstellungen und Drucken links gesperrt.
                OnPropertyChanged(nameof(ZeigtEinrichtung));
                OnPropertyChanged(nameof(ZeigtArbeitsbereich));
                OnPropertyChanged(nameof(AktiveAnsicht));
                EinstellungenCommand.NotifyCanExecuteChanged();
                DatenCommand.NotifyCanExecuteChanged();
                DruckenCommand.NotifyCanExecuteChanged();
                AnsichtsbefehleAktualisieren();
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob die Einrichtungsseite gezeigt wird.</summary>
    public bool ZeigtEinrichtung => Einrichtung is not null;

    /// <summary>Holt einen Wert, der angibt, ob Kacheln und Ansichten gezeigt werden (Staffel offen, Einrichtung fertig).</summary>
    public bool ZeigtArbeitsbereich => IstGeoeffnet && Einrichtung is null;

    /// <summary>Holt die Vergrößerung, mit der Ansichten geöffnet werden (siehe <see cref="StartzoomFestlegen"/>).</summary>
    public int Startzoom => startzoom;

    /// <summary>Holt die Beschriftung des Knopfs, der die Generierung startet, anhält und fortsetzt.</summary>
    public string GenerierungText => (Laeuft, Pausiert) switch
    {
        (false, _) => "Generierung starten",
        (true, true) => "Fortsetzen",
        _ => "Pausieren",
    };

    /// <summary>Holt einen Wert, der angibt, ob der Knopf das Pause-Symbol zeigt (Generierung läuft).</summary>
    public bool ZeigtPause => Laeuft && !Pausiert;

    /// <summary>Holt einen Wert, der angibt, ob der Knopf das Start-Symbol zeigt.</summary>
    public bool ZeigtStart => !ZeigtPause;

    /// <summary>
    /// Holt einen Wert, der angibt, ob „Start mit“ gewählt werden kann: nur vor dem Start. Eine laufende oder angehaltene
    /// Generierung setzt immer ihren eigenen besten Plan fort; für einen anderen Ausgangsplan erst „Beenden“.
    /// </summary>
    public bool StartplanWaehlbar => IstGeoeffnet && !Laeuft;

    /// <summary>Holt die Statuszeile (Pläne, Pläne/s, Kosten, Verbesserungen).</summary>
    public string Statuszeile
    {
        get => statuszeile;
        private set => SetProperty(ref statuszeile, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob eine Staffel geöffnet ist.</summary>
    public bool IstGeoeffnet => sitzung is not null;

    /// <summary>Holt einen Wert, der angibt, ob eine Generierung läuft.</summary>
    public bool Laeuft
    {
        get => laeuft;
        private set
        {
            if (SetProperty(ref laeuft, value))
            {
                PauseCommand.NotifyCanExecuteChanged();
                StoppenCommand.NotifyCanExecuteChanged();
                GenerierungGeaendert();
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob die Generierung angehalten ist.</summary>
    public bool Pausiert
    {
        get => pausiert;
        private set
        {
            if (SetProperty(ref pausiert, value))
            {
                OnPropertyChanged(nameof(PauseText));
                GenerierungGeaendert();
            }
        }
    }

    /// <summary>Holt die Beschriftung der Pause-Schaltfläche.</summary>
    public string PauseText => Pausiert ? "Weiter" : "Pause";

    /// <summary>Holt die auswählbaren Planquellen aller Ansichten.</summary>
    public IReadOnlyList<PlanQuelle> Planquellen
    {
        get => planquellen;
        private set => SetProperty(ref planquellen, value);
    }

    /// <summary>Holt den Befehl „Öffnen…“.</summary>
    public IAsyncRelayCommand OeffnenCommand { get; }

    /// <summary>Holt den Befehl „Neue Datei zur komplett manuellen Eingabe erstellen…“ (Original <c>MenuNewClick</c>).</summary>
    public IAsyncRelayCommand NeuCommand { get; }

    /// <summary>Holt den Befehl „Schließen“: Staffel schließen und zur Startseite zurückkehren.</summary>
    public IAsyncRelayCommand SchliessenCommand { get; }

    /// <summary>Holt den Befehl, der die Generierung startet, anhält oder fortsetzt (ein Knopf, der umschaltet).</summary>
    public IRelayCommand GenerierungCommand { get; }

    /// <summary>Holt den Befehl, der eine zuletzt geöffnete Staffel öffnet (Parameter: Pfad).</summary>
    public IAsyncRelayCommand<string> ZuletztOeffnenCommand { get; }

    /// <summary>Holt den Befehl, eine Staffel aus der Liste „Zuletzt geöffnet“ zu entfernen (die Datei bleibt erhalten).</summary>
    public IRelayCommand<string> ZuletztEntfernenCommand { get; }

    /// <summary>Holt den Befehl, der die aktive Ansicht vergrößert (Strg + Plus wie im Browser).</summary>
    public IRelayCommand ZoomGroesserCommand { get; }

    /// <summary>Holt den Befehl, der die aktive Ansicht verkleinert (Strg + Minus wie im Browser).</summary>
    public IRelayCommand ZoomKleinerCommand { get; }

    /// <summary>Holt den Befehl, der die aktive Ansicht auf 100 % zurücksetzt (Strg + 0 wie im Browser).</summary>
    public IRelayCommand ZoomZuruecksetzenCommand { get; }

    /// <summary>Holt den Befehl „Generierung starten“ (mit leerem Plan wie im Original).</summary>
    public IRelayCommand StartenCommand { get; }

    /// <summary>Holt den Befehl „Pause/Weiter“.</summary>
    public IRelayCommand PauseCommand { get; }

    /// <summary>Holt den Befehl „Generierung beenden“.</summary>
    public IRelayCommand StoppenCommand { get; }

    /// <summary>Holt den Befehl „Plan merken…“ (bester Plan der Generierung).</summary>
    public IAsyncRelayCommand PlanMerkenCommand { get; }

    /// <summary>Holt den Befehl „CSV für click-TT exportieren…“ (bester Plan der Generierung).</summary>
    public IAsyncRelayCommand CsvExportierenCommand { get; }

    /// <summary>Holt den Befehl „Drucken…“ (Original: Druckauswahl und Ausdruck; hier als PDF-Datei).</summary>
    public IAsyncRelayCommand DruckenCommand { get; }

    /// <summary>Holt den Befehl „Anleitung“: öffnet die mitgelieferte PDF-Anleitung (wie im Original).</summary>
    public IAsyncRelayCommand AnleitungCommand { get; }

    /// <summary>Holt den Befehl „Über…“ (Programm, Urheber, Lizenz).</summary>
    public IAsyncRelayCommand UeberCommand { get; }

    /// <summary>Holt den Befehl „Neue Kostenansicht“.</summary>
    public IRelayCommand KostenansichtCommand { get; }

    /// <summary>Holt den Befehl „Neue Terminplanansicht“.</summary>
    public IRelayCommand TerminplanansichtCommand { get; }

    /// <summary>Holt den Befehl „Neue Diagrammansicht“.</summary>
    public IRelayCommand DiagrammansichtCommand { get; }

    /// <summary>Holt den Befehl „Neue Qualitätsansicht“.</summary>
    public IRelayCommand QualitaetsansichtCommand { get; }

    /// <summary>Holt den Befehl „Neue Meldungsansicht“ (Meldungen je Mannschaft, im Original unter der Kostentabelle).</summary>
    public IRelayCommand MeldungsansichtCommand { get; }

    /// <summary>Holt den Befehl „Terminwünsche“ (Wünsche und Auswertung je Mannschaft, als andockbares Dokument).</summary>
    public IRelayCommand TerminwunschansichtCommand { get; }

    /// <summary>Holt den Befehl „Nachbarmannschaften“ (gemeldete Spiele der Nachbarmannschaften, als andockbares Dokument).</summary>
    public IRelayCommand NachbarterminansichtCommand { get; }

    /// <summary>Holt den Befehl „Einstellungen“ (Optionen der Staffel, als andockbares Dokument).</summary>
    public IRelayCommand EinstellungenCommand { get; }

    /// <summary>Holt den Befehl „Spielplandaten…“ (Original Schaltfläche „Daten“).</summary>
    public IAsyncRelayCommand DatenCommand { get; }

    /// <summary>Holt die möglichen Ausgangspläne für „Generierung starten“.</summary>
    public IReadOnlyList<Startplan> Startplaene
    {
        get => startplaene;
        private set => SetProperty(ref startplaene, value);
    }

    /// <summary>Holt oder setzt den Ausgangsplan für „Generierung starten“ (Standard: leerer Plan wie im Original).</summary>
    public Startplan? Startplan
    {
        get => startplan;
        set
        {
            if (value is not null)
            {
                SetProperty(ref startplan, value);
            }
        }
    }

    /// <summary>Holt die Optionen der geöffneten Staffel (<c>null</c>, solange keine geöffnet ist).</summary>
    internal Berechnungsoptionen? Optionen => sitzung?.Optionen;

    /// <summary>Holt die geöffnete Staffel (<c>null</c>, solange keine geöffnet ist).</summary>
    internal Staffel? Staffel => sitzung?.Staffel;

    /// <summary>Holt die Dienste der Oberfläche (für Rückfragen aus Ansichten).</summary>
    internal IOberflaeche Oberflaeche => oberflaeche;

    /// <inheritdoc/>
    public void Dispose()
    {
        statusTakt.Dispose();
        dienst.Verbessert -= BeiVerbesserung;
        dienst.Dispose();
    }

    /// <summary>
    /// Legt die Vergrößerung fest, mit der die Ansichten starten (von der Oberfläche einmal nach der Größe des
    /// Bildschirms ermittelt, siehe <see cref="Zoom.Vorschlag"/>); gilt für die offenen und alle neuen Ansichten.
    /// </summary>
    /// <param name="prozent">Die Vergrößerung in Prozent.</param>
    public void StartzoomFestlegen(int prozent)
    {
        startzoom = Math.Clamp(prozent, Zoom.Minimum, Zoom.Maximum);
        OnPropertyChanged(nameof(Startzoom));
        foreach (IZoombar ansicht in ansichten.Cast<IZoombar>().Concat(new IZoombar?[] { terminwunschAnsicht, nachbarterminAnsicht }.OfType<IZoombar>()))
        {
            ansicht.Zoom.Prozent = startzoom;
        }
    }

    /// <summary>Liefert den Plan einer Quelle (bewertet), sofern vorhanden.</summary>
    /// <param name="quelle">Die Quelle.</param>
    /// <returns>Der Plan oder <c>null</c>.</returns>
    internal Planstand? PlanFuer(PlanQuelle quelle)
    {
        Plansitzung? s = sitzung;
        if (s is null)
        {
            return null;
        }

        switch (quelle.Art)
        {
            case PlanQuellenArt.LaufendeGenerierung:
                return laufenderStand;
            case PlanQuellenArt.ClickTtPlan:
                clickTtStand ??= new Planstand(s.Staffel.BestehenderSpielplan, s.Bewerten(s.Staffel.BestehenderSpielplan));
                return clickTtStand;
            default:
                return GemerktenStandLaden(s, quelle.Eintrag!);
        }
    }

    /// <summary>
    /// Ändert eine Gewichtung nach Rückfrage wie das Original nach einem Klick in der Kostenansicht: Optionen speichern,
    /// laufende Generierung mit den neuen Gewichten fortsetzen, alle Ansichten neu bewerten.
    /// </summary>
    /// <param name="ziel">Die Gewichtung.</param>
    /// <param name="angezeigt">Plan der Ansicht, aus der geklickt wurde (für die Meldungen im Dialog).</param>
    /// <returns>Erledigt nach Dialog und Übernahme.</returns>
    internal async Task GewichtungAendernAsync(Gewichtungsziel ziel, Planstand? angezeigt)
    {
        if (sitzung is not Plansitzung s)
        {
            return;
        }

        Gewichtung aktuell = ziel.Lesen(s.Optionen);
        string dialogtitel = ziel switch
        {
            MannschaftsKostenartGewichtungsziel d => $"Gewichtung der Mannschaft {d.Name} ändern",
            MannschaftsGewichtungsziel => "Gewichtung der Mannschaft ändern",
            _ => "Gewichtung ändern",
        };
        IReadOnlyList<string> meldungen = ziel is MannschaftsKostenartGewichtungsziel detail ? DetailMeldungen(s, detail, angezeigt) : [];
        if (await oberflaeche.GewichtungWaehlenAsync(dialogtitel, ziel.Beschriftung, aktuell, meldungen) is not Gewichtung neu || neu == aktuell)
        {
            return;
        }

        if (OptionenAnwenden(ziel.Setzen(s.Optionen, neu)))
        {
            optionenAnsicht?.Laden();
        }
    }

    /// <summary>
    /// Setzt eine Gewichtung direkt (Details der Kostenansicht, ohne Dialog) und übernimmt sie wie nach dem
    /// Gewichtungsdialog: speichern, laufende Generierung mit den neuen Gewichten fortsetzen, alle Ansichten neu bewerten.
    /// </summary>
    /// <param name="ziel">Die Gewichtung.</param>
    /// <param name="wert">Die neue Stufe.</param>
    internal void GewichtungSetzen(Gewichtungsziel ziel, Gewichtung wert)
    {
        if (sitzung is not Plansitzung s || ziel.Lesen(s.Optionen) == wert)
        {
            return;
        }

        if (OptionenAnwenden(ziel.Setzen(s.Optionen, wert)))
        {
            optionenAnsicht?.Laden();
        }
    }

    /// <summary>Meldungen einer Mannschaft zu einer Kostenart im angezeigten Plan (Details der Kostenansicht).</summary>
    /// <param name="mannschaft">Name der Mannschaft.</param>
    /// <param name="art">Die Kostenart.</param>
    /// <param name="angezeigt">Der angezeigte Plan.</param>
    /// <returns>Die Meldungen; leer, wenn es keine gibt.</returns>
    internal IReadOnlyList<string> Zellmeldungen(string mannschaft, MannschaftsKostenart art, Planstand? angezeigt) =>
        sitzung is Plansitzung s && angezeigt is not null ? s.Meldungen(angezeigt.Spiele, mannschaft, art) : [];

    /// <summary>
    /// Übernimmt geänderte Optionen wie das Original (<c>DoOnAfterPlanChanged</c>): speichern, laufende Generierung vom
    /// besten Plan aus mit den neuen Optionen fortsetzen, alle Ansichten neu bewerten.
    /// </summary>
    /// <param name="neu">Die neuen Optionen.</param>
    /// <returns><c>true</c>, wenn sie übernommen wurden; bei einem Speicherfehler erscheint eine Meldung.</returns>
    internal bool OptionenAnwenden(Berechnungsoptionen neu)
    {
        if (sitzung is not Plansitzung s)
        {
            return false;
        }

        try
        {
            s.OptionenSpeichern(neu);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            _ = oberflaeche.MeldenAsync("Einstellungen", "Die Optionen konnten nicht gespeichert werden:" + Environment.NewLine + fehler.Message);
            return false;
        }

        NachAenderung(s);
        return true;
    }

    /// <summary>Terminwünsche der geöffneten Staffel.</summary>
    /// <returns>Die Übersicht oder <c>null</c>, solange keine Staffel geöffnet ist.</returns>
    internal Terminwunschuebersicht? Terminwuensche() => sitzung?.Terminwuensche();

    /// <summary>Spiele der Nachbarmannschaften der geöffneten Staffel.</summary>
    /// <returns>Je Mannschaft die Nachbartermine; leer, solange keine Staffel geöffnet ist.</returns>
    internal IReadOnlyList<MannschaftsNachbartermine> Nachbartermine() => sitzung?.Nachbartermine() ?? [];

    /// <summary>Terminplan eines Plans mit den Hinweisen und Nachbarterminen des Originals.</summary>
    /// <param name="stand">Der Plan.</param>
    /// <returns>Die Spiele; leer, solange keine Staffel geöffnet ist.</returns>
    internal IReadOnlyList<Terminplanzeile> Terminplan(Planstand stand) => sitzung?.Terminplan(stand.Spiele) ?? [];

    /// <summary>
    /// Baut den Ausdruck zur Druckauswahl: Terminwünsche und Nachbartermine der Staffel, Kosten, Spielplan und
    /// Mannschaftspläne aus dem gewählten Plan.
    /// </summary>
    /// <param name="auswahl">Die Druckauswahl.</param>
    /// <returns>Der Ausdruck.</returns>
    internal Druckdokument DruckdokumentErstellen(DruckauswahlViewModel auswahl)
    {
        ArgumentNullException.ThrowIfNull(auswahl);
        Plansitzung s = sitzung ?? throw new InvalidOperationException("Keine Staffel geöffnet.");
        var abschnitte = new List<Abschnitt>();
        if (auswahl.Terminwuensche)
        {
            abschnitte.Add(Druckbericht.Terminwuensche(s.Terminwuensche()));
        }

        if (auswahl.Nachbartermine)
        {
            abschnitte.Add(Druckbericht.Termine("Termine der Nachbarmannschaften", Terminzeilen.Nachbartermine(s.Nachbartermine())));
        }

        string plan = string.Empty;
        if (auswahl.Quelle is PlanQuelle quelle && PlanFuer(quelle) is Planstand stand)
        {
            abschnitte.AddRange(PlanAbschnitte(s, stand, auswahl));
            plan = quelle.Name;
        }

        return new Druckdokument(s.Staffel.Name, plan, DateTime.Now, abschnitte);
    }

    /// <summary>
    /// Speichert einen Plan als Excel-Arbeitsmappe (Übersicht, Spielplan, Mannschaftspläne, Kosten) und öffnet sie
    /// anschließend mit dem zugeordneten Programm.
    /// </summary>
    /// <param name="quelle">Die Planquelle.</param>
    /// <returns>Erledigt nach dem Export.</returns>
    internal async Task ExcelExportierenAsync(PlanQuelle quelle)
    {
        ArgumentNullException.ThrowIfNull(quelle);
        if (sitzung is not Plansitzung s || PlanFuer(quelle) is not Planstand stand)
        {
            return;
        }

        const string Titeltext = "Nach Excel exportieren";
        string? pfad = await oberflaeche.DateiSpeichernAsync(Titeltext, s.Staffel.Name + ".xlsx", new Dateifilter("Excel-Arbeitsmappen", ["*.xlsx"]));
        if (pfad is null)
        {
            return;
        }

        try
        {
            Arbeitsmappe mappe = Tabellenexport.Erstellen(
                s.Staffel,
                quelle.Name,
                DateTime.Now,
                s.Terminplan(stand.Spiele),
                Kostendarstellung.Kennzahlen(stand.Bewertung, s.Optionen),
                Kostendarstellung.Tabelle(stand.Bewertung, s.Optionen, s.Staffel));
            XlsxDatei.Speichern(pfad, mappe);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            await oberflaeche.MeldenAsync(Titeltext, fehler.Message);
            return;
        }

        await oberflaeche.DateiAnzeigenAsync(pfad);
    }

    /// <summary>Daten der Diagramme eines Plans.</summary>
    /// <param name="stand">Plan.</param>
    /// <returns>Die Diagrammdaten oder <c>null</c>, solange keine Staffel geöffnet ist.</returns>
    internal Diagrammdaten? Diagramme(Planstand stand) => sitzung?.Diagramme(stand.Spiele);

    /// <summary>Automatisch ermittelte Enddaten der Viertelrunden für die aktuellen Optionen.</summary>
    /// <returns>Die Daten oder <c>null</c>, solange keine Staffel geöffnet ist.</returns>
    internal (DateOnly Mitte1, DateOnly Mitte2)? AutomatischeRundenmitten() => sitzung?.AutomatischeRundenmitten();

    /// <summary>Löscht einen gemerkten Plan nach Rückfrage.</summary>
    /// <param name="quelle">Quelle des gemerkten Plans.</param>
    /// <returns>Erledigt nach Rückfrage und Löschen.</returns>
    internal async Task GemerktenPlanEntfernenAsync(PlanQuelle quelle)
    {
        if (sitzung is not Plansitzung s || quelle.Eintrag is not GemerkterPlanEintrag eintrag)
        {
            return;
        }

        if (!await oberflaeche.FragenAsync("Gemerkten Plan entfernen", $"Den gemerkten Plan „{eintrag.Name}“ endgültig löschen?"))
        {
            return;
        }

        try
        {
            s.GemerktenPlanEntfernen(eintrag);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            await oberflaeche.MeldenAsync("Gemerkten Plan entfernen", fehler.Message);
        }

        gemerkteStaende.Remove(eintrag.Pfad);
        QuellenNeuAufbauen();
    }

    private static IReadOnlyList<string> DetailMeldungen(Plansitzung s, MannschaftsKostenartGewichtungsziel ziel, Planstand? angezeigt)
    {
        IReadOnlyList<string> meldungen = angezeigt is null ? [] : s.Meldungen(angezeigt.Spiele, ziel.Name, ziel.Art);
        return meldungen.Count > 0 ? meldungen : ["Zu dieser Kostenart gibt es bei dieser Mannschaft keine Meldung."];
    }

    private static List<Abschnitt> PlanAbschnitte(Plansitzung s, Planstand stand, DruckauswahlViewModel auswahl)
    {
        var abschnitte = new List<Abschnitt>();
        if (auswahl.Kosten)
        {
            abschnitte.Add(Druckbericht.Kosten(Kostendarstellung.Kennzahlen(stand.Bewertung, s.Optionen), Kostendarstellung.Tabelle(stand.Bewertung, s.Optionen, s.Staffel)));
        }

        IReadOnlyList<Terminplanzeile> plan = s.Terminplan(stand.Spiele);
        if (auswahl.Spielplan)
        {
            abschnitte.Add(Druckbericht.Termine("Spielplan", Terminzeilen.Spielplan(plan)));
        }

        if (auswahl.Mannschaftsplaene)
        {
            abschnitte.Add(Druckbericht.Termine("Mannschaftspläne", Terminzeilen.Mannschaftsplaene(plan, s.Staffel, mitNachbarn: false)));
        }

        if (auswahl.MannschaftsplaeneMitNachbarn)
        {
            abschnitte.Add(Druckbericht.Termine("Mannschaftspläne mit Nachbarmannschaften", Terminzeilen.Mannschaftsplaene(plan, s.Staffel, mitNachbarn: true)));
        }

        if (auswahl.Diagramme)
        {
            abschnitte.Add(Druckbericht.Diagramme(s.Diagramme(stand.Spiele)));
        }

        return abschnitte;
    }

    private static List<Zuletztzeile> Zeilen(IReadOnlyList<ZuletztGeoeffnet> eintraege)
    {
        DateTime heute = DateTime.Today;
        return eintraege.Select(e => new Zuletztzeile(
            e.Name.Length > 0 ? e.Name : Path.GetFileNameWithoutExtension(e.Pfad),
            e.Pfad,
            Path.GetDirectoryName(e.Pfad) ?? string.Empty,
            Zeitangabe(e.Zeitpunkt, heute))).ToList();
    }

    private static string Zeitangabe(DateTime zeitpunkt, DateTime heute)
    {
        if (zeitpunkt.Date == heute)
        {
            return "heute, " + zeitpunkt.ToString("HH:mm", Deutsch);
        }

        return zeitpunkt.Date == heute.AddDays(-1) ? "gestern, " + zeitpunkt.ToString("HH:mm", Deutsch) : zeitpunkt.ToString("dd.MM.yyyy", Deutsch);
    }

    private static string? Ansichtsart(IDockable? ansicht) => ansicht switch
    {
        KostenAnsichtViewModel => "Kosten",
        QualitaetAnsichtViewModel => "Qualitaet",
        MeldungenAnsichtViewModel => "Meldungen",
        TerminplanAnsichtViewModel => "Terminplan",
        DiagrammAnsichtViewModel => "Diagramme",
        TerminwunschAnsichtViewModel => "Terminwuensche",
        NachbarterminAnsichtViewModel => "Nachbarn",
        OptionenAnsichtViewModel => "Einstellungen",
        _ => null,
    };

    private static string StartplanName(PlanQuelle quelle) => quelle.Art switch
    {
        PlanQuellenArt.LaufendeGenerierung => "bestem Plan der letzten Generierung",
        PlanQuellenArt.ClickTtPlan => "in click-TT vorhandenem Plan",
        _ => "gemerktem Plan „" + quelle.Eintrag!.Name + "“",
    };

    private static string Zahl(double wert) => wert.ToString("N0", Deutsch);

    private static string Statustext(Optimierungsstand stand)
    {
        string text = $"{Zahl(stand.Durchlaeufe)} Pläne berechnet ({Zahl(stand.PlaeneProSekunde)} Pläne/s)";
        if (stand.Kosten >= 0)
        {
            text += $" · Kosten {Kostenanzeige.Kurz(stand.Kosten)} · {stand.Verbesserungen} Verbesserungen"
                + $" · letzte vor {stand.SeitLetzterVerbesserung:hh\\:mm\\:ss}";
        }

        if (stand.SpezialKostenart is { } art)
        {
            text += $" · Spezial-Insel: {Referenzbewertung.Kostenartnamen[(int)art]}";
        }

        return stand.Pausiert ? "Angehalten – " + text : text;
    }

    /// <summary>Die Ansicht mit dem Fokus, sonst die aktive Ansicht des Hauptbereichs, sofern sie vergrößert werden kann.</summary>
    private IZoombar? ZoombareAnsicht() =>
        Layout.FocusedDockable as IZoombar ?? (Layout.ActiveDockable as IDock)?.ActiveDockable as IZoombar;

    private string NaechsteId() => "Ansicht" + (naechsteAnsicht++).ToString(CultureInfo.InvariantCulture);

    private async Task OeffnenAsync()
    {
        string? pfad = await oberflaeche.DateiOeffnenAsync("Staffel öffnen", [PlanDateien]);
        if (pfad is not null)
        {
            await StaffelOeffnenAsync(pfad);
        }
    }

    /// <summary>
    /// Original <c>MenuNewClick</c>: leere Plandatei anlegen, öffnen und sofort „Spielplandaten bearbeiten“ zeigen. Alle
    /// Daten (Liga, Mannschaften, Termine …) werden dann von Hand eingegeben; Änderungen speichert das Programm in diese Datei.
    /// </summary>
    private async Task NeueDateiAsync()
    {
        const string Titeltext = "Neue Datei zur komplett manuellen Eingabe erstellen";
        string? pfad = await oberflaeche.DateiSpeichernAsync(Titeltext, "Neue Staffel.xml", new Dateifilter("Plandateien", ["*.xml"]));
        if (pfad is null)
        {
            return;
        }

        try
        {
            Plansitzung.NeueDateiAnlegen(pfad);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            await oberflaeche.MeldenAsync(Titeltext, $"Die Datei konnte nicht angelegt werden:{Environment.NewLine}{fehler.Message}");
            return;
        }

        if (await StaffelOeffnenAsync(pfad))
        {
            await DatenBearbeitenAsync();
        }
    }

    /// <summary>Zeigt die Anleitung, die neben dem Programm liegt; fehlt sie, erscheint ein Hinweis.</summary>
    private async Task AnleitungAsync()
    {
        string pfad = Path.Combine(AppContext.BaseDirectory, Anleitungsdatei);
        if (File.Exists(pfad))
        {
            await oberflaeche.DateiAnzeigenAsync(pfad);
        }
        else
        {
            await oberflaeche.MeldenAsync("Anleitung", "Die Anleitung wurde nicht gefunden: " + pfad);
        }
    }

    /// <summary>
    /// Schließt die Staffel (nicht im Original): Generierung beenden, alle Ansichten schließen, Startseite zeigen. Läuft
    /// schon ein Plan, wird vorher nachgefragt, weil nicht gemerkte Pläne verloren gehen.
    /// </summary>
    private async Task SchliessenAsync()
    {
        if (sitzung is null)
        {
            return;
        }

        const string Frage = "Die Generierung wird beendet; nicht gemerkte Pläne gehen verloren. Staffel trotzdem schließen?";
        if (laufenderStand is not null && !await oberflaeche.FragenAsync("Staffel schließen", Frage))
        {
            return;
        }

        Stoppen();
        Volatile.Write(ref sitzung, null);
        laufenderStand = null;
        clickTtStand = null;
        gemerkteStaende.Clear();
        Einrichtung = null;
        var offen = new List<IDockable>(ansichten);
        offen.AddRange(new IDockable?[] { optionenAnsicht, terminwunschAnsicht, nachbarterminAnsicht }.OfType<IDockable>());
        foreach (IDockable ansicht in offen)
        {
            fabrik.CloseDockable(ansicht);
        }

        ansichten.Clear();
        optionenAnsicht = null;
        terminwunschAnsicht = null;
        nachbarterminAnsicht = null;
        AnsichtOeffnen(new KostenAnsichtViewModel(this, NaechsteId()));

        Titel = UeberViewModel.Programm;
        Staffelname = string.Empty;
        Staffelinfo = string.Empty;
        Statuszeile = "Bitte eine click-TT-Datei oder Plandatei öffnen.";
        Anzeige.Zuruecksetzen("Generierung noch nicht gestartet");
        OnPropertyChanged(nameof(IstGeoeffnet));
        OnPropertyChanged(nameof(ZeigtArbeitsbereich));
        OnPropertyChanged(nameof(AktiveAnsicht));
        OnPropertyChanged(nameof(StartplanWaehlbar));
        StartenCommand.NotifyCanExecuteChanged();
        GenerierungCommand.NotifyCanExecuteChanged();
        EinstellungenCommand.NotifyCanExecuteChanged();
        DatenCommand.NotifyCanExecuteChanged();
        DruckenCommand.NotifyCanExecuteChanged();
        TerminwunschansichtCommand.NotifyCanExecuteChanged();
        NachbarterminansichtCommand.NotifyCanExecuteChanged();
        SchliessenCommand.NotifyCanExecuteChanged();
        BefehleAktualisieren();
        QuellenNeuAufbauen();
        AlleAnsichtenAktualisieren();
    }

    private async Task<bool> StaffelOeffnenAsync(string pfad)
    {
        Plansitzung neu;
        try
        {
            neu = Plansitzung.Oeffnen(pfad, eigeneBasis, uebernahmeAus);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException or XmlException or FormatException
            or ClickTtFormatException or PlanDatenFormatException)
        {
            await oberflaeche.MeldenAsync("Staffel öffnen", $"Die Datei konnte nicht geöffnet werden:{Environment.NewLine}{fehler.Message}");
            return false;
        }

        // Eine laufende oder angehaltene Generierung gehört zur bisherigen Staffel: beenden, auch in der Anzeige
        // (sonst stünde dort weiter „Fortsetzen“, obwohl es für die neue Staffel nichts fortzusetzen gibt).
        Stoppen();
        Volatile.Write(ref sitzung, neu);
        laufenderStand = null;
        clickTtStand = null;
        gemerkteStaende.Clear();
        KopfAktualisieren(neu);
        AktiveAnsicht = Ansichtsart((Layout.ActiveDockable as IDock)?.ActiveDockable) ?? string.Empty;
        Anzeige.Zuruecksetzen("Generierung noch nicht gestartet");
        ZuletztMerken(neu);
        string herkunft = neu.UebernommenAus is null ? string.Empty : " Optionen und gemerkte Pläne wurden aus dem Original übernommen (nur kopiert).";
        Statuszeile = $"{neu.Staffel.Name}: {neu.Staffel.Mannschaften.Count} Mannschaften. Generierung noch nicht gestartet.{herkunft}";
        OnPropertyChanged(nameof(IstGeoeffnet));
        OnPropertyChanged(nameof(ZeigtArbeitsbereich));
        OnPropertyChanged(nameof(AktiveAnsicht));
        OnPropertyChanged(nameof(StartplanWaehlbar));
        StartenCommand.NotifyCanExecuteChanged();
        GenerierungCommand.NotifyCanExecuteChanged();
        EinstellungenCommand.NotifyCanExecuteChanged();
        DatenCommand.NotifyCanExecuteChanged();
        DruckenCommand.NotifyCanExecuteChanged();
        SchliessenCommand.NotifyCanExecuteChanged();
        BefehleAktualisieren();
        QuellenNeuAufbauen();
        optionenAnsicht?.Laden();
        terminwunschAnsicht?.Laden();
        nachbarterminAnsicht?.Laden();
        Einrichtung = EinrichtungErstellen(neu);
        return true;
    }

    /// <summary>
    /// Rückfragen des Originals nach dem Öffnen (<c>OpenFile</c>) als Einrichtungsseite statt einzelner Fenster: gespeicherte
    /// Benutzereinstellungen weiterverwenden, Rundenvorschlag (Halbrunde bzw. nur Rückrunde) und beim ersten Öffnen die
    /// Punkte des Erststart-Assistenten.
    /// </summary>
    /// <param name="neu">Die gerade geöffnete Staffel.</param>
    /// <returns>Die Seite oder <c>null</c>, wenn es nichts zu klären gibt.</returns>
    private EinrichtungViewModel? EinrichtungErstellen(Plansitzung neu)
    {
        var schritte = new List<Einrichtungsschritt>();
        IReadOnlyList<string> abweichungen = neu.AbweichungenVomStandard();
        if (abweichungen.Count > 0)
        {
            schritte.Add(new Einrichtungsschritt(
                schritte.Count + 1,
                "Gespeicherte Einstellungen",
                "Bei der letzten Verwendung wurden zu dieser Staffel folgende Einstellungen hinterlegt:",
                abweichungen,
                [
                    new Einrichtungsoption("Diese Einstellungen verwenden", "wie bei der letzten Verwendung"),
                    new Einrichtungsoption("Standardeinstellungen verwenden", "nur für diese Sitzung; die gespeicherten bleiben erhalten"),
                ]));
        }

        Rundenvorschlag vorschlag = neu.RundeVorschlagen(DateOnly.FromDateTime(DateTime.Today));
        Einrichtungsschritt? rundenschritt = null;
        if (vorschlag != Rundenvorschlag.Keiner)
        {
            bool halb = vorschlag == Rundenvorschlag.Halbrunde;
            rundenschritt = new Einrichtungsschritt(
                schritte.Count + 1,
                "Rundenplanung",
                halb ? "Die Runde ist kürzer als 6 Monate. Welche Spiele sollen generiert werden?" : "Die Vorrunde liegt in der Vergangenheit. Welche Spiele sollen generiert werden?",
                [],
                [
                    halb ? new Einrichtungsoption("Nur eine Halbrunde", "Vorschlag für kurze Runden") : new Einrichtungsoption("Nur die Rückrunde", "Vorschlag – die Vorrunde bleibt, wie sie ist"),
                    new Einrichtungsoption("Die ganze Runde", "Vor- und Rückrunde planen"),
                ]);
            schritte.Add(rundenschritt);
        }

        if (neu.ErsterStart)
        {
            int bisher = schritte.Count;
            schritte.AddRange(neu.Erststarthinweise().Select((h, i) => new Einrichtungsschritt(bisher + i + 1, h)));
        }

        return schritte.Count == 0
            ? null
            : new EinrichtungViewModel(
                neu.Staffel.Name + " einrichten",
                schritte,
                schritt => SchrittUebernehmenAsync(neu, schritt, ReferenceEquals(schritt, rundenschritt) ? vorschlag : Rundenvorschlag.Keiner),
                schritt => SeiteErzeugen(neu, schritt),
                EinrichtungAbschliessen);
    }

    private DatenDialogViewModel? SeiteErzeugen(Plansitzung s, Einrichtungsschritt schritt)
    {
        if (schritt.Punkt is not Erststartpunkt punkt)
        {
            return null;
        }

        Datenbearbeitung bearbeitung = s.DatenBearbeiten();
        DatenSeiteViewModel seite = punkt switch
        {
            Erststartpunkt.Spiellokale => new SpiellokaleSeiteViewModel(bearbeitung, oberflaeche),
            Erststartpunkt.Koppeltermine => new HeimkoppelnSeiteViewModel(bearbeitung),
            Erststartpunkt.Auswaertskoppeln => new AuswaertskoppelnSeiteViewModel(bearbeitung, oberflaeche),
            Erststartpunkt.Setzliste => new SetzlistenSeiteViewModel(bearbeitung),
            _ => new PflichtspieltageSeiteViewModel(bearbeitung, oberflaeche),
        };
        return new DatenDialogViewModel(bearbeitung, [seite]);
    }

    /// <summary>
    /// Übernimmt einen Punkt der Einrichtung (Original: „Ja“ bzw. OK im jeweiligen Dialog). <paramref name="vorschlag"/> ist
    /// nur beim Punkt „Rundenplanung“ gesetzt.
    /// </summary>
    private async Task<bool> SchrittUebernehmenAsync(Plansitzung s, Einrichtungsschritt schritt, Rundenvorschlag vorschlag)
    {
        if (!ReferenceEquals(s, sitzung))
        {
            return false;
        }

        if (schritt.Seite is DatenDialogViewModel seite)
        {
            if (!seite.Bestaetigen() || !await UebernehmenAsync(s, seite.Bearbeitung))
            {
                return false;
            }

            KopfAktualisieren(s);
        }
        else if (vorschlag != Rundenvorschlag.Keiner)
        {
            if (schritt.Auswahl == 0 && !await RundeUebernehmenAsync(s, vorschlag))
            {
                return false;
            }
        }
        else if (schritt.Auswahl == 1)
        {
            // Befund #34: nur für diese Sitzung, die gespeicherten Einstellungen bleiben erhalten.
            s.StandardoptionenFuerDieseSitzung();
        }

        NachAenderung(s);
        optionenAnsicht?.Laden();
        return true;
    }

    private async Task<bool> RundeUebernehmenAsync(Plansitzung s, Rundenvorschlag vorschlag)
    {
        try
        {
            s.RundeUebernehmen(vorschlag);
            return true;
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            await oberflaeche.MeldenAsync("Rundenplanung", fehler.Message);
            return false;
        }
    }

    private Task EinrichtungAbschliessen(bool generieren)
    {
        Einrichtung = null;
        if (generieren && IstGeoeffnet && !Laeuft)
        {
            Starten();
        }

        return Task.CompletedTask;
    }

    /// <summary>Schreibt die bearbeiteten Daten (<c>.modifications</c> bzw. Plandatei) und übernimmt sie in die Sitzung.</summary>
    /// <param name="s">Die Sitzung.</param>
    /// <param name="bearbeitung">Die Arbeitskopie.</param>
    /// <returns><c>true</c> bei Erfolg; sonst wurde eine Meldung angezeigt.</returns>
    private async Task<bool> UebernehmenAsync(Plansitzung s, Datenbearbeitung bearbeitung)
    {
        try
        {
            s.DatenUebernehmen(bearbeitung);
            return true;
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            await oberflaeche.MeldenAsync("Spielplandaten", "Die Änderungen konnten nicht gespeichert werden:" + Environment.NewLine + fehler.Message);
            return false;
        }
    }

    private void Starten()
    {
        if (sitzung is not Plansitzung s)
        {
            return;
        }

        // Ohne mindestens zwei Mannschaften gibt es keine Spiele; die Generierung liefe ins Leere (Kosten 0, kein Plan).
        if (s.Staffel.Mannschaften.Count < 2)
        {
            _ = oberflaeche.MeldenAsync(
                "Generierung starten",
                "Die Staffel enthält weniger als zwei Mannschaften. Bitte zuerst unter „Spielplandaten“ die Mannschaften eintragen.");
            return;
        }

        IReadOnlyList<Spiel>? ausgangsplan = startplan.Quelle is PlanQuelle quelle ? PlanFuer(quelle)?.Spiele : null;
        laufenderStand = null;
        dienst.Pausiert = false;
        Anzeige.Zuruecksetzen("erster Plan wird gesucht …");
        dienst.Starten(s.Staffel, s.Optionen, ausgangsplan);
        StartplaeneAufbauen();
        Laeuft = true;
        Pausiert = false;
        BefehleAktualisieren();
        AnsichtenAktualisieren(PlanQuellenArt.LaufendeGenerierung);
    }

    private void GenerierungUmschalten()
    {
        if (Laeuft)
        {
            PauseUmschalten();
        }
        else
        {
            Starten();
        }
    }

    private void GenerierungGeaendert()
    {
        OnPropertyChanged(nameof(GenerierungText));
        OnPropertyChanged(nameof(ZeigtPause));
        OnPropertyChanged(nameof(ZeigtStart));
        OnPropertyChanged(nameof(StartplanWaehlbar));
    }

    private void KopfAktualisieren(Plansitzung s)
    {
        string name = string.IsNullOrWhiteSpace(s.Staffel.Name) ? Path.GetFileNameWithoutExtension(s.Pfad) : s.Staffel.Name;
        Titel = $"{UeberViewModel.Programm} – {name}";
        Staffelname = name;
        string art = s.IstClickTtDatei ? "click-TT-Datei" : "Eigene Plandatei";
        Staffelinfo = string.Create(Deutsch, $"{art} · {s.Staffel.Mannschaften.Count} Mannschaften · {Path.GetFileName(s.Pfad)}");
    }

    private IReadOnlyList<ZuletztGeoeffnet> Zuletztlesen()
    {
        try
        {
            return Zuletztliste.Laden(eigeneBasis);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void ZuletztMerken(Plansitzung s)
    {
        try
        {
            Zuletzt = Zeilen(Zuletztliste.Hinzufuegen(eigeneBasis, new ZuletztGeoeffnet(Path.GetFullPath(s.Pfad), s.Staffel.Name, DateTime.Now)));
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            // Die Liste ist nur eine Bequemlichkeit; ohne sie geht es weiter.
        }
    }

    private void ZuletztEntfernen(string? pfad)
    {
        if (string.IsNullOrEmpty(pfad))
        {
            return;
        }

        try
        {
            Zuletzt = Zeilen(Zuletztliste.Entfernen(eigeneBasis, pfad));
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            _ = oberflaeche.MeldenAsync("Zuletzt geöffnet", "Die Liste konnte nicht gespeichert werden:" + Environment.NewLine + fehler.Message);
        }
    }

    private async Task ZuletztOeffnenAsync(string? pfad)
    {
        if (pfad is not null)
        {
            await StaffelOeffnenAsync(pfad);
        }
    }

    private void PauseUmschalten()
    {
        dienst.Pausiert = !dienst.Pausiert;
        Pausiert = dienst.Pausiert;
    }

    private void Stoppen()
    {
        dienst.Stoppen();
        Laeuft = false;
        Pausiert = false;
        StartplaeneAufbauen();
    }

    private async Task PlanMerkenAsync()
    {
        if (sitzung is not Plansitzung s || laufenderStand is not Planstand stand)
        {
            return;
        }

        string vorschlag = "Plan " + DateTime.Now.ToString("yyyy-MM-dd HH-mm", CultureInfo.InvariantCulture);
        string? name = await oberflaeche.TextEingebenAsync("Plan merken", "Name des Plans:", vorschlag);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            s.PlanMerken(name.Trim(), stand.Spiele);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            await oberflaeche.MeldenAsync("Plan merken", fehler.Message);
            return;
        }

        QuellenNeuAufbauen();
    }

    private async Task CsvExportierenAsync()
    {
        if (sitzung is not Plansitzung s || laufenderStand is not Planstand stand)
        {
            return;
        }

        IReadOnlyList<string> harteFehler = s.HarteFehler(stand.Spiele);
        if (harteFehler.Count > 0)
        {
            string frage = "Der Plan hat harte Fehler:" + Environment.NewLine + string.Join(Environment.NewLine, harteFehler)
                + Environment.NewLine + Environment.NewLine + "Trotzdem exportieren?";
            if (!await oberflaeche.FragenAsync("CSV exportieren", frage))
            {
                return;
            }
        }

        string? pfad = await oberflaeche.DateiSpeichernAsync("CSV für click-TT exportieren", s.Staffel.Name + ".csv", new Dateifilter("CSV-Dateien", ["*.csv"]));
        if (pfad is null)
        {
            return;
        }

        try
        {
            s.CsvExportieren(pfad, stand.Spiele);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            await oberflaeche.MeldenAsync("CSV exportieren", fehler.Message);
        }
    }

    private async Task DruckenAsync()
    {
        if (sitzung is not Plansitzung s)
        {
            return;
        }

        var auswahl = new DruckauswahlViewModel(Planquellen.Where(q => PlanFuer(q) is not null).ToList());
        if (!await oberflaeche.DruckauswahlAsync(auswahl) || !auswahl.IstGueltig)
        {
            return;
        }

        string? pfad = await oberflaeche.DateiSpeichernAsync("Drucken: PDF-Datei speichern", s.Staffel.Name + ".pdf", new Dateifilter("PDF-Dateien", ["*.pdf"]));
        if (pfad is null)
        {
            return;
        }

        try
        {
            Druckdokument dokument = DruckdokumentErstellen(auswahl);
            await oberflaeche.PdfErzeugenAsync(pfad, dokument, auswahl.Querformat ? Seitenformat.A4Quer : Seitenformat.A4Hoch);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            await oberflaeche.MeldenAsync("Drucken", fehler.Message);
            return;
        }

        await oberflaeche.DateiAnzeigenAsync(pfad);
    }

    private void TerminwuenscheOeffnen()
    {
        if (terminwunschAnsicht is null)
        {
            terminwunschAnsicht = new TerminwunschAnsichtViewModel(this);
            terminwunschAnsicht.Zoom.Prozent = startzoom;
            fabrik.AnsichtHinzufuegen(terminwunschAnsicht);
        }
        else
        {
            fabrik.SetActiveDockable(terminwunschAnsicht);
        }
    }

    private void NachbartermineOeffnen()
    {
        if (nachbarterminAnsicht is null)
        {
            nachbarterminAnsicht = new NachbarterminAnsichtViewModel(this);
            nachbarterminAnsicht.Zoom.Prozent = startzoom;
            fabrik.AnsichtHinzufuegen(nachbarterminAnsicht);
        }
        else
        {
            fabrik.SetActiveDockable(nachbarterminAnsicht);
        }
    }

    private void EinstellungenOeffnen()
    {
        if (optionenAnsicht is null)
        {
            optionenAnsicht = new OptionenAnsichtViewModel(this);
            fabrik.AnsichtHinzufuegen(optionenAnsicht);
        }
        else
        {
            fabrik.SetActiveDockable(optionenAnsicht);
        }
    }

    /// <summary>
    /// Original <c>ToolButtonDataClick</c>: Generierung anhalten, Arbeitskopie bearbeiten, bei OK speichern und wie nach
    /// geänderten Optionen übernehmen (<c>DoOnAfterPlanChanged</c>).
    /// </summary>
    private async Task DatenBearbeitenAsync()
    {
        if (sitzung is not Plansitzung s)
        {
            return;
        }

        using (dienst.Anhalten())
        {
            Datenbearbeitung bearbeitung = s.DatenBearbeiten();
            IReadOnlyList<DatenSeiteViewModel> seiten = DatenDialogViewModel.AlleSeiten(bearbeitung, oberflaeche);
            var dialog = new DatenDialogViewModel(bearbeitung, seiten);
            if (!await oberflaeche.DatenBearbeitenAsync(dialog) || !await UebernehmenAsync(s, bearbeitung))
            {
                return;
            }

            KopfAktualisieren(s);
            NachAenderung(s);
            optionenAnsicht?.Laden();
        }
    }

    /// <summary>Nach geänderten Optionen oder Stammdaten: Generierung fortsetzen, Pläne neu bewerten, Ansichten aktualisieren.</summary>
    private void NachAenderung(Plansitzung s)
    {
        if (dienst.Laeuft)
        {
            dienst.DatenAendern(s.Staffel, s.Optionen);
            Anzeige.KostenAngepasst();
        }

        clickTtStand = null;
        gemerkteStaende.Clear();
        if (laufenderStand is Planstand laufend)
        {
            laufenderStand = new Planstand(laufend.Spiele, s.Bewerten(laufend.Spiele));
        }

        AlleAnsichtenAktualisieren();
        terminwunschAnsicht?.Laden();
        nachbarterminAnsicht?.Laden();
    }

    private void AnsichtOeffnen(AnsichtViewModel ansicht)
    {
        ansicht.Zoom.Prozent = startzoom;
        ansichten.Add(ansicht);
        fabrik.AnsichtHinzufuegen(ansicht);
        ansicht.QuellenUebernehmen(Planquellen);
    }

    private Planstand? GemerktenStandLaden(Plansitzung s, GemerkterPlanEintrag eintrag)
    {
        if (gemerkteStaende.TryGetValue(eintrag.Pfad, out Planstand? stand))
        {
            return stand;
        }

        try
        {
            IReadOnlyList<Spiel> spiele = s.GemerktenPlanLaden(eintrag);
            stand = new Planstand(spiele, s.Bewerten(spiele));
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException or XmlException or FormatException or PlanDatenFormatException)
        {
            stand = null;
        }

        gemerkteStaende[eintrag.Pfad] = stand;
        return stand;
    }

    private void QuellenNeuAufbauen()
    {
        var neu = new List<PlanQuelle> { PlanQuelle.Laufend };
        if (sitzung is Plansitzung s)
        {
            if (s.Staffel.BestehenderSpielplan.Count > 0)
            {
                neu.Add(PlanQuelle.ClickTt);
            }

            neu.AddRange(s.GemerktePlaene().Select(PlanQuelle.Gemerkt));
        }

        Planquellen = neu;
        StartplaeneAufbauen();
        foreach (AnsichtViewModel ansicht in ansichten)
        {
            ansicht.QuellenUebernehmen(neu);
        }

        AnsichtsbefehleAktualisieren();
    }

    /// <summary>Ausgangspläne für „Generierung starten“: leer, bester Plan der letzten Generierung, click-TT-Plan, gemerkte Pläne.</summary>
    private void StartplaeneAufbauen()
    {
        var starts = new List<Startplan> { Startplan.Leer };
        starts.AddRange(Planquellen
            .Where(q => q.Art != PlanQuellenArt.LaufendeGenerierung || laufenderStand is not null)
            .Select(q => new Startplan(StartplanName(q), q)));
        Startplan bisher = startplan;
        Startplaene = starts;
        startplan = starts.FirstOrDefault(p => p == bisher) ?? Startplan.Leer;
        OnPropertyChanged(nameof(Startplan));
    }

    private void AlleAnsichtenAktualisieren()
    {
        foreach (AnsichtViewModel ansicht in ansichten)
        {
            ansicht.Aktualisieren();
        }
    }

    private void AnsichtenAktualisieren(PlanQuellenArt art)
    {
        foreach (AnsichtViewModel ansicht in ansichten.Where(a => a.QuellenArt == art))
        {
            ansicht.Aktualisieren();
        }
    }

    private void BefehleAktualisieren()
    {
        PlanMerkenCommand.NotifyCanExecuteChanged();
        CsvExportierenCommand.NotifyCanExecuteChanged();
        AnsichtsbefehleAktualisieren();
    }

    private void AnsichtsbefehleAktualisieren()
    {
        OnPropertyChanged(nameof(HatPlan));
        KostenansichtCommand.NotifyCanExecuteChanged();
        QualitaetsansichtCommand.NotifyCanExecuteChanged();
        MeldungsansichtCommand.NotifyCanExecuteChanged();
        TerminplanansichtCommand.NotifyCanExecuteChanged();
        DiagrammansichtCommand.NotifyCanExecuteChanged();
        TerminwunschansichtCommand.NotifyCanExecuteChanged();
        NachbarterminansichtCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Zeigt die Ansicht dieser Art: holt eine offene nach vorn oder öffnet sie neu.</summary>
    private void AnsichtZeigen<T>(Func<T> erzeugen)
        where T : AnsichtViewModel
    {
        if (ansichten.OfType<T>().FirstOrDefault() is T offen)
        {
            fabrik.SetActiveDockable(offen);
            if (offen.Owner is IDock dock)
            {
                fabrik.SetFocusedDockable(dock, offen);
            }

            return;
        }

        AnsichtOeffnen(erzeugen());
    }

    /// <summary>Verbesserter Plan (Hintergrund-Thread): dort bewerten, auf dem UI-Thread anzeigen.</summary>
    private void BeiVerbesserung(object? sender, Optimierungsstand stand)
    {
        Plansitzung? s = Volatile.Read(ref sitzung);
        if (s is null || stand.Spiele.Count == 0)
        {
            return;
        }

        var neu = new Planstand(stand.Spiele, s.Bewerten(stand.Spiele));
        oberflaeche.AufOberflaeche(() =>
        {
            if (!ReferenceEquals(s, sitzung) || !Laeuft)
            {
                return;
            }

            bool erster = laufenderStand is null;
            laufenderStand = neu;
            Anzeige.Uebernehmen(neu.Bewertung);
            if (erster)
            {
                StartplaeneAufbauen();
            }

            BefehleAktualisieren();
            AnsichtenAktualisieren(PlanQuellenArt.LaufendeGenerierung);
        });
    }

    /// <summary>Statuszeile im Takt (Timer-Thread): Stand holen, auf dem UI-Thread anzeigen.</summary>
    private void StatusAbfragen()
    {
        if (!dienst.Laeuft)
        {
            return;
        }

        Optimierungsstand stand = dienst.Stand();
        Exception? fehler = dienst.Fehler;
        string text = fehler is null ? Statustext(stand) : "Generierung abgebrochen: " + fehler.Message;
        oberflaeche.AufOberflaeche(() =>
        {
            if (Laeuft)
            {
                Statuszeile = text;
                Anzeige.Uebernehmen(stand);
            }
        });
    }
}
