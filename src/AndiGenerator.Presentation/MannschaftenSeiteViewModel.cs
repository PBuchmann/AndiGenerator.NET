// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>Seite „Mannschaften“ (Original <c>TDialogPanelTeams</c>): Liste mit Neu, Bearbeiten und Löschen.</summary>
public sealed class MannschaftenSeiteViewModel : DatenSeiteViewModel
{
    private readonly IOberflaeche oberflaeche;
    private IReadOnlyList<string> namen = [];
    private string? auswahl;

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche für die Dialoge.</param>
    public MannschaftenSeiteViewModel(Datenbearbeitung bearbeitung, IOberflaeche oberflaeche)
        : base(bearbeitung, "Mannschaften")
    {
        ArgumentNullException.ThrowIfNull(oberflaeche);
        this.oberflaeche = oberflaeche;
        NeuCommand = new AsyncRelayCommand(NeuAsync);
        BearbeitenCommand = new AsyncRelayCommand(BearbeitenAsync, () => Auswahl is not null);
        LoeschenCommand = new AsyncRelayCommand(LoeschenAsync, () => Auswahl is not null);
        Laden();
    }

    /// <summary>Holt die Mannschaftsnamen (sortiert).</summary>
    public IReadOnlyList<string> Namen
    {
        get => namen;
        private set => SetProperty(ref namen, value);
    }

    /// <summary>Holt oder setzt die gewählte Mannschaft.</summary>
    public string? Auswahl
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

    /// <summary>Holt den Befehl „Neu“.</summary>
    public IAsyncRelayCommand NeuCommand { get; }

    /// <summary>Holt den Befehl „Bearbeiten“ (auch per Doppelklick).</summary>
    public IAsyncRelayCommand BearbeitenCommand { get; }

    /// <summary>Holt den Befehl „Löschen“.</summary>
    public IAsyncRelayCommand LoeschenCommand { get; }

    /// <inheritdoc/>
    public override bool IstStandard => Bearbeitung.MannschaftenSindStandard;

    /// <inheritdoc/>
    public override void Laden()
    {
        // Wie im Original bleibt die Position in der Liste erhalten.
        int index = Auswahl is null ? -1 : Namen.ToList().IndexOf(Auswahl);
        Namen = Bearbeitung.Mannschaftsnamen();
        Auswahl = index >= 0 && index < Namen.Count ? Namen[index] : null;
    }

    /// <inheritdoc/>
    public override void Speichern()
    {
        // Änderungen stehen sofort in der Arbeitskopie (Original: FormToData leer).
    }

    /// <inheritdoc/>
    public override void Standard()
    {
        Bearbeitung.MannschaftenZuruecksetzen();
        Laden();
        AenderungMelden();
    }

    private async Task NeuAsync()
    {
        var dialog = new MannschaftsdialogViewModel("Mannschaft", Mannschaftsdaten.Neu, d => Bearbeitung.MannschaftPruefen(null, d));
        if (await oberflaeche.MannschaftBearbeitenAsync(dialog))
        {
            Bearbeitung.MannschaftSpeichern(null, dialog.Ergebnis);
            Laden();
            AenderungMelden();
        }
    }

    private async Task BearbeitenAsync()
    {
        if (Auswahl is not string bisher)
        {
            return;
        }

        var dialog = new MannschaftsdialogViewModel("Mannschaft", Bearbeitung.Mannschaft(bisher), d => Bearbeitung.MannschaftPruefen(bisher, d));
        if (await oberflaeche.MannschaftBearbeitenAsync(dialog))
        {
            Bearbeitung.MannschaftSpeichern(bisher, dialog.Ergebnis);
            Laden();
            AenderungMelden();
        }
    }

    private async Task LoeschenAsync()
    {
        if (Auswahl is string name && await oberflaeche.FragenAsync("Mannschaft löschen", $"Möchten Sie die Mannschaft {name} wirklich löschen?"))
        {
            Bearbeitung.MannschaftLoeschen(name);
            Laden();
            AenderungMelden();
        }
    }
}
