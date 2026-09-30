// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Auswärtskoppelwünsche einer Mannschaft (Original <c>TDialogPanelAuswaertsKoppelDetail</c>): Liste der Wünsche mit Neu,
/// Bearbeiten und Löschen sowie alternative Anfangszeiten an Heimspielterminen, die Auswärtskoppeln anderer Mannschaften
/// ermöglichen würden.
/// </summary>
public sealed class AuswaertskoppelDetailViewModel : DatenSeiteViewModel
{
    private static readonly StringComparer Listenvergleich = StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), ignoreCase: true);

    private readonly string mannschaft;
    private readonly IOberflaeche oberflaeche;
    private IReadOnlyList<Auswaertskoppeldaten> koppeln = [];
    private Auswaertskoppeldaten? auswahl;
    private IReadOnlyList<ZweitzeitZeile> zweitzeiten = [];

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche für die Dialoge.</param>
    public AuswaertskoppelDetailViewModel(Datenbearbeitung bearbeitung, string mannschaft, IOberflaeche oberflaeche)
        : base(bearbeitung, "Auswärtskoppeltermine")
    {
        ArgumentNullException.ThrowIfNull(oberflaeche);
        this.mannschaft = mannschaft;
        this.oberflaeche = oberflaeche;
        NeuCommand = new AsyncRelayCommand(NeuAsync);
        BearbeitenCommand = new AsyncRelayCommand(BearbeitenAsync, () => Auswahl is not null);
        LoeschenCommand = new AsyncRelayCommand(LoeschenAsync, () => Auswahl is not null);
        AllesSetzenCommand = new RelayCommand(AllesSetzen);
        AlleLoeschenCommand = new RelayCommand(AlleLoeschen);
        Laden();
    }

    /// <summary>Holt den Erklärungstext zu den alternativen Zeiten.</summary>
    public static string Erklaerung =>
        "Um Auswärtskoppeltermine zu ermöglichen soll bei diesen Terminwünschen im Bedarfsfall eine andere Anfangszeit gewählt werden.\n"
        + "(geben Sie die alternative Anfangszeit ein, leer = keine alternative Zeit)";

    /// <summary>Holt die Auswärtskoppelwünsche, nach Bezeichnung sortiert.</summary>
    public IReadOnlyList<Auswaertskoppeldaten> Koppeln
    {
        get => koppeln;
        private set => SetProperty(ref koppeln, value);
    }

    /// <summary>Holt oder setzt den gewählten Wunsch.</summary>
    public Auswaertskoppeldaten? Auswahl
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

    /// <summary>Holt die Heimspieltermine mit alternativer Zeit.</summary>
    public IReadOnlyList<ZweitzeitZeile> Zweitzeiten
    {
        get => zweitzeiten;
        private set
        {
            if (SetProperty(ref zweitzeiten, value))
            {
                OnPropertyChanged(nameof(KeineZweitzeiten));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es keine relevanten Termine gibt.</summary>
    public bool KeineZweitzeiten => Zweitzeiten.Count == 0;

    /// <summary>Holt den Befehl „Neuer Wunsch…“.</summary>
    public IAsyncRelayCommand NeuCommand { get; }

    /// <summary>Holt den Befehl „Wunsch bearbeiten…“ (auch per Doppelklick).</summary>
    public IAsyncRelayCommand BearbeitenCommand { get; }

    /// <summary>Holt den Befehl „Wunsch löschen…“.</summary>
    public IAsyncRelayCommand LoeschenCommand { get; }

    /// <summary>Holt den Befehl „Alles setzen“ (alternative Zeiten vorschlagen).</summary>
    public IRelayCommand AllesSetzenCommand { get; }

    /// <summary>Holt den Befehl „Alle löschen“ (alternative Zeiten).</summary>
    public IRelayCommand AlleLoeschenCommand { get; }

    /// <inheritdoc/>
    public override bool IstStandard => Bearbeitung.AuswaertskoppelnSindStandard(mannschaft, Zweitzeiten.Select(z => z.Wert));

    /// <inheritdoc/>
    public override void Laden()
    {
        KoppelnLaden();
        Zweitzeiten = Bearbeitung.AuswaertsZweitzeiten(mannschaft).Select(z => new ZweitzeitZeile(z, AenderungMelden)).ToList();
    }

    /// <inheritdoc/>
    public override void Speichern() => Bearbeitung.AuswaertsZweitzeitenSpeichern(mannschaft, Zweitzeiten.Select(z => z.Wert).ToList());

    /// <inheritdoc/>
    public override void Standard()
    {
        Bearbeitung.AuswaertskoppelnZuruecksetzen(mannschaft);
        Laden();
        AenderungMelden();
    }

    private void KoppelnLaden()
    {
        // Wie im Original bleibt die Position in der Liste erhalten.
        int index = Auswahl is null ? -1 : Koppeln.ToList().IndexOf(Auswahl);
        Koppeln = Bearbeitung.Auswaertskoppeln(mannschaft).OrderBy(k => k.Bezeichnung, Listenvergleich).ToList();
        Auswahl = index >= 0 && index < Koppeln.Count ? Koppeln[index] : null;
    }

    private async Task NeuAsync()
    {
        var dialog = new AuswaertskoppeldialogViewModel(
            Bearbeitung.Auswaertskoppelpartner(mannschaft),
            null,
            d => Bearbeitung.AuswaertskoppelPruefen(mannschaft, null, d));
        if (await oberflaeche.AuswaertskoppelBearbeitenAsync(dialog))
        {
            Bearbeitung.AuswaertskoppelSpeichern(mannschaft, null, dialog.Ergebnis);
            KoppelnLaden();
            AenderungMelden();
        }
    }

    private async Task BearbeitenAsync()
    {
        if (Auswahl is not Auswaertskoppeldaten bisher)
        {
            return;
        }

        var dialog = new AuswaertskoppeldialogViewModel(
            Bearbeitung.Auswaertskoppelpartner(mannschaft),
            bisher,
            d => Bearbeitung.AuswaertskoppelPruefen(mannschaft, bisher, d));
        if (await oberflaeche.AuswaertskoppelBearbeitenAsync(dialog))
        {
            Bearbeitung.AuswaertskoppelSpeichern(mannschaft, bisher, dialog.Ergebnis);
            KoppelnLaden();
            AenderungMelden();
        }
    }

    private async Task LoeschenAsync()
    {
        if (Auswahl is Auswaertskoppeldaten koppel
            && await oberflaeche.FragenAsync("Auswärtskoppelwunsch löschen", $"Möchten Sie den Auswärtskoppelwunsch {koppel.Bezeichnung} wirklich löschen?"))
        {
            Bearbeitung.AuswaertskoppelLoeschen(mannschaft, koppel);
            KoppelnLaden();
            AenderungMelden();
        }
    }

    private void AllesSetzen()
    {
        foreach (ZweitzeitZeile zeile in Zweitzeiten)
        {
            zeile.Text = ZweitzeitZeile.Format(zeile.Vorschlag);
        }
    }

    private void AlleLoeschen()
    {
        foreach (ZweitzeitZeile zeile in Zweitzeiten)
        {
            zeile.Text = string.Empty;
        }
    }
}
