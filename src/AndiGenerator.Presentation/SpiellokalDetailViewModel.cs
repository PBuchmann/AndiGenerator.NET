// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Spiellokale einer Mannschaft (Original <c>TDialogPanelLocationsCompactDetail</c>): Standardspiellokal, Spiellokal je
/// Heimspieltermin und je Nachbarmannschaft.
/// </summary>
public sealed class SpiellokalDetailViewModel : DatenSeiteViewModel
{
    private readonly string mannschaft;
    private readonly IOberflaeche oberflaeche;
    private string standardlokal = string.Empty;
    private IReadOnlyList<SpiellokalZeile> heimtage = [];
    private IReadOnlyList<SpiellokalZeile> nachbarn = [];

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche für den Dialog „Nachbarmannschaft“.</param>
    public SpiellokalDetailViewModel(Datenbearbeitung bearbeitung, string mannschaft, IOberflaeche oberflaeche)
        : base(bearbeitung, "Spiellokale")
    {
        ArgumentNullException.ThrowIfNull(oberflaeche);
        this.mannschaft = mannschaft;
        this.oberflaeche = oberflaeche;
        NachbarBearbeitenCommand = new AsyncRelayCommand<SpiellokalZeile>(NachbarBearbeitenAsync);
        Laden();
    }

    /// <summary>Holt die wählbaren Spiellokale.</summary>
    public static IReadOnlyList<string> Spiellokale => Mannschaftsdaten.Spiellokale;

    /// <summary>Holt oder setzt das Standardspiellokal.</summary>
    public string? Standardlokal
    {
        get => standardlokal;
        set
        {
            if (SetProperty(ref standardlokal, value ?? string.Empty))
            {
                AenderungMelden();
            }
        }
    }

    /// <summary>Holt die Spiellokale an den Heimspielterminen.</summary>
    public IReadOnlyList<SpiellokalZeile> Heimtage
    {
        get => heimtage;
        private set => SetProperty(ref heimtage, value);
    }

    /// <summary>Holt die Spiellokale der Nachbarmannschaften.</summary>
    public IReadOnlyList<SpiellokalZeile> Nachbarn
    {
        get => nachbarn;
        private set => SetProperty(ref nachbarn, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob es keine Heimspieltermine gibt.</summary>
    public bool KeineHeimtage => Heimtage.Count == 0;

    /// <summary>Holt einen Wert, der angibt, ob es keine Nachbarmannschaften gibt.</summary>
    public bool KeineNachbarn => Nachbarn.Count == 0;

    /// <summary>Holt den Befehl „Nachbarmannschaft bearbeiten“ (Doppelklick auf eine Nachbarmannschaft wie im Original).</summary>
    public IAsyncRelayCommand<SpiellokalZeile> NachbarBearbeitenCommand { get; }

    /// <inheritdoc/>
    public override bool IstStandard => Werte().IstStandard;

    /// <inheritdoc/>
    public override void Laden()
    {
        Spiellokaldaten daten = Bearbeitung.SpiellokaleLesen(mannschaft);
        standardlokal = daten.Standardlokal;
        OnPropertyChanged(nameof(Standardlokal));
        Heimtage = daten.Heimtage
            .Select(h => new SpiellokalZeile(Datumsanzeige.Termin(h.Termin), h.Spiellokal, AenderungMelden))
            .ToList();
        Nachbarn = daten.Nachbarn.Select(n => new SpiellokalZeile(n.Bezeichnung, n.Spiellokal, AenderungMelden)).ToList();
        OnPropertyChanged(nameof(KeineHeimtage));
        OnPropertyChanged(nameof(KeineNachbarn));
    }

    /// <inheritdoc/>
    public override void Speichern() => Bearbeitung.SpiellokaleSpeichern(mannschaft, Werte());

    /// <inheritdoc/>
    public override void Standard()
    {
        // Wie im Original: alles leer und sofort übernehmen.
        Standardlokal = string.Empty;
        foreach (SpiellokalZeile zeile in Heimtage.Concat(Nachbarn))
        {
            zeile.Spiellokal = string.Empty;
        }

        Speichern();
        Laden();
        AenderungMelden();
    }

    private Spiellokaldaten Werte()
    {
        Spiellokaldaten gelesen = Bearbeitung.SpiellokaleLesen(mannschaft);
        return new Spiellokaldaten(
            standardlokal,
            gelesen.Heimtage.Select((h, i) => (h.Termin, i < Heimtage.Count ? Heimtage[i].Wert : h.Spiellokal)).ToList(),
            gelesen.Nachbarn.Select((n, i) => (n.Bezeichnung, i < Nachbarn.Count ? Nachbarn[i].Wert : n.Spiellokal)).ToList());
    }

    /// <summary>
    /// Original <c>TeamsDblClick</c>: angezeigte Spiellokale übernehmen, Dialog „Nachbarmannschaft“ öffnen und danach neu lesen.
    /// </summary>
    private async Task NachbarBearbeitenAsync(SpiellokalZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        Speichern();
        if (Bearbeitung.NachbarmannschaftIndex(mannschaft, zeile.Beschriftung) is int index
            && await Nachbarmannschaftsbearbeitung.BearbeitenAsync(Bearbeitung, mannschaft, index, oberflaeche))
        {
            Laden();
            AenderungMelden();
        }
    }
}
