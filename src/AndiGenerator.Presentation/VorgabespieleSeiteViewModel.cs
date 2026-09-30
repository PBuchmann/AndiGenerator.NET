// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Seite „Manuell festgelegte Begegnungen“ (Original <c>TDialogPanelPredefinedGames</c>). Wie im Original wirken Neu,
/// Bearbeiten und Löschen sofort auf die Daten; beim Verlassen der Seite gibt es nichts mehr zu speichern.
/// </summary>
public sealed class VorgabespieleSeiteViewModel : DatenSeiteViewModel
{
    private readonly IOberflaeche oberflaeche;
    private IReadOnlyList<VorgabespielZeile> zeilen = [];
    private VorgabespielZeile? auswahl;

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche für Dialog und Rückfrage.</param>
    public VorgabespieleSeiteViewModel(Datenbearbeitung bearbeitung, IOberflaeche oberflaeche)
        : base(bearbeitung, "Manuell festgelegte Begegnungen")
    {
        ArgumentNullException.ThrowIfNull(oberflaeche);
        this.oberflaeche = oberflaeche;
        NeuCommand = new AsyncRelayCommand(NeuAsync);
        BearbeitenCommand = new AsyncRelayCommand(BearbeitenAsync, () => Auswahl is not null);
        LoeschenCommand = new AsyncRelayCommand(LoeschenAsync, () => Auswahl is not null);
        Laden();
    }

    /// <summary>Holt die Begegnungen, nach Termin sortiert.</summary>
    public IReadOnlyList<VorgabespielZeile> Zeilen
    {
        get => zeilen;
        private set => SetProperty(ref zeilen, value);
    }

    /// <summary>Holt oder setzt die gewählte Begegnung.</summary>
    public VorgabespielZeile? Auswahl
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

    /// <summary>Holt den Befehl „Neue Begegnung…“.</summary>
    public IAsyncRelayCommand NeuCommand { get; }

    /// <summary>Holt den Befehl „Begegnung bearbeiten…“ (auch per Doppelklick).</summary>
    public IAsyncRelayCommand BearbeitenCommand { get; }

    /// <summary>Holt den Befehl „Begegnung löschen…“.</summary>
    public IAsyncRelayCommand LoeschenCommand { get; }

    /// <inheritdoc/>
    public override bool IstStandard => Bearbeitung.VorgegebeneSpieleSindStandard;

    /// <inheritdoc/>
    public override void Laden()
    {
        Zeilen = Bearbeitung.VorgegebeneSpiele().Select(s => new VorgabespielZeile(s)).ToList();
        Auswahl = null;
    }

    /// <inheritdoc/>
    public override void Speichern()
    {
        // Wie im Original (leeres FormToData): Änderungen stehen bereits in den Daten.
    }

    /// <inheritdoc/>
    public override void Standard()
    {
        Bearbeitung.VorgegebeneSpieleZuruecksetzen();
        Laden();
        AenderungMelden();
    }

    private async Task NeuAsync()
    {
        var dialog = new VorgabespieldialogViewModel(
            Bearbeitung.Mannschaftsnamen(),
            null,
            Bearbeitung.Ligadaten.Beginn,
            s => Bearbeitung.VorgegebenesSpielPruefen(null, s));
        if (await oberflaeche.VorgabespielBearbeitenAsync(dialog))
        {
            Bearbeitung.VorgegebenesSpielSpeichern(null, dialog.Ergebnis);
            Laden();
            AenderungMelden();
        }
    }

    private async Task BearbeitenAsync()
    {
        if (Auswahl is not VorgabespielZeile zeile)
        {
            return;
        }

        int index = zeile.Spiel.Index;
        var dialog = new VorgabespieldialogViewModel(
            Bearbeitung.Mannschaftsnamen(),
            Bearbeitung.VorgegebenesSpiel(index),
            Bearbeitung.Ligadaten.Beginn,
            s => Bearbeitung.VorgegebenesSpielPruefen(index, s));
        if (await oberflaeche.VorgabespielBearbeitenAsync(dialog))
        {
            Bearbeitung.VorgegebenesSpielSpeichern(index, dialog.Ergebnis);
            Laden();
            AenderungMelden();
        }
    }

    private async Task LoeschenAsync()
    {
        if (Auswahl is VorgabespielZeile zeile && await oberflaeche.FragenAsync("Begegnung löschen", "Möchten Sie das Spiel wirklich löschen?"))
        {
            Bearbeitung.VorgegebenesSpielLoeschen(zeile.Spiel.Index);
            Laden();
            AenderungMelden();
        }
    }
}
