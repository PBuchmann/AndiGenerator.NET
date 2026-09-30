// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Seite „Pflichtspieltage“ (Original <c>TDialogPanelMandatoryDays</c>): Zeiträume, in denen jede Mannschaft mindestens eine
/// bestimmte Anzahl Spiele machen muss. Die Anzahl ist direkt in der Liste wählbar; Neu, Bearbeiten und Löschen wie im
/// Kontextmenü des Originals.
/// </summary>
public sealed class PflichtspieltageSeiteViewModel : DatenSeiteViewModel
{
    private readonly IOberflaeche oberflaeche;
    private IReadOnlyList<PflichtspielzeitZeile> zeilen = [];
    private PflichtspielzeitZeile? auswahl;

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche für den Dialog.</param>
    public PflichtspieltageSeiteViewModel(Datenbearbeitung bearbeitung, IOberflaeche oberflaeche)
        : base(bearbeitung, "Pflichtspieltage")
    {
        ArgumentNullException.ThrowIfNull(oberflaeche);
        this.oberflaeche = oberflaeche;
        NeuCommand = new AsyncRelayCommand(NeuAsync);
        BearbeitenCommand = new AsyncRelayCommand(BearbeitenAsync, () => Auswahl is not null);
        LoeschenCommand = new RelayCommand(Loeschen, () => Auswahl is not null);
        Laden();
    }

    /// <summary>Holt den Erklärungstext des Originals.</summary>
    public static string Erklaerung => "In diesen Zeiträumen müssen die Mannschaften mindestens eine bestimmte Anzahl Spiele machen";

    /// <summary>Holt die Zeiträume.</summary>
    public IReadOnlyList<PflichtspielzeitZeile> Zeilen
    {
        get => zeilen;
        private set
        {
            if (SetProperty(ref zeilen, value))
            {
                OnPropertyChanged(nameof(Keine));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es keine Zeiträume gibt.</summary>
    public bool Keine => Zeilen.Count == 0;

    /// <summary>Holt oder setzt den gewählten Zeitraum.</summary>
    public PflichtspielzeitZeile? Auswahl
    {
        get => auswahl;
        set
        {
            if (SetProperty(ref auswahl, value))
            {
                BearbeitenCommand.NotifyCanExecuteChanged();
                LoeschenCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Holt den Befehl „Neu…“.</summary>
    public IAsyncRelayCommand NeuCommand { get; }

    /// <summary>Holt den Befehl „Bearbeiten…“ (auch per Doppelklick).</summary>
    public IAsyncRelayCommand BearbeitenCommand { get; }

    /// <summary>Holt den Befehl „Löschen“.</summary>
    public IRelayCommand LoeschenCommand { get; }

    /// <inheritdoc/>
    public override bool IstStandard =>
        Bearbeitung.PflichtspielzeitenStandard() is not IReadOnlyList<Pflichtspielzeit> standard
        || standard.SequenceEqual(Sortiert(Werte()));

    /// <inheritdoc/>
    public override void Laden() => Anzeigen(Bearbeitung.Pflichtspielzeiten());

    /// <inheritdoc/>
    public override void Speichern() => Bearbeitung.PflichtspielzeitenSpeichern(Werte());

    /// <inheritdoc/>
    public override void Standard()
    {
        // Wie im Original: die Zeiträume des click-TT-Stands anzeigen; gespeichert wird beim Verlassen der Seite.
        Anzeigen(Bearbeitung.PflichtspielzeitenStandard() ?? []);
        AenderungMelden();
    }

    private static IEnumerable<Pflichtspielzeit> Sortiert(IEnumerable<Pflichtspielzeit> zeiten) =>
        zeiten.OrderBy(z => z.Von).ThenBy(z => z.Bis).ThenBy(z => z.Anzahl);

    private List<Pflichtspielzeit> Werte() => Zeilen.Select(z => z.Wert).ToList();

    private void Anzeigen(IEnumerable<Pflichtspielzeit> zeiten)
    {
        Zeilen = zeiten.Select(z => new PflichtspielzeitZeile(z, AenderungMelden)).ToList();
        Auswahl = null;
    }

    private async Task NeuAsync()
    {
        DateOnly heute = DateOnly.FromDateTime(DateTime.Today);
        List<Pflichtspielzeit> bisher = Werte();
        var dialog = new PflichtspielzeitdialogViewModel(new Pflichtspielzeit(heute, heute, 1), z => z.Pruefen(bisher));
        if (await oberflaeche.PflichtspielzeitBearbeitenAsync(dialog))
        {
            // Wie im Original wird der neue Zeitraum angehängt; sortiert wird beim nächsten Lesen.
            Anzeigen([.. bisher, dialog.Ergebnis]);
            AenderungMelden();
        }
    }

    private async Task BearbeitenAsync()
    {
        if (Auswahl is not PflichtspielzeitZeile zeile)
        {
            return;
        }

        List<Pflichtspielzeit> andere = Zeilen.Where(z => !ReferenceEquals(z, zeile)).Select(z => z.Wert).ToList();
        var dialog = new PflichtspielzeitdialogViewModel(zeile.Wert, z => z.Pruefen(andere));
        if (await oberflaeche.PflichtspielzeitBearbeitenAsync(dialog))
        {
            Anzeigen(Zeilen.Select(z => ReferenceEquals(z, zeile) ? dialog.Ergebnis : z.Wert).ToList());
            AenderungMelden();
        }
    }

    private void Loeschen()
    {
        if (Auswahl is PflichtspielzeitZeile zeile)
        {
            Anzeigen(Zeilen.Where(z => !ReferenceEquals(z, zeile)).Select(z => z.Wert).ToList());
            AenderungMelden();
        }
    }
}
