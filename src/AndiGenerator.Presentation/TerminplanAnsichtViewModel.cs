// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Terminplanansicht (Original: Tab „Terminplan“) mit den drei Darstellungen des Originals: Spielplan nach Wochen,
/// Mannschaftspläne nach Runden und Mannschaftspläne mit den Spielen der Nachbar- und Vereinsmannschaften am selben Tag.
/// Zu jedem Spiel erscheinen die Hinweise des Originals (Ausweichtermin, Koppelspiel, kein Wunschtermin …).
/// </summary>
public sealed class TerminplanAnsichtViewModel : AnsichtViewModel
{
    private IReadOnlyList<Terminplanzeile> plan = [];
    private IReadOnlyList<Terminzeile> zeilen = [];
    private IReadOnlyList<Terminspiel> spiele = [];
    private IReadOnlyList<Terminwoche> wochen = [];
    private IReadOnlyList<Mannschaftsplan> plaene = [];
    private IReadOnlyList<Terminspiel> ohneTermin = [];
    private Terminspiel? auswahl;
    private string zusammenfassung = string.Empty;
    private int darstellung;

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="hauptfenster">Hauptfenster.</param>
    /// <param name="id">Eindeutige Kennung im Layout.</param>
    public TerminplanAnsichtViewModel(HauptfensterViewModel hauptfenster, string id)
        : base(hauptfenster, "Terminplan", id)
    {
        SpielWaehlenCommand = new RelayCommand<Terminspiel>(s => Auswahl = s);
        AuswahlSchliessenCommand = new RelayCommand(() => Auswahl = null);
    }

    /// <summary>Holt die Namen der Darstellungen (wie die Schaltflächen im Original).</summary>
    public static IReadOnlyList<string> Darstellungen { get; } = ["Spielplan", "Mannschaftspläne", "Mannschaftspläne mit Nachbarmannschaften"];

    /// <summary>Holt oder setzt die Darstellung als Index in <see cref="Darstellungen"/>.</summary>
    public int Darstellung
    {
        get => darstellung;
        set
        {
            if (value >= 0 && SetProperty(ref darstellung, value))
            {
                OnPropertyChanged(nameof(IstSpielplan));
                OnPropertyChanged(nameof(IstMannschaften));
                OnPropertyChanged(nameof(IstMitNachbarn));
                ZeilenBilden();
            }
        }
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob der Spielplan nach Wochen gezeigt wird.</summary>
    public bool IstSpielplan
    {
        get => Darstellung == 0;
        set => DarstellungWaehlen(0, value);
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Mannschaftspläne gezeigt werden.</summary>
    public bool IstMannschaften
    {
        get => Darstellung == 1;
        set => DarstellungWaehlen(1, value);
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Mannschaftspläne mit Nachbarmannschaften gezeigt werden.</summary>
    public bool IstMitNachbarn
    {
        get => Darstellung == 2;
        set => DarstellungWaehlen(2, value);
    }

    /// <summary>Holt die Kalenderwochen mit ihren Spielen (Darstellung „Spielplan“).</summary>
    public IReadOnlyList<Terminwoche> Wochen
    {
        get => wochen;
        private set => SetProperty(ref wochen, value);
    }

    /// <summary>Holt die Mannschaftspläne (Darstellungen „Mannschaftspläne“ mit und ohne Nachbarn).</summary>
    public IReadOnlyList<Mannschaftsplan> Plaene
    {
        get => plaene;
        private set => SetProperty(ref plaene, value);
    }

    /// <summary>Holt die Spiele ohne Termin (gesammelt über allen Darstellungen).</summary>
    public IReadOnlyList<Terminspiel> OhneTermin
    {
        get => ohneTermin;
        private set
        {
            if (SetProperty(ref ohneTermin, value))
            {
                OnPropertyChanged(nameof(HatOhneTermin));
                OnPropertyChanged(nameof(OhneTerminTitel));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es Spiele ohne Termin gibt.</summary>
    public bool HatOhneTermin => OhneTermin.Count > 0;

    /// <summary>Holt die Überschrift der Spiele ohne Termin.</summary>
    public string OhneTerminTitel => OhneTermin.Count == 1 ? "1 Spiel ohne Termin" : OhneTermin.Count + " Spiele ohne Termin";

    /// <summary>Holt das ausgewählte Spiel (Details rechts) oder <c>null</c>.</summary>
    public Terminspiel? Auswahl
    {
        get => auswahl;
        private set
        {
            if (SetProperty(ref auswahl, value))
            {
                OnPropertyChanged(nameof(HatAuswahl));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob ein Spiel ausgewählt ist.</summary>
    public bool HatAuswahl => Auswahl is not null;

    /// <summary>Holt den Befehl „Spiel auswählen“ (Details rechts).</summary>
    public IRelayCommand<Terminspiel> SpielWaehlenCommand { get; }

    /// <summary>Holt den Befehl, die Details zu schließen.</summary>
    public IRelayCommand AuswahlSchliessenCommand { get; }

    /// <summary>Holt die Zeilen der gewählten Darstellung.</summary>
    public IReadOnlyList<Terminzeile> Zeilen
    {
        get => zeilen;
        private set => SetProperty(ref zeilen, value);
    }

    /// <summary>Holt die Zusammenfassung, z. B. <c>132 Spiele, davon 4 ohne Termin</c>.</summary>
    public string Zusammenfassung
    {
        get => zusammenfassung;
        private set => SetProperty(ref zusammenfassung, value);
    }

    /// <inheritdoc/>
    protected override void Anzeigen(Planstand? stand)
    {
        plan = stand is null ? [] : Hauptfenster.Terminplan(stand);
        spiele = Terminplanbau.Spiele(plan);
        OhneTermin = spiele.Where(s => s.Zeit.Length == 0).ToList();
        Auswahl = Auswahl is Terminspiel alt ? spiele.FirstOrDefault(s => s.Heim == alt.Heim && s.Gast == alt.Gast) : null;
        int ohne = plan.Count(z => z.Zeitpunkt is null);
        string text = ohne == 0 ? $"{plan.Count} Spiele" : $"{plan.Count} Spiele, davon {ohne} ohne Termin";
        Zusammenfassung = stand is null ? string.Empty : text;
        ZeilenBilden();
    }

    private void DarstellungWaehlen(int nummer, bool an)
    {
        if (an)
        {
            Darstellung = nummer;
        }
        else if (Darstellung == nummer)
        {
            // Ein Klick auf die schon gewählte Darstellung lässt sie gewählt (Umschalter wie Optionsfelder).
            OnPropertyChanged(nameof(IstSpielplan));
            OnPropertyChanged(nameof(IstMannschaften));
            OnPropertyChanged(nameof(IstMitNachbarn));
        }
    }

    private void ZeilenBilden()
    {
        if (plan.Count == 0 || Hauptfenster.Staffel is not Staffel staffel)
        {
            Zeilen = [];
            Wochen = [];
            Plaene = [];
            return;
        }

        Wochen = Darstellung == 0 ? Terminplanbau.Wochen(plan, spiele) : [];
        Plaene = Darstellung == 0 ? [] : Terminplanbau.Mannschaftsplaene(plan, spiele, staffel, mitNachbarn: Darstellung == 2);

        Zeilen = Darstellung switch
        {
            1 => Terminzeilen.Mannschaftsplaene(plan, staffel, mitNachbarn: false),
            2 => Terminzeilen.Mannschaftsplaene(plan, staffel, mitNachbarn: true),
            _ => Terminzeilen.Spielplan(plan),
        };
    }
}
