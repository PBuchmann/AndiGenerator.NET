// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Wunschtermine einer Mannschaft (Original <c>TDialogPanelHomeDaysDetail</c>): Wochenübersicht mit einem Kurztext je Tag,
/// Bearbeiten per Doppelklick oder Kontextmenü im Dialog „Wunschtermin“.
/// </summary>
public sealed class WunschterminDetailViewModel : DatenSeiteViewModel
{
    private readonly string mannschaft;
    private readonly IOberflaeche oberflaeche;
    private IReadOnlyList<WunschterminZeile> wochen = [];

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche für den Dialog.</param>
    public WunschterminDetailViewModel(Datenbearbeitung bearbeitung, string mannschaft, IOberflaeche oberflaeche)
        : base(bearbeitung, "Wunschtermine")
    {
        ArgumentNullException.ThrowIfNull(oberflaeche);
        this.mannschaft = mannschaft;
        this.oberflaeche = oberflaeche;
        BearbeitenCommand = new AsyncRelayCommand<WunschterminZelle>(BearbeitenAsync);
        LoeschenCommand = new RelayCommand<WunschterminZelle>(Loeschen, z => z?.HatTermin == true);
        Laden();
    }

    /// <summary>Holt die Wochentage für die Kopfzeile.</summary>
    public static IReadOnlyList<string> Wochentage { get; } = ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];

    /// <summary>Holt die Wochen.</summary>
    public IReadOnlyList<WunschterminZeile> Wochen
    {
        get => wochen;
        private set => SetProperty(ref wochen, value);
    }

    /// <summary>Holt den Befehl „Bearbeiten…“ für einen Tag (auch per Doppelklick).</summary>
    public IAsyncRelayCommand<WunschterminZelle> BearbeitenCommand { get; }

    /// <summary>Holt den Befehl „Löschen“ für einen Tag.</summary>
    public IRelayCommand<WunschterminZelle> LoeschenCommand { get; }

    /// <inheritdoc/>
    public override bool IstStandard => Bearbeitung.WunschtermineSindStandard(mannschaft, Werte());

    /// <inheritdoc/>
    public override void Laden() => Anzeigen(Bearbeitung.Wunschtermine(mannschaft));

    /// <inheritdoc/>
    public override void Speichern() => Bearbeitung.WunschtermineSpeichern(mannschaft, Werte());

    /// <inheritdoc/>
    public override string? Pruefen() => Datenbearbeitung.WunschterminePruefen(Werte());

    /// <inheritdoc/>
    public override void Standard()
    {
        // Wie im Original: die Termine des click-TT-Stands anzeigen; gespeichert wird beim Verlassen der Seite.
        if (Bearbeitung.WunschtermineStandard(mannschaft) is IReadOnlyList<Wunschterminwoche> standard)
        {
            Anzeigen(standard);
            AenderungMelden();
        }
    }

    private static void Loeschen(WunschterminZelle? zelle)
    {
        if (zelle is not null)
        {
            zelle.Text = string.Empty;
        }
    }

    private List<Wunschterminwoche> Werte() => Wochen.Select(w => w.Wert()).ToList();

    private void Anzeigen(IReadOnlyList<Wunschterminwoche> daten) =>
        Wochen = daten.Select(w => new WunschterminZeile(w, ZelleGeaendert)).ToList();

    private void ZelleGeaendert()
    {
        LoeschenCommand.NotifyCanExecuteChanged();
        AenderungMelden();
    }

    private async Task BearbeitenAsync(WunschterminZelle? zelle)
    {
        if (zelle is null)
        {
            return;
        }

        var dialog = new WunschterminDialogViewModel(zelle.Tag, zelle.Text);
        if (await oberflaeche.WunschterminBearbeitenAsync(dialog))
        {
            zelle.Text = dialog.Ergebnis;
        }
    }
}
