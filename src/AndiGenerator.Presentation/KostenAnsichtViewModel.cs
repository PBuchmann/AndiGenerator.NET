// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Kostenansicht (Original: Tab „Abweichungen“): Plan-Kostenarten als Kacheln und die Kosten je Mannschaft und Kostenart
/// als Matrix. Ein Klick auf eine Kachel, einen Spaltenkopf, eine Mannschaft oder eine Zelle zeigt rechts die Details; dort
/// wird die zugehörige Gewichtung direkt geändert (im Original je Klick ein Dialog).
/// </summary>
public sealed class KostenAnsichtViewModel : AnsichtViewModel
{
    private IReadOnlyList<Kennzahl> kennzahlen = [];
    private Kostentabelle tabelle = Kostentabelle.Leer;
    private Kostenmatrix matrix = Kostenmatrix.Leer;
    private Kostendetail? detail;
    private Kostenauswahl? auswahl;
    private bool zeigtVerstoesse;
    private bool leereZeigen;
    private Planstand? angezeigt;

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="hauptfenster">Hauptfenster.</param>
    /// <param name="id">Eindeutige Kennung im Layout.</param>
    public KostenAnsichtViewModel(HauptfensterViewModel hauptfenster, string id)
        : base(hauptfenster, "Kosten", id)
    {
        GewichtungAendernCommand = new AsyncRelayCommand<Kostenfeld>(GewichtungAendernAsync);
        ZelleWaehlenCommand = new RelayCommand<Matrixzelle>(z => Waehlen(z is null ? null : new Kostenauswahl(Kostenauswahlart.Zelle, z.Mannschaft, z.Kostenart, string.Empty)));
        SpalteWaehlenCommand = new RelayCommand<Matrixspalte>(sp => Waehlen(sp is null ? null : new Kostenauswahl(Kostenauswahlart.Kostenart, string.Empty, sp.Kostenart, string.Empty)));
        AnteilWaehlenCommand = new RelayCommand<Kostenanteil>(a => Waehlen(a is null ? null : new Kostenauswahl(Kostenauswahlart.Kostenart, string.Empty, a.Kostenart, string.Empty)));
        ZeileWaehlenCommand = new RelayCommand<Matrixzeile>(z => Waehlen(z is null ? null : new Kostenauswahl(Kostenauswahlart.Mannschaft, z.Name, default, string.Empty)));
        KennzahlWaehlenCommand = new RelayCommand<Kennzahl>(k => Waehlen(k is null ? null : new Kostenauswahl(Kostenauswahlart.Kennzahl, string.Empty, default, k.Name)));
        ZurKostenartCommand = new RelayCommand(() => Waehlen(auswahl is null ? null : auswahl with { Art = Kostenauswahlart.Kostenart }));
        ZurMannschaftCommand = new RelayCommand(() => Waehlen(auswahl is null ? null : auswahl with { Art = Kostenauswahlart.Mannschaft }));
        StufeWaehlenCommand = new RelayCommand<Gewichtungsstufe>(StufeWaehlen);
        DetailSchliessenCommand = new RelayCommand(() => Waehlen(null));
        LeereUmschaltenCommand = new RelayCommand(() => LeereZeigen = !LeereZeigen);
    }

    /// <summary>Holt die Plan-Kostenarten (Gesamtkosten, nicht terminierte Spiele, Spieltage …).</summary>
    public IReadOnlyList<Kennzahl> Kennzahlen
    {
        get => kennzahlen;
        private set => SetProperty(ref kennzahlen, value);
    }

    /// <summary>Holt die Kostentabelle je Mannschaft und Kostenart.</summary>
    public Kostentabelle Tabelle
    {
        get => tabelle;
        private set => SetProperty(ref tabelle, value);
    }

    /// <summary>Holt die Kostenmatrix (Mannschaften × Kostenarten, nach Kosten sortiert).</summary>
    public Kostenmatrix Matrix
    {
        get => matrix;
        private set => SetProperty(ref matrix, value);
    }

    /// <summary>Holt die Details zur Auswahl oder <c>null</c>, solange nichts ausgewählt ist.</summary>
    public Kostendetail? Detail
    {
        get => detail;
        private set
        {
            if (SetProperty(ref detail, value))
            {
                OnPropertyChanged(nameof(HatDetail));
                OnPropertyChanged(nameof(OhneDetail));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es eine Matrix gibt (ein Plan ist angezeigt).</summary>
    public bool HatMatrix => Matrix.Zeilen.Count > 0;

    /// <summary>Holt einen Wert, der angibt, ob Details gezeigt werden.</summary>
    public bool HatDetail => Detail is not null;

    /// <summary>Holt einen Wert, der angibt, ob statt Details der Hinweis zum Anklicken gezeigt wird.</summary>
    public bool OhneDetail => Detail is null && Matrix.Zeilen.Count > 0;

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Zellen Verstöße statt Kosten zeigen.</summary>
    public bool ZeigtVerstoesse
    {
        get => zeigtVerstoesse;
        set
        {
            if (SetProperty(ref zeigtVerstoesse, value))
            {
                OnPropertyChanged(nameof(ZeigtKosten));
                Neuaufbauen();
            }
        }
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Zellen Kosten zeigen (Gegenstück zu <see cref="ZeigtVerstoesse"/>).</summary>
    public bool ZeigtKosten
    {
        get => !ZeigtVerstoesse;
        set => ZeigtVerstoesse = !value;
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob auch Kostenarten ohne Kosten gezeigt werden.</summary>
    public bool LeereZeigen
    {
        get => leereZeigen;
        set
        {
            if (SetProperty(ref leereZeigen, value))
            {
                Neuaufbauen();
            }
        }
    }

    /// <summary>Holt die Beschriftung des Umschalters für Kostenarten ohne Kosten.</summary>
    public string LeereText
    {
        get
        {
            if (Matrix.OhneKosten == 0)
            {
                return "Alle Kostenarten haben Kosten";
            }

            return LeereZeigen ? "Kostenarten ohne Kosten ausblenden" : $"{Matrix.OhneKosten} Kostenarten ohne Kosten einblenden";
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es Kostenarten ohne Kosten gibt (Umschalter aktiv).</summary>
    public bool HatLeere => Matrix.OhneKosten > 0;

    /// <summary>Holt den Befehl „Zelle auswählen“.</summary>
    public IRelayCommand<Matrixzelle> ZelleWaehlenCommand { get; }

    /// <summary>Holt den Befehl „Kostenart auswählen“ (Spaltenkopf).</summary>
    public IRelayCommand<Matrixspalte> SpalteWaehlenCommand { get; }

    /// <summary>Holt den Befehl „Kostenart auswählen“ (Abschnitt im Balken „Wo stecken die Kosten?“).</summary>
    public IRelayCommand<Kostenanteil> AnteilWaehlenCommand { get; }

    /// <summary>Holt den Befehl „Mannschaft auswählen“ (Zeilenkopf).</summary>
    public IRelayCommand<Matrixzeile> ZeileWaehlenCommand { get; }

    /// <summary>Holt den Befehl „Kennzahl auswählen“ (Kachel).</summary>
    public IRelayCommand<Kennzahl> KennzahlWaehlenCommand { get; }

    /// <summary>Holt den Befehl, von einer Zelle zu ihrer Kostenart (alle Mannschaften) zu wechseln.</summary>
    public IRelayCommand ZurKostenartCommand { get; }

    /// <summary>Holt den Befehl, von einer Zelle zu ihrer Mannschaft (alle Kostenarten) zu wechseln.</summary>
    public IRelayCommand ZurMannschaftCommand { get; }

    /// <summary>Holt den Befehl, die Details zu schließen (Auswahl aufheben).</summary>
    public IRelayCommand DetailSchliessenCommand { get; }

    /// <summary>Holt den Befehl, Kostenarten ohne Kosten ein- bzw. auszublenden.</summary>
    public IRelayCommand LeereUmschaltenCommand { get; }

    /// <summary>Holt den Befehl, die Gewichtung der Auswahl auf eine Stufe zu setzen (wirkt sofort).</summary>
    public IRelayCommand<Gewichtungsstufe> StufeWaehlenCommand { get; }

    /// <summary>Holt den Befehl, der die Gewichtung eines angeklickten Felds ändert (Parameter: das Feld).</summary>
    public IAsyncRelayCommand<Kostenfeld> GewichtungAendernCommand { get; }

    /// <inheritdoc/>
    protected override void Anzeigen(Planstand? stand)
    {
        angezeigt = stand;
        Berechnungsoptionen? optionen = Hauptfenster.Optionen;
        Staffel? staffel = Hauptfenster.Staffel;
        if (stand is null || optionen is null || staffel is null)
        {
            Kennzahlen = [];
            Tabelle = Kostentabelle.Leer;
            Neuaufbauen();
            return;
        }

        Kennzahlen = Kostendarstellung.Kennzahlen(stand.Bewertung, optionen);
        Tabelle = Kostendarstellung.Tabelle(stand.Bewertung, optionen, staffel);
        Neuaufbauen();
    }

    private void Waehlen(Kostenauswahl? neu)
    {
        auswahl = neu;
        Neuaufbauen();
    }

    private void StufeWaehlen(Gewichtungsstufe? stufe)
    {
        if (stufe is not null && Detail?.Ziel is Gewichtungsziel ziel)
        {
            // Übernimmt und bewertet alle Ansichten neu; diese baut sich dabei mit der gleichen Auswahl wieder auf.
            Hauptfenster.GewichtungSetzen(ziel, stufe.Wert);
        }
    }

    private void Neuaufbauen()
    {
        Berechnungsoptionen? optionen = Hauptfenster.Optionen;
        Staffel? staffel = Hauptfenster.Staffel;
        if (angezeigt is not Planstand stand || optionen is null || staffel is null)
        {
            Matrix = Kostenmatrix.Leer;
            Detail = null;
        }
        else
        {
            Matrix = Kostenmatrixbau.Bauen(stand.Bewertung, optionen, staffel, LeereZeigen, ZeigtVerstoesse, auswahl);
            Detail = auswahl is null
                ? null
                : Kostenmatrixbau.Detail(stand.Bewertung, optionen, staffel, Kennzahlen, auswahl, (m, art) => Hauptfenster.Zellmeldungen(m, art, angezeigt));
        }

        OnPropertyChanged(nameof(HatMatrix));
        OnPropertyChanged(nameof(OhneDetail));
        OnPropertyChanged(nameof(LeereText));
        OnPropertyChanged(nameof(HatLeere));
    }

    private Task GewichtungAendernAsync(Kostenfeld? feld) =>
        feld?.Ziel is Gewichtungsziel ziel ? Hauptfenster.GewichtungAendernAsync(ziel, angezeigt) : Task.CompletedTask;
}
