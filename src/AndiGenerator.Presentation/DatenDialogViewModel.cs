// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Dialog „Spielplandaten bearbeiten“ (Original <c>TDialogForMultiplePanel</c>): links die Seiten, rechts die gewählte
/// Seite. Ein Seitenwechsel ist nur mit gültigen Eingaben möglich; beim Verlassen schreibt die Seite in die Arbeitskopie.
/// „Standard wiederherstellen“ wirkt wie im Original nur auf die aktuelle Seite und ist nur bei click-TT-Dateien sichtbar.
/// </summary>
public sealed class DatenDialogViewModel : ObservableObject
{
    private DatenSeiteViewModel seite;
    private string meldung = string.Empty;

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="seiten">Die Seiten in der Reihenfolge des Originals.</param>
    public DatenDialogViewModel(Datenbearbeitung bearbeitung, IReadOnlyList<DatenSeiteViewModel> seiten)
    {
        ArgumentNullException.ThrowIfNull(bearbeitung);
        ArgumentNullException.ThrowIfNull(seiten);
        if (seiten.Count == 0)
        {
            throw new ArgumentException("Der Dialog braucht mindestens eine Seite.", nameof(seiten));
        }

        Bearbeitung = bearbeitung;
        Seiten = seiten;
        seite = seiten[0];
        StandardCommand = new RelayCommand(() => seite.Standard(), () => !seite.IstStandard);
        foreach (DatenSeiteViewModel s in seiten)
        {
            s.Geaendert += (_, _) => StandardCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Holt die Seiten.</summary>
    public IReadOnlyList<DatenSeiteViewModel> Seiten { get; }

    /// <summary>Holt oder setzt die angezeigte Seite; der Wechsel unterbleibt bei ungültigen Eingaben.</summary>
    public DatenSeiteViewModel? Seite
    {
        get => seite;
        set
        {
            if (value is null || ReferenceEquals(value, seite))
            {
                return;
            }

            if (seite.Pruefen() is string fehler)
            {
                Meldung = fehler;

                // Die Auswahl in der Liste springt zurück.
                OnPropertyChanged(nameof(Seite));
                return;
            }

            seite.Speichern();
            value.Laden();
            Meldung = string.Empty;
            SetProperty(ref seite, value);
            StandardCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Holt die Meldung der letzten Prüfung (Original <c>ShowMessage</c> in <c>CheckData</c>).</summary>
    public string Meldung
    {
        get => meldung;
        private set
        {
            if (SetProperty(ref meldung, value))
            {
                OnPropertyChanged(nameof(HatMeldung));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob eine Meldung angezeigt wird.</summary>
    public bool HatMeldung => Meldung.Length > 0;

    /// <summary>Holt einen Wert, der angibt, ob „Standard wiederherstellen“ angeboten wird (nur bei click-TT-Dateien).</summary>
    public bool StandardSichtbar => Bearbeitung.HatStandard;

    /// <summary>Holt den Befehl „Standard wiederherstellen“ (nur für die aktuelle Seite).</summary>
    public IRelayCommand StandardCommand { get; }

    /// <summary>Holt die Arbeitskopie.</summary>
    public Datenbearbeitung Bearbeitung { get; }

    /// <summary>Erzeugt alle Seiten des Dialogs in der Reihenfolge des Originals.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche für die Bearbeitungsdialoge.</param>
    /// <returns>Die Seiten.</returns>
    public static IReadOnlyList<DatenSeiteViewModel> AlleSeiten(Datenbearbeitung bearbeitung, IOberflaeche oberflaeche) =>
    [
        new LigadatenSeiteViewModel(bearbeitung),
        new MannschaftenSeiteViewModel(bearbeitung, oberflaeche),
        new SetzlistenSeiteViewModel(bearbeitung),
        new SpiellokaleSeiteViewModel(bearbeitung, oberflaeche),
        new HeimkoppelnSeiteViewModel(bearbeitung),
        new AuswaertskoppelnSeiteViewModel(bearbeitung, oberflaeche),
        new WunschtermineSeiteViewModel(bearbeitung, oberflaeche),
        new NachbarmannschaftenSeiteViewModel(bearbeitung, oberflaeche),
        new WochenendeSeiteViewModel(bearbeitung),
        new SpielfreieTageSeiteViewModel(bearbeitung),
        new PflichtspieltageSeiteViewModel(bearbeitung, oberflaeche),
        new VorgabespieleSeiteViewModel(bearbeitung, oberflaeche),
        new HeimrechtSeiteViewModel(bearbeitung),
    ];

    /// <summary>Prüft die aktuelle Seite und schreibt sie in die Arbeitskopie (OK im Original).</summary>
    /// <returns><c>true</c>, wenn der Dialog geschlossen werden darf.</returns>
    public bool Bestaetigen()
    {
        if (seite.Pruefen() is string fehler)
        {
            Meldung = fehler;
            return false;
        }

        seite.Speichern();
        return true;
    }
}
