// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Einfach;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Der normale Modus (Peter, 29.10.2026; Entwurf „AndiGenerator.NET – Einfacher Modus“, Seiten 1, 2D und 3): Dateien
/// ablegen, Plan erstellen, Ergebnis. Auf der Seite „Planung“ zeigen Balken je Kriterium, wie weit der beste Plan von der
/// bewiesenen Grenze entfernt ist und wie er zum gespeicherten Plan steht; daneben die größten Probleme in Sätzen. Der beste
/// Plan bleibt immer erhalten (<see cref="Normalplanung.Uebernehmen"/>).
/// </summary>
public sealed partial class NormalmodusViewModel : ObservableObject, IDisposable
{
    /// <summary>So viele Kriterien des Feinschliffs zeigen die Balken höchstens.</summary>
    public const int FeinschliffHoechstens = 6;

    /// <summary>Ohne Verbesserung seit so vielen Sekunden lohnt das Weiterrechnen kaum noch (Hinweis).</summary>
    public const int StillstandHinweisSekunden = 45;

    /// <summary>Ohne Verbesserung seit so vielen Sekunden endet die Planung von selbst.</summary>
    public const int StillstandEndeSekunden = 180;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private readonly HauptfensterViewModel hauptfenster;
    private readonly Lock sperre = new();
    private readonly Dictionary<Kostenkriterium, Kriterienwichtigkeit> wichtigkeiten = [];
    private readonly Stopwatch laufuhr = new();
    private Normalseite seite = Normalseite.Start;
    private IReadOnlyList<Normaldatei> dateien = [];
    private string pruefung = string.Empty;
    private string hinderung = string.Empty;
    private string vorhanden = string.Empty;
    private bool einrichtungAnzeigen;
    private Normalplanung? planung;
    private Timer? takt;
    private Schnellstand? letzterStand;
    private Planguete? letzteGuete;
    private TimeSpan zuletztBesser;
    private (IReadOnlyList<Spiel> Plan, Planguete Guete, DateTime Zeit)? vergleich;
    private IReadOnlyList<Normalgruppe> gruppen = [];
    private IReadOnlyList<Normalproblem> probleme = [];
    private string laufzeit = string.Empty;
    private string urteil = string.Empty;
    private string urteilText = string.Empty;
    private bool urteilGut;
    private string hinweis = string.Empty;
    private string fazitTitel = string.Empty;
    private string fazitText = string.Empty;
    private bool fazitGut;
    private string fazitVergleich = string.Empty;
    private IReadOnlyList<Normalproblem> vereinshinweise = [];
    private IReadOnlyList<Normalproblem> feinschliffhinweise = [];
    private PlanQuelle? ergebnisQuelle;
    private TerminplanAnsichtViewModel? terminplan;
    private DiagrammAnsichtViewModel? diagramme;
    private int reiter;
    private Schnellsteuerung steuerung = new();
    private int ansichtVersion = -1;
    private bool ansichtBeschaeftigt;

    /// <summary>Initialisiert den normalen Modus.</summary>
    /// <param name="hauptfenster">Das Hauptfenster (Staffel, Öffnen, Export).</param>
    internal NormalmodusViewModel(HauptfensterViewModel hauptfenster)
    {
        this.hauptfenster = hauptfenster;
        OeffnenCommand = new AsyncRelayCommand(() => hauptfenster.OeffnenCommand.ExecuteAsync(null), () => !Laeuft);
        PlanErstellenCommand = new AsyncRelayCommand(() => StartenAsync(neu: true), () => hauptfenster.IstGeoeffnet && !Laeuft && hinderung.Length == 0);
        EinrichtungPruefenCommand = new RelayCommand(EinrichtungPruefen, () => hauptfenster.ZeigtEinrichtung && !Laeuft);
        FertigCommand = new AsyncRelayCommand(FertigAsync, () => Laeuft);
        PauseCommand = new AsyncRelayCommand(PauseWeiterAsync, () => Laeuft);
        NeuStartenCommand = new AsyncRelayCommand(NeuStartenAsync, () => Laeuft);
        ErgebnisZeigenCommand = new RelayCommand(VorhandenenPlanZeigen, () => vorhanden.Length > 0 && !Laeuft);
        ExportierenCommand = new AsyncRelayCommand(() => ergebnisQuelle is PlanQuelle q ? hauptfenster.PlanExportierenAsync(q) : Task.CompletedTask, () => ergebnisQuelle is not null);
        DruckenCommand = new AsyncRelayCommand(() => hauptfenster.PlanDruckenAsync(ergebnisQuelle), () => ergebnisQuelle is not null);
        DatenCommand = new AsyncRelayCommand(() => hauptfenster.DatenCommand.ExecuteAsync(null), () => hauptfenster.IstGeoeffnet && !Laeuft);
        ZurueckCommand = new RelayCommand(() => Seite = Normalseite.Start, () => !Laeuft);
        ReiterCommand = new RelayCommand<string>(r => Reiter = int.Parse(r ?? "0", CultureInfo.InvariantCulture));
        WichtigkeitCommand = new RelayCommand<string>(WichtigkeitSetzen);
        hauptfenster.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(HauptfensterViewModel.ZeigtEinrichtung) or nameof(HauptfensterViewModel.Einrichtung))
            {
                hauptfenster.NormalAnsichtGeaendert();
                EinrichtungPruefenCommand.NotifyCanExecuteChanged();
            }
        };
        Aktualisieren();
    }

    /// <summary>Holt die angezeigte Seite.</summary>
    public Normalseite Seite
    {
        get => seite;
        private set
        {
            if (SetProperty(ref seite, value))
            {
                OnPropertyChanged(nameof(ZeigtStart));
                OnPropertyChanged(nameof(ZeigtPlanung));
                OnPropertyChanged(nameof(ZeigtErgebnis));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob die Seite „Dateien“ angezeigt wird.</summary>
    public bool ZeigtStart => Seite == Normalseite.Start;

    /// <summary>Holt einen Wert, der angibt, ob die Seite „Planung“ angezeigt wird.</summary>
    public bool ZeigtPlanung => Seite == Normalseite.Planung;

    /// <summary>Holt einen Wert, der angibt, ob die Seite „Ergebnis“ angezeigt wird.</summary>
    public bool ZeigtErgebnis => Seite == Normalseite.Ergebnis;

    /// <summary>Holt einen Wert, der angibt, ob eine Staffel geöffnet ist.</summary>
    public bool IstGeoeffnet => hauptfenster.IstGeoeffnet;

    /// <summary>Holt die Dateien aus click-TT (erste Seite).</summary>
    public IReadOnlyList<Normaldatei> Dateien
    {
        get => dateien;
        private set => SetProperty(ref dateien, value);
    }

    /// <summary>Holt das Ergebnis der Prüfung (erste Seite), z. B. „Geprüft: Vor- und Rückrunde …“.</summary>
    public string Pruefung
    {
        get => pruefung;
        private set => SetProperty(ref pruefung, value);
    }

    /// <summary>Holt, warum der normale Modus diese Staffel nicht planen kann (leer, wenn er es kann).</summary>
    public string Hinderung
    {
        get => hinderung;
        private set
        {
            if (SetProperty(ref hinderung, value))
            {
                OnPropertyChanged(nameof(HatHinderung));
                PlanErstellenCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es eine <see cref="Hinderung"/> gibt.</summary>
    public bool HatHinderung => Hinderung.Length > 0;

    /// <summary>Holt den Hinweis auf einen schon vorhandenen besten Plan (leer, wenn es keinen gibt).</summary>
    public string Vorhanden
    {
        get => vorhanden;
        private set
        {
            if (SetProperty(ref vorhanden, value))
            {
                OnPropertyChanged(nameof(HatVorhanden));
                ErgebnisZeigenCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es einen <see cref="Vorhanden"/>en Plan gibt.</summary>
    public bool HatVorhanden => Vorhanden.Length > 0;

    /// <summary>Holt einen Wert, der angibt, ob die Einrichtung gerade geprüft wird (dann liegt sie über den Seiten).</summary>
    public bool EinrichtungAnzeigen
    {
        get => einrichtungAnzeigen;
        private set
        {
            if (SetProperty(ref einrichtungAnzeigen, value))
            {
                hauptfenster.NormalAnsichtGeaendert();
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob die Planung läuft.</summary>
    public bool Laeuft => planung is not null || pausiert;

    /// <summary>Holt die Gruppen der Balken (Seite „Planung“).</summary>
    public IReadOnlyList<Normalgruppe> Gruppen
    {
        get => gruppen;
        private set => SetProperty(ref gruppen, value);
    }

    /// <summary>Holt die größten Probleme des besten Plans (Seite „Planung“).</summary>
    public IReadOnlyList<Normalproblem> Probleme
    {
        get => probleme;
        private set => SetProperty(ref probleme, value);
    }

    /// <summary>Holt „läuft seit … · zuletzt besser vor …“.</summary>
    public string Laufzeit
    {
        get => laufzeit;
        private set => SetProperty(ref laufzeit, value);
    }

    /// <summary>Holt das Urteil in einer Zeile, z. B. „Muss ist bestmöglich“.</summary>
    public string Urteil
    {
        get => urteil;
        private set => SetProperty(ref urteil, value);
    }

    /// <summary>Holt die Erläuterung zum Urteil (auch der Vergleich mit dem gespeicherten Plan).</summary>
    public string UrteilText
    {
        get => urteilText;
        private set => SetProperty(ref urteilText, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob das Urteil gut ist (grüner Haken statt Warnung).</summary>
    public bool UrteilGut
    {
        get => urteilGut;
        private set => SetProperty(ref urteilGut, value);
    }

    /// <summary>Holt den Hinweis unten rechts, z. B. „Der Plan ist schon spielbar.“.</summary>
    public string Hinweis
    {
        get => hinweis;
        private set => SetProperty(ref hinweis, value);
    }

    /// <summary>Holt die Überschrift des Kurz-Fazits (Seite „Ergebnis“).</summary>
    public string FazitTitel
    {
        get => fazitTitel;
        private set => SetProperty(ref fazitTitel, value);
    }

    /// <summary>Holt das Kurz-Fazit in einer Zeile.</summary>
    public string FazitText
    {
        get => fazitText;
        private set => SetProperty(ref fazitText, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob das Fazit gut ist.</summary>
    public bool FazitGut
    {
        get => fazitGut;
        private set => SetProperty(ref fazitGut, value);
    }

    /// <summary>Holt den Vergleich mit dem bisher besten Plan (leer, wenn es keinen gab).</summary>
    public string FazitVergleich
    {
        get => fazitVergleich;
        private set
        {
            if (SetProperty(ref fazitVergleich, value))
            {
                OnPropertyChanged(nameof(HatFazitVergleich));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es einen <see cref="FazitVergleich"/> gibt.</summary>
    public bool HatFazitVergleich => FazitVergleich.Length > 0;

    /// <summary>Holt, was man den Vereinen sagen sollte (Muss und Soll).</summary>
    public IReadOnlyList<Normalproblem> Vereinshinweise
    {
        get => vereinshinweise;
        private set
        {
            if (SetProperty(ref vereinshinweise, value))
            {
                OnPropertyChanged(nameof(HatVereinshinweise));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es <see cref="Vereinshinweise"/> gibt.</summary>
    public bool HatVereinshinweise => Vereinshinweise.Count > 0;

    /// <summary>Holt die größten Punkte des Feinschliffs (Seite „Ergebnis“).</summary>
    public IReadOnlyList<Normalproblem> Feinschliffhinweise
    {
        get => feinschliffhinweise;
        private set => SetProperty(ref feinschliffhinweise, value);
    }

    /// <summary>Holt den Terminplan des Ergebnisses.</summary>
    public TerminplanAnsichtViewModel? Terminplan
    {
        get => terminplan;
        private set
        {
            if (SetProperty(ref terminplan, value))
            {
                OnPropertyChanged(nameof(PlanungOhneAnsicht));
            }
        }
    }

    /// <summary>Holt die Diagramme des Ergebnisses.</summary>
    public DiagrammAnsichtViewModel? Diagramme
    {
        get => diagramme;
        private set => SetProperty(ref diagramme, value);
    }

    /// <summary>Holt oder setzt den Reiter der Seite „Ergebnis“: 0 Übersicht, 1 Terminplan, 2 Diagramme.</summary>
    public int Reiter
    {
        get => reiter;
        set
        {
            if (SetProperty(ref reiter, value))
            {
                OnPropertyChanged(nameof(ZeigtUebersicht));
                OnPropertyChanged(nameof(ZeigtTerminplan));
                OnPropertyChanged(nameof(ZeigtDiagramme));
                OnPropertyChanged(nameof(PlanungOhneAnsicht));
                PlanungsansichtAnfordern();
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob der Reiter „Übersicht“ gewählt ist.</summary>
    public bool ZeigtUebersicht => Reiter == 0;

    /// <summary>Holt einen Wert, der angibt, ob der Reiter „Terminplan“ gewählt ist.</summary>
    public bool ZeigtTerminplan => Reiter == 1;

    /// <summary>Holt einen Wert, der angibt, ob der Reiter „Diagramme“ gewählt ist.</summary>
    public bool ZeigtDiagramme => Reiter == 2;

    /// <summary>Holt den Befehl „Dateien auswählen …“.</summary>
    public IAsyncRelayCommand OeffnenCommand { get; }

    /// <summary>Holt den Befehl „Plan erstellen“ bzw. „Nochmal planen“.</summary>
    public IAsyncRelayCommand PlanErstellenCommand { get; }

    /// <summary>Holt den Befehl „Einrichtung prüfen (für Fortgeschrittene)“.</summary>
    public IRelayCommand EinrichtungPruefenCommand { get; }

    /// <summary>Holt den Befehl „Fertig – Plan übernehmen“.</summary>
    public IAsyncRelayCommand FertigCommand { get; }

    /// <summary>Holt den Befehl „Vorhandenen Plan ansehen“.</summary>
    public IRelayCommand ErgebnisZeigenCommand { get; }

    /// <summary>Holt den Befehl „Für click-TT exportieren“.</summary>
    public IAsyncRelayCommand ExportierenCommand { get; }

    /// <summary>Holt den Befehl „Für die Vereine drucken (PDF)“.</summary>
    public IAsyncRelayCommand DruckenCommand { get; }

    /// <summary>Holt den Befehl „Wünsche ändern“ (Spielplandaten).</summary>
    public IAsyncRelayCommand DatenCommand { get; }

    /// <summary>Holt den Befehl „Zurück zu den Dateien“.</summary>
    public IRelayCommand ZurueckCommand { get; }

    /// <summary>Holt den Befehl, der einen Reiter wählt (Parameter „0“, „1“, „2“).</summary>
    public IRelayCommand<string> ReiterCommand { get; }

    /// <summary>Holt oder setzt Zeiten und Kerne der Planung (<c>null</c>: Standard; kürzer für Tests und Bildschirmfotos).</summary>
    public Schnellplanereinstellungen? Planeinstellungen { get; set; }

    /// <summary>Öffnet die Staffel aus Dateien, die auf die Ablage gezogen wurden.</summary>
    /// <param name="pfade">Die Dateien.</param>
    /// <returns>Erledigt, wenn die Staffel geöffnet ist (oder ein Hinweis kam).</returns>
    public Task AblegenAsync(IReadOnlyList<string> pfade) => Laeuft ? Task.CompletedTask : hauptfenster.DateienOeffnenAsync(pfade);

    /// <summary>Stellt die Seiten nach dem Stand des Hauptfensters ein (nach dem Wechsel in den normalen Modus).</summary>
    public void Aktualisieren()
    {
        if (Laeuft)
        {
            return;
        }

        StartAufbauen(null, null, []);
        Seite = Normalseite.Start;
    }

    /// <summary>Übernimmt den besten Plan und beendet die Planung.</summary>
    /// <returns>Erledigt, wenn das Ergebnis angezeigt wird.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Die Planung läuft auf dem Threadpool und wartet nie auf die Oberfläche.")]
    public async Task FertigAsync()
    {
        Normalplanung? p;
        lock (sperre)
        {
            p = planung;
        }

        if (p is null && !pausiert)
        {
            return;
        }

        Schnellergebnis ergebnis = LeeresErgebnis;
        if (p is not null)
        {
            p.Beenden();
            try
            {
                ergebnis = await p.Lauf.ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                // Abgebrochen ohne Ergebnis: es zählt der beste Plan, den die Meldungen gebracht haben.
            }

            PlanungBeenden(p);
        }

        pausiert = false;
        LaufGeaendert();
        (IReadOnlyList<Spiel> Plan, Planguete Guete)? gesamt;
        Schnellstand? bestStand;
        lock (sperre)
        {
            gesamt = gesamtBester;
            bestStand = gesamtStand;
        }

        if (hauptfenster.Sitzung is not Plansitzung s)
        {
            Seite = Normalseite.Start;
            return;
        }

        bool neuGespeichert = false;
        bool gerechnet = false;
        foreach (IReadOnlyList<Spiel> plan in new[] { ergebnis.Plan, gesamt?.Plan }.OfType<IReadOnlyList<Spiel>>().Where(x => x.Count > 0))
        {
            gerechnet = true;
            try
            {
                neuGespeichert |= Normalplanung.Uebernehmen(s, plan, steuerung.Massstab).Neu;
            }
            catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
            {
                await hauptfenster.Oberflaeche.MeldenAsync("Plan speichern", "Der Plan ließ sich nicht speichern: " + fehler.Message).ConfigureAwait(true);
            }
        }

        if (Normalplanung.BesterPlan(s) is not { } b)
        {
            Seite = Normalseite.Start;
            return;
        }

        ErgebnisAufbauen(s, b.Plan, b.Guete, gerechnet && !neuGespeichert, ergebnis.SchrankeA ?? bestStand?.SchrankeA, ergebnis.SchrankeB ?? bestStand?.SchrankeB);
        Seite = Normalseite.Ergebnis;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Normalplanung? p;
        lock (sperre)
        {
            p = planung;
            planung = null;
        }

        p?.Dispose();
        takt?.Dispose();
        takt = null;
    }

    /// <summary>Nach dem Öffnen einer Staffel: die erste Seite mit den Dateien und der Prüfung.</summary>
    /// <param name="mannschaftsmeldung">Die mit geöffnete Mannschaftsmeldung (CSV), oder <c>null</c>.</param>
    /// <param name="terminwuensche">Der mit geöffnete Bericht „Terminwünsche“ (PDF), oder <c>null</c>.</param>
    /// <param name="hinweise">Hinweise zu Dateien, die nicht passten.</param>
    internal void StaffelGeoeffnet(string? mannschaftsmeldung, string? terminwuensche, IReadOnlyList<string> hinweise)
    {
        StartAufbauen(mannschaftsmeldung, terminwuensche, hinweise);
        Seite = Normalseite.Start;
    }

    /// <summary>Nach dem Schließen der Staffel: zurück zur ersten Seite.</summary>
    internal void StaffelGeschlossen()
    {
        Dispose();
        OnPropertyChanged(nameof(Laeuft));
        StartAufbauen(null, null, []);
        Seite = Normalseite.Start;
    }

    /// <summary>Die Einrichtung ist abgeschlossen: wieder die Seiten des normalen Modus.</summary>
    internal void EinrichtungBeendet()
    {
        EinrichtungAnzeigen = false;
        StartAufbauen(null, null, []);
    }

    private static string Marke(Stufe stufe) => stufe switch
    {
        Stufe.A => "MUSS",
        Stufe.B => "SOLL",
        _ => "GÜTE",
    };

    private static string Zahl(int n, string einzahl, string mehrzahl) => string.Create(Deutsch, $"{n} {(n == 1 ? einzahl : mehrzahl)}");

    private static string Dauer(TimeSpan zeit) => zeit.TotalSeconds < 60
        ? string.Create(Deutsch, $"{zeit.TotalSeconds:0} s")
        : string.Create(Deutsch, $"{(int)zeit.TotalMinutes}:{zeit.Seconds:00} min");

    private static string Balkenurteil(int anzahl, int grenze, bool best)
    {
        if (anzahl == 0)
        {
            return "keiner";
        }

        if (best)
        {
            return "bestmöglich";
        }

        return grenze > 0 ? $"mind. {grenze} · {anzahl - grenze} offen" : string.Empty;
    }

    private static string Vergleichstext(Planguete jetzt, Planguete gespeichert, DateTime zeit)
    {
        string wann = string.Create(Deutsch, $"{zeit:ddd dd.MM., HH:mm} Uhr");
        if (jetzt.IstBesserAls(gespeichert))
        {
            return $"Besser als der gespeicherte Plan ({wann}).";
        }

        return gespeichert.IstBesserAls(jetzt)
            ? $"Noch nicht so gut wie der gespeicherte Plan ({wann}) – der bleibt erhalten."
            : $"Gleich gut wie der gespeicherte Plan ({wann}).";
    }

    private static string Kurz(Planguete g) => string.Join(", ", new[]
    {
        g.Harte > 0 ? Zahl(g.Harte, "Spiel ohne Termin", "Spiele ohne Termin") : null,
        g.A > 0 ? Zahl(g.A, "Muss-Verstoß", "Muss-Verstöße") : "Pflicht erfüllt",
        g.Wuensche > 0 ? Zahl(g.Wuensche, "Wunsch offen", "Wünsche offen") : "alle Wünsche erfüllt",
    }.OfType<string>());

    private static string HinweisBilden(Schnellstand? stand, (IReadOnlyList<Spiel> Plan, Planguete Guete)? gesamt, TimeSpan ohneVerbesserung)
    {
        if (gesamt is null || (stand is not null && stand.Phase == 1 && gesamt.Value.Guete.Harte == 0 && stand.Bester is null))
        {
            return "Erst die Pflicht, dann die Wünsche der Vereine …";
        }

        if (gesamt is { } b && b.Guete.Harte > 0)
        {
            return "Der Plan hat noch Spiele ohne gültigen Termin.";
        }

        return ohneVerbesserung.TotalSeconds > StillstandHinweisSekunden
            ? "Weiterrechnen bringt kaum noch etwas – Sie können den Plan übernehmen."
            : "Der Plan ist schon spielbar. Er wird weiter geglättet.";
    }

    /// <summary>Die Probleme als Zeilen (Muss, Soll, Feinschliff).</summary>
    private static List<Normalproblem> Zeilen(IEnumerable<Planproblem> liste) =>
        [.. liste.Select(p => new Normalproblem(Marke(p.Stufe), p.Stufe, p.Wer, p.Was))];

    /// <summary>
    /// Die Balken je Stufe: Muss und Soll alle Kriterien mit Verstößen (jetzt oder im gespeicherten Plan), der Feinschliff die
    /// mit der größten Wirkung auf die Güte. Die Grenze eines Kriteriums: die Untergrenze aus HiGHS (für das Kriterium allein);
    /// ist die Summe der Stufe bewiesen erreicht, ist jedes Kriterium der Stufe bestmöglich.
    /// </summary>
    private static List<Normalgruppe> GruppenBilden(Planguete guete, IReadOnlyList<Kriteriumsstatus>? status, int? schrankeA, int? schrankeB, Planguete? vorher, IReadOnlyDictionary<Kostenkriterium, Kriterienwichtigkeit> wichtigkeiten, IRelayCommand<string> wichtigkeitCommand)
    {
        Dictionary<Kostenkriterium, Kriteriumsstatus> je = (status ?? []).ToDictionary(s => s.Kriterium);
        Normalbalken Balken(Kostenkriterium k, Stufe stufe, int maximum, bool einstellbar = false)
        {
            int anzahl = guete.Anzahl[(int)k];
            bool stufeBewiesen = je.TryGetValue(k, out Kriteriumsstatus? st) && st.Stand == Beweisstand.Bewiesen;
            int grenze = stufeBewiesen ? anzahl : Math.Min(anzahl, st?.Untergrenze ?? 0);
            bool best = anzahl == 0 || grenze >= anzahl;
            return einstellbar
                ? new Normalbalken(Massstab.Kriteriumsname(k), anzahl, grenze, vorher?.Anzahl[(int)k], maximum, stufe, Balkenurteil(anzahl, grenze, best), best, k, wichtigkeiten.GetValueOrDefault(k, Kriterienwichtigkeit.Normal), wichtigkeitCommand)
                : new Normalbalken(Massstab.Kriteriumsname(k), anzahl, grenze, vorher?.Anzahl[(int)k], maximum, stufe, Balkenurteil(anzahl, grenze, best), best);
        }

        List<Normalbalken> Stufenbalken(IEnumerable<Kostenkriterium> kriterien, Stufe stufe)
        {
            List<Kostenkriterium> gezeigt = [.. kriterien.Where(k => guete.Anzahl[(int)k] > 0 || (vorher?.Anzahl[(int)k] ?? 0) > 0)];
            int maximum = Math.Max(1, gezeigt.Select(k => Math.Max(guete.Anzahl[(int)k], vorher?.Anzahl[(int)k] ?? 0)).DefaultIfEmpty(0).Max());
            return [.. gezeigt.Select(k => Balken(k, stufe, maximum))];
        }

        string SummenText(int summe, int? schranke, int? summeVorher, bool bewiesenGenau)
        {
            var teile = new List<string>();
            if (schranke is int g && summe <= g)
            {
                teile.Add(bewiesenGenau ? $"bewiesen: mindestens {g} – besser geht es nicht" : $"bewiesen: mindestens {g}");
            }
            else if (schranke is int h)
            {
                teile.Add($"bewiesen: mindestens {h} · noch {summe - h} offen");
            }
            else
            {
                teile.Add("Grenze wird noch berechnet");
            }

            if (summeVorher is int v)
            {
                teile.Add($"gespeichert {v}");
            }

            return string.Join(" · ", teile);
        }

        Kostenkriterium[] feinAlle = [.. Massstab.Gruppen.SelectMany(g => g.Kriterien)];
        Kostenkriterium[] eingestellt = [.. feinAlle.Where(k => wichtigkeiten.GetValueOrDefault(k, Kriterienwichtigkeit.Normal) != Kriterienwichtigkeit.Normal)];
        Kostenkriterium[] fein = [.. eingestellt.Concat(feinAlle.Except(eingestellt)
            .Where(k => guete.Anzahl[(int)k] > 0)
            .OrderByDescending(k => Massstab.Standard.Kriteriumsgewicht(k, guete.Mannschaften) * guete.Anzahl[(int)k])
            .Take(Math.Max(0, FeinschliffHoechstens - eingestellt.Length)))
            .OrderByDescending(k => Massstab.Standard.Kriteriumsgewicht(k, guete.Mannschaften) * guete.Anzahl[(int)k])];
        int feinSumme = Massstab.Gruppen.SelectMany(g => g.Kriterien).Sum(k => guete.Anzahl[(int)k]);
        int? feinVorher = vorher is { } v ? Massstab.Gruppen.SelectMany(g => g.Kriterien).Sum(k => v.Anzahl[(int)k]) : null;
        int feinMax = Math.Max(1, fein.Select(k => Math.Max(guete.Anzahl[(int)k], vorher?.Anzahl[(int)k] ?? 0)).DefaultIfEmpty(0).Max());

        List<Normalbalken> muss = Stufenbalken(Planguete.Pflicht, Stufe.A);
        if (guete.Harte > 0)
        {
            muss.Insert(0, new Normalbalken("Spiele ohne gültigen Termin", guete.Harte, 0, vorher?.Harte, Math.Max(guete.Harte, muss.Count > 0 ? muss[0].Maximum : 1), Stufe.A, $"{guete.Harte} offen", false));
        }

        return
        [
            new("MUSS", Stufe.A, Zahl(guete.A + guete.Harte, "Verstoß", "Verstöße"), SummenText(guete.A, schrankeA, vorher?.A, true), muss, "Alle Pflichtregeln erfüllt."),
            new("SOLL", Stufe.B, Zahl(guete.Wuensche, "Verstoß", "Verstöße"), SummenText(guete.Wuensche, schrankeA is null ? null : schrankeB, vorher?.Wuensche, true), Stufenbalken(Planguete.Wunschkriterien, Stufe.B), "Alle Wünsche der Vereine erfüllt."),
            new("FEINSCHLIFF", Stufe.C, Zahl(feinSumme, "Schönheitsfehler", "Schönheitsfehler"), feinVorher is int fv ? $"gespeichert {fv}" : string.Empty, [.. fein.Select(k => Balken(k, Stufe.C, feinMax, einstellbar: true))], "Keine Punkte im Feinschliff."),
        ];
    }

    private void StartAufbauen(string? mannschaftsmeldung, string? terminwuensche, IReadOnlyList<string> hinweise)
    {
        OnPropertyChanged(nameof(IstGeoeffnet));
        PlanErstellenCommand.NotifyCanExecuteChanged();
        DatenCommand.NotifyCanExecuteChanged();
        EinrichtungPruefenCommand.NotifyCanExecuteChanged();
        if (hauptfenster.Sitzung is not Plansitzung s)
        {
            Dateien = [];
            Pruefung = string.Empty;
            Hinderung = string.Empty;
            Vorhanden = string.Empty;
            return;
        }

        lock (sperre)
        {
            steuerung = new Schnellsteuerung();
            wichtigkeiten.Clear();
        }

        Staffel staffel = s.Staffel;
        string runde = s.Optionen.Rundenplanung switch
        {
            Domain.Optionen.Rundenplanung.Beide => "Vor- und Rückrunde",
            Domain.Optionen.Rundenplanung.Halbrunde => "Halbrunde",
            Domain.Optionen.Rundenplanung.NurVorrunde => "nur Vorrunde",
            Domain.Optionen.Rundenplanung.NurRueckrunde => "nur Rückrunde (die Vorrunde bleibt)",
            _ => "Corona-Rückrunde",
        };
        bool setzliste = staffel.Setzliste is not null || mannschaftsmeldung is not null;
        Dateien =
        [
            new("Terminwünsche (XML)", "notwendig", $"{staffel.Name} · {staffel.Mannschaften.Count} Mannschaften", true),
            new("Terminmeldungen (PDF)", "empfohlen", "Hallen und Adressen für die 60-km-Regel, Bemerkungen der Vereine", terminwuensche is not null),
            new("Mannschaftsmeldung (CSV)", "freiwillig", "Setzliste nach Spielstärke – starke Mannschaften treffen sich nicht zu früh", setzliste),
        ];
        var pruef = new List<string> { "Geprüft: " + runde };
        pruef.AddRange(hinweise);
        if (hauptfenster.ZeigtEinrichtung)
        {
            pruef.Add("Offene Punkte der Einrichtung übernimmt das Programm mit seinen Vorschlägen");
        }

        Pruefung = string.Join(" · ", pruef) + ".";
        Hinderung = hauptfenster.KompaktAktiv
            ? "Diese Klasse spielt in Kompaktspieltagen – sie wird im Expertenmodus geplant."
            : string.Empty;
        Vorhanden = Normalplanung.BesterPlan(s) is { } bester
            ? string.Create(Deutsch, $"Es gibt schon einen Plan vom {bester.Zeit:ddd dd.MM., HH:mm} Uhr: {Kurz(bester.Guete)}.")
            : string.Empty;
    }

    private void EinrichtungPruefen() => EinrichtungAnzeigen = true;

    private void VorhandenenPlanZeigen()
    {
        if (hauptfenster.Sitzung is Plansitzung s && Normalplanung.BesterPlan(s) is { } bester)
        {
            ErgebnisAufbauen(s, bester.Plan, bester.Guete, false, null, null);
            Seite = Normalseite.Ergebnis;
        }
    }

    /// <summary>Startet die Planung: neu (alles auf Anfang, Verlauf leer) oder als „Weiter“ nach einer Pause (Verlauf bleibt).</summary>
    private async Task StartenAsync(bool neu)
    {
        if (hauptfenster.Sitzung is null || (planung is not null))
        {
            return;
        }

        if (neu)
        {
            if (pausiert)
            {
                pausiert = false;
            }
            else
            {
                await hauptfenster.EinrichtungUebernehmenAsync().ConfigureAwait(true);
            }
        }

        if (hauptfenster.Sitzung is not Plansitzung s || hauptfenster.KompaktAktiv)
        {
            StartAufbauen(null, null, []);
            return;
        }

        if (neu)
        {
            vergleich = await Task.Run(() => Normalplanung.Vergleichsplan(s)).ConfigureAwait(true);
            VerlaufZuruecksetzen();
            Reiter = 0;
            ansichtVersion = -1;
            ergebnisQuelle = null;
            Terminplan = null;
            Diagramme = null;
            Gruppen = [];
            Probleme = [];
            Urteil = "Der Plan entsteht";
            UrteilText = "Erst die Pflicht, dann die Wünsche der Vereine – das dauert meist nur Sekunden.";
            UrteilGut = false;
            Hinweis = string.Empty;
            zuletztBesser = TimeSpan.Zero;
            laufuhr.Restart();
        }
        else
        {
            pausiert = false;
            zuletztBesser = laufuhr.Elapsed;
            laufuhr.Start();
        }

        lock (sperre)
        {
            letzterStand = null;
        }

        Normalplanung neuePlanung = Normalplanung.Starten(s, StandMerken, Planeinstellungen, steuerung: steuerung);
        lock (sperre)
        {
            planung = neuePlanung;
        }

        hauptfenster.Oberflaeche.WachHalten(true);
        LaufGeaendert();
        Seite = Normalseite.Planung;
        Laufzeit = neu ? "läuft seit 0 s" : Laufzeit;
        takt = new Timer(_ => Takt(s), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <summary>Merkt sich den neuesten Stand der Planung und den besten Plan über alle Läufe (Hintergrund).</summary>
    private void StandMerken(Schnellstand stand)
    {
        lock (sperre)
        {
            letzterStand = stand;
            if (stand.Bester is not { } b)
            {
                return;
            }

            if (gesamtBester is null || steuerung.Massstab.IstBesser(b.Guete, gesamtBester.Value.Guete))
            {
                gesamtBester = b;
                gesamtStand = stand;
                gezaehlteVerbesserungen++;
            }

            if (gesamtBester is { } g && g.Guete.Harte == 0)
            {
                VerlaufPunktMerken(g.Guete, stand.Status);
            }
        }
    }

    /// <summary>Jede Sekunde (Hintergrund): neuen Stand auswerten, die Anzeige auf der Oberfläche nachziehen.</summary>
    private void Takt(Plansitzung s)
    {
        Schnellstand? stand;
        Schnellstand? bestStand;
        (IReadOnlyList<Spiel> Plan, Planguete Guete)? gesamt;
        VerlaufStand verlaufStand;
        Dictionary<Kostenkriterium, Kriterienwichtigkeit> wichtig;
        Massstab massstab;
        int version;
        lock (sperre)
        {
            stand = letzterStand;
            bestStand = gesamtStand;
            gesamt = gesamtBester;
            if (planung is null && !pausiert)
            {
                return;
            }

            wichtig = new(wichtigkeiten);
            massstab = steuerung.Massstab;
            version = gezaehlteVerbesserungen;

            verlaufStand = VerlaufStandHolen();
        }

        TimeSpan jetzt = laufuhr.Elapsed;
        List<Normalgruppe>? neueGruppen = null;
        List<Normalproblem>? neueProbleme = null;
        (string Urteil, string Text, bool Gut)? neuesUrteil = null;
        int? schrankeA = stand?.SchrankeA ?? bestStand?.SchrankeA;
        int? schrankeB = stand?.SchrankeB ?? bestStand?.SchrankeB;
        if (gesamt is { } bester && (letzteGuete is null || bester.Guete.IstBesserAls(letzteGuete) || !bester.Guete.Anzahl.SequenceEqual(letzteGuete.Anzahl)))
        {
            if (letzteGuete is null || bester.Guete.IstBesserAls(letzteGuete))
            {
                zuletztBesser = jetzt;
            }

            letzteGuete = bester.Guete;
            Planbewertung bewertung = s.Bewerten(bester.Plan);
            neueGruppen = GruppenBilden(bester.Guete, bestStand?.Status ?? stand?.Status, schrankeA, schrankeB, vergleich?.Guete, wichtig, WichtigkeitCommand);
            List<Planproblem> alle = [.. Planprobleme.Erstellen(bewertung, massstab)];
            neueProbleme = Zeilen(alle.Where(p => p.Stufe != Stufe.C).Concat(alle.Where(p => p.Stufe == Stufe.C).Take(3)));
            neuesUrteil = UrteilBilden(bester.Guete, schrankeA, schrankeB);
        }

        bool ruht = pausiert;
        bool ende = !ruht && gesamt is not null && jetzt - zuletztBesser > TimeSpan.FromSeconds(StillstandEndeSekunden);
        string laufText = $"läuft seit {Dauer(jetzt)}" + (gesamt is null ? string.Empty : $" · zuletzt besser vor {Dauer(jetzt - zuletztBesser)}");
        string hinweisText = HinweisBilden(stand, gesamt, jetzt - zuletztBesser);
        hauptfenster.Oberflaeche.AufOberflaeche(() =>
        {
            if (!Laeuft)
            {
                return;
            }

            if (!ruht)
            {
                Laufzeit = laufText;
                Hinweis = hinweisText;
                VerlaufAnzeigen(verlaufStand);
            }

            if (neueGruppen is not null)
            {
                Gruppen = neueGruppen;
            }

            if (neueProbleme is not null)
            {
                Probleme = neueProbleme;
            }

            if (neuesUrteil is { } u)
            {
                (Urteil, UrteilText, UrteilGut) = u;
            }

            if (ende)
            {
                _ = FertigAsync();
            }
            else if (Reiter != 0 && gesamt is not null && version != ansichtVersion)
            {
                _ = PlanungsansichtAktualisierenAsync(s, gesamt.Value.Plan, version, massstab);
            }
        });
    }

    private (string Urteil, string Text, bool Gut) UrteilBilden(Planguete g, int? schrankeA, int? schrankeB)
    {
        string vergleichText = vergleich is { } v ? Vergleichstext(g, v.Guete, v.Zeit) : string.Empty;
        if (g.Harte > 0)
        {
            return ("Noch nicht spielbar", $"{Zahl(g.Harte, "Spiel hat", "Spiele haben")} noch keinen gültigen Termin. {vergleichText}".Trim(), false);
        }

        bool mussBest = schrankeA is int a && g.A <= a;
        bool sollBest = mussBest && schrankeB is int b && g.Wuensche <= b;
        string titel = (mussBest, sollBest) switch
        {
            (true, true) => "Muss und Soll sind bestmöglich",
            (true, false) => "Muss ist bestmöglich",
            _ => "Der Plan ist spielbar",
        };
        string soll = string.Empty;
        if (!sollBest && g.Wuensche > 0)
        {
            soll = schrankeB is int sb && mussBest
                ? $"Bei den Wünschen fehlen noch höchstens {g.Wuensche - sb}. "
                : $"{Zahl(g.Wuensche, "Wunsch ist", "Wünsche sind")} noch nicht erfüllt. ";
        }

        string weiter = sollBest
            ? "Jetzt wird nur noch der Feinschliff geglättet. Sie können den Plan jederzeit übernehmen. "
            : string.Empty;
        return (titel, (soll + weiter + vergleichText).Trim(), mussBest);
    }

    private void PlanungBeenden(Normalplanung p)
    {
        lock (sperre)
        {
            if (ReferenceEquals(planung, p))
            {
                planung = null;
            }
        }

        p.Dispose();
        takt?.Dispose();
        takt = null;
        laufuhr.Stop();
        hauptfenster.Oberflaeche.WachHalten(false);
        LaufGeaendert();
    }

    private void LaufGeaendert()
    {
        OnPropertyChanged(nameof(Laeuft));
        PlanErstellenCommand.NotifyCanExecuteChanged();
        FertigCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        NeuStartenCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(Rechnet));
        OnPropertyChanged(nameof(Pausiert));
        OnPropertyChanged(nameof(PauseText));
        OeffnenCommand.NotifyCanExecuteChanged();
        ErgebnisZeigenCommand.NotifyCanExecuteChanged();
        EinrichtungPruefenCommand.NotifyCanExecuteChanged();
        DatenCommand.NotifyCanExecuteChanged();
        ZurueckCommand.NotifyCanExecuteChanged();
        hauptfenster.NormalAnsichtGeaendert();
    }

    private void ErgebnisAufbauen(Plansitzung s, IReadOnlyList<Spiel> plan, Planguete guete, bool neuNichtBesser, int? schrankeA, int? schrankeB)
    {
        Planbewertung bewertung = s.Bewerten(plan);
        List<Planproblem> alle = [.. Planprobleme.Erstellen(bewertung, steuerung.Massstab)];
        Vereinshinweise = Zeilen(alle.Where(p => p.Stufe != Stufe.C));
        Feinschliffhinweise = Zeilen(alle.Where(p => p.Stufe == Stufe.C).Take(5));
        bool mussBest = schrankeA is int a ? guete.A <= a : guete.A == 0;
        bool sollBest = mussBest && (schrankeB is int b ? guete.Wuensche <= b : guete.Wuensche == 0);
        int fein = Massstab.Gruppen.SelectMany(g => g.Kriterien).Sum(k => guete.Anzahl[(int)k]);
        (FazitTitel, FazitGut) = (guete.Harte, mussBest, sollBest) switch
        {
            (> 0, _, _) => ("Der Plan hat noch Fehler", false),
            (_, true, true) => ("Der Plan ist fertig und sehr gut", true),
            (_, true, false) => ("Der Plan ist fertig", true),
            _ => ("Der Plan ist fertig – mit Pflichtverstößen", false),
        };
        var teile = new List<string>();
        if (guete.Harte > 0)
        {
            teile.Add(Zahl(guete.Harte, "Spiel ohne gültigen Termin", "Spiele ohne gültigen Termin"));
        }

        string unvermeidbar = mussBest && schrankeA is not null ? ", nicht vermeidbar" : string.Empty;
        string mehrNicht = sollBest && schrankeB is not null ? " – mehr geht nicht" : string.Empty;
        teile.Add(guete.A == 0 ? "Alle Muss-Regeln erfüllt" : Zahl(guete.A, "Muss-Verstoß", "Muss-Verstöße") + unvermeidbar);
        teile.Add(guete.Wuensche == 0 ? "alle Wünsche der Vereine erfüllt" : Zahl(guete.Wuensche, "Wunsch", "Wünsche") + " nicht erfüllt" + mehrNicht);
        teile.Add(Zahl(fein, "Schönheitsfehler", "Schönheitsfehler") + " im Feinschliff");
        FazitText = string.Join(" · ", teile);
        FazitVergleich = neuNichtBesser && Normalplanung.BesterPlan(s) is { } bisher
            ? string.Create(Deutsch, $"Der neue Lauf war nicht besser – der bisher beste Plan vom {bisher.Zeit:ddd dd.MM., HH:mm} Uhr bleibt.")
            : string.Empty;

        GemerkterPlanEintragSuchen(s);
        Reiter = 0;
    }

    private void GemerkterPlanEintragSuchen(Plansitzung s)
    {
        if (s.GemerktePlaene().FirstOrDefault(e => e.Name == Normalplanung.BesterPlanName) is not { } eintrag)
        {
            ergebnisQuelle = null;
            Terminplan = null;
            Diagramme = null;
        }
        else
        {
            ergebnisQuelle = hauptfenster.GemerktenPlanNeuLesen(eintrag);
            if (Terminplan is null || Diagramme is null)
            {
                Terminplan = new TerminplanAnsichtViewModel(hauptfenster, hauptfenster.AnsichtsId()) { Quelle = ergebnisQuelle };
                Diagramme = new DiagrammAnsichtViewModel(hauptfenster, hauptfenster.AnsichtsId()) { Quelle = ergebnisQuelle };
            }
            else
            {
                Terminplan.Quelle = ergebnisQuelle;
                Terminplan.Aktualisieren();
                Diagramme.Quelle = ergebnisQuelle;
                Diagramme.Aktualisieren();
            }
        }

        ExportierenCommand.NotifyCanExecuteChanged();
        DruckenCommand.NotifyCanExecuteChanged();
    }
}
