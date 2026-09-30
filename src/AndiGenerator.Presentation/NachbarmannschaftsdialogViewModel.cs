// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Dialog „Nachbarmannschaft“ (Original <c>TDialogEditSisterTeam</c>): Name, Nummer, Art, Spiellokal, parallele Spiele und
/// die Begegnungen mit Neu, Bearbeiten und Löschen.
/// </summary>
public sealed class NachbarmannschaftsdialogViewModel : ObservableObject
{
    private readonly Nachbarmannschaftsentwurf entwurf;
    private readonly IOberflaeche oberflaeche;
    private readonly DateOnly? saisonbeginn;
    private readonly Func<Nachbarmannschaftsentwurf, string?> pruefen;
    private IReadOnlyList<NachbarspielZeile> spiele = [];
    private NachbarspielZeile? auswahl;
    private string meldung = string.Empty;

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="entwurf">Arbeitskopie der Nachbarmannschaft.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche für den Begegnungsdialog.</param>
    /// <param name="saisonbeginn">Vorbelegung des Datums neuer Begegnungen.</param>
    /// <param name="pruefen">Prüfung beim OK; liefert die Fehlermeldung oder <c>null</c>.</param>
    public NachbarmannschaftsdialogViewModel(
        Nachbarmannschaftsentwurf entwurf,
        IOberflaeche oberflaeche,
        DateOnly? saisonbeginn,
        Func<Nachbarmannschaftsentwurf, string?> pruefen)
    {
        ArgumentNullException.ThrowIfNull(entwurf);
        ArgumentNullException.ThrowIfNull(oberflaeche);
        ArgumentNullException.ThrowIfNull(pruefen);
        this.entwurf = entwurf;
        this.oberflaeche = oberflaeche;
        this.saisonbeginn = saisonbeginn;
        this.pruefen = pruefen;
        NeuCommand = new AsyncRelayCommand(NeuAsync);
        BearbeitenCommand = new AsyncRelayCommand(BearbeitenAsync, () => Auswahl is not null);
        LoeschenCommand = new AsyncRelayCommand(LoeschenAsync, () => Auswahl is not null);
        SpieleLaden();
    }

    /// <summary>Holt die vorgeschlagenen Arten.</summary>
    public static IReadOnlyList<string> Arten => Ligadaten.Arten;

    /// <summary>Holt die Spiellokale.</summary>
    public static IReadOnlyList<string> Spiellokale => Mannschaftsdaten.Spiellokale;

    /// <summary>Holt oder setzt den Mannschaftsnamen; die Begegnungen zeigen ihn sofort.</summary>
    public string Name
    {
        get => entwurf.Name;
        set
        {
            if (entwurf.Name != value)
            {
                entwurf.Name = value ?? string.Empty;
                OnPropertyChanged();
                SpieleLaden();
            }
        }
    }

    /// <summary>Holt oder setzt die Mannschaftsnummer.</summary>
    public decimal? Nummer
    {
        get => entwurf.Nummer;
        set
        {
            entwurf.Nummer = (int)(value ?? 1);
            OnPropertyChanged();
        }
    }

    /// <summary>Holt oder setzt die Art.</summary>
    public string Art
    {
        get => entwurf.Art;
        set
        {
            entwurf.Art = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    /// <summary>Holt oder setzt das Spiellokal; die Begegnungen zeigen es sofort.</summary>
    public string? Spiellokal
    {
        get => entwurf.Spiellokal;
        set
        {
            entwurf.Spiellokal = value ?? string.Empty;
            OnPropertyChanged();
            SpieleLaden();
        }
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob gleichzeitige Spiele vermieden werden sollen.</summary>
    public bool KeineParallelenSpiele
    {
        get => entwurf.KeineParallelenSpiele;
        set
        {
            entwurf.KeineParallelenSpiele = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob gleichzeitige Heimspiele erwünscht sind.</summary>
    public bool ParalleleHeimspiele
    {
        get => entwurf.ParalleleHeimspiele;
        set
        {
            entwurf.ParalleleHeimspiele = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Holt die Begegnungen.</summary>
    public IReadOnlyList<NachbarspielZeile> Spiele
    {
        get => spiele;
        private set => SetProperty(ref spiele, value);
    }

    /// <summary>Holt oder setzt die gewählte Begegnung.</summary>
    public NachbarspielZeile? Auswahl
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

    /// <summary>Holt die Meldung der letzten Prüfung.</summary>
    public string Meldung
    {
        get => meldung;
        private set => SetProperty(ref meldung, value);
    }

    /// <summary>Prüft die Eingaben (OK im Original).</summary>
    /// <returns><c>true</c>, wenn der Dialog geschlossen werden darf.</returns>
    public bool Bestaetigen()
    {
        Meldung = pruefen(entwurf) ?? string.Empty;
        return Meldung.Length == 0;
    }

    private void SpieleLaden()
    {
        int? index = Auswahl?.Zeile.Index;
        Spiele = entwurf.Spiele().Select(z => new NachbarspielZeile(z)).ToList();
        Auswahl = Spiele.FirstOrDefault(z => z.Zeile.Index == index);
    }

    private async Task NeuAsync()
    {
        var dialog = new NachbarspieldialogViewModel(Name.Trim(), null, saisonbeginn, (s, g) => entwurf.SpielPruefen(null, s, g));
        if (await oberflaeche.NachbarspielBearbeitenAsync(dialog))
        {
            entwurf.SpielSpeichern(null, dialog.Ergebnis, dialog.Heimspiel);
            SpieleLaden();
        }
    }

    private async Task BearbeitenAsync()
    {
        if (Auswahl?.Zeile.Index is not int index)
        {
            return;
        }

        Nachbarspieldaten bisher = entwurf.Spiel(index);
        string name = Name.Trim();
        Nachbarspieldaten angezeigt = bisher with
        {
            Heim = bisher.Heim == entwurf.AlterName ? name : bisher.Heim,
            Gast = bisher.Gast == entwurf.AlterName ? name : bisher.Gast,
        };
        var dialog = new NachbarspieldialogViewModel(name, angezeigt, saisonbeginn, (s, g) => entwurf.SpielPruefen(index, s, g));
        if (await oberflaeche.NachbarspielBearbeitenAsync(dialog))
        {
            entwurf.SpielSpeichern(index, dialog.Ergebnis, dialog.Heimspiel);
            SpieleLaden();
        }
    }

    private async Task LoeschenAsync()
    {
        if (Auswahl?.Zeile.Index is int index && await oberflaeche.FragenAsync("Begegnung löschen", "Möchten Sie das Spiel wirklich löschen?"))
        {
            entwurf.SpielLoeschen(index);
            Auswahl = null;
            SpieleLaden();
        }
    }
}
