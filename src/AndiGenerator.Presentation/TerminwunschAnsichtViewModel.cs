// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;
using Dock.Model.Mvvm.Controls;

namespace AndiGenerator.Presentation;

/// <summary>
/// Terminwünsche (Original: Tab „Terminwünsche“): Zeitachse mit Heimspielwünschen, Koppelterminen und Sperrterminen je
/// Mannschaft, darunter je Mannschaft die Wünsche mit Hallenbelegung und parallelen Spielen der Nachbarmannschaften,
/// Sperrtermine, Auswärtskoppeln, 60-km-Regel, Heimrecht und die Auswertung, ob die Termine reichen. Hängt nur von den
/// Stammdaten und Optionen ab, nicht vom Plan.
/// </summary>
public sealed class TerminwunschAnsichtViewModel : Document, IZoombar
{
    private readonly HauptfensterViewModel hauptfenster;
    private Terminwunschuebersicht? uebersicht;
    private IReadOnlyList<Terminwunschzeile> zeilen = [];
    private IReadOnlyList<Wunschkarte> karten = [];
    private IReadOnlyList<string> hinweise = [];
    private bool istUebersicht = true;

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="hauptfenster">Hauptfenster.</param>
    public TerminwunschAnsichtViewModel(HauptfensterViewModel hauptfenster)
    {
        ArgumentNullException.ThrowIfNull(hauptfenster);
        this.hauptfenster = hauptfenster;
        Id = "Terminwuensche";
        Title = "Terminwünsche";
        CanClose = true;
        CanFloat = true;
        Laden();
    }

    /// <summary>Holt die Übersicht (für das Diagramm); <c>null</c>, solange keine Staffel geöffnet ist.</summary>
    public Terminwunschuebersicht? Uebersicht
    {
        get => uebersicht;
        private set => SetProperty(ref uebersicht, value);
    }

    /// <summary>Holt die Textzeilen je Mannschaft.</summary>
    public IReadOnlyList<Terminwunschzeile> Zeilen
    {
        get => zeilen;
        private set => SetProperty(ref zeilen, value);
    }

    /// <summary>Holt die Karten je Mannschaft (Mannschaften mit Problemen zuerst).</summary>
    public IReadOnlyList<Wunschkarte> Karten
    {
        get => karten;
        private set => SetProperty(ref karten, value);
    }

    /// <summary>Holt die allgemeinen Hinweise (über allen Mannschaften).</summary>
    public IReadOnlyList<string> Hinweise
    {
        get => hinweise;
        private set
        {
            if (SetProperty(ref hinweise, value))
            {
                OnPropertyChanged(nameof(HatHinweise));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es allgemeine Hinweise gibt.</summary>
    public bool HatHinweise => Hinweise.Count > 0;

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Übersicht (Raster) gezeigt wird.</summary>
    public bool IstUebersicht
    {
        get => istUebersicht;
        set => SeiteWaehlen(uebersichtGewaehlt: value, gesetzt: value);
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Karten je Mannschaft gezeigt werden.</summary>
    public bool IstKarten
    {
        get => !istUebersicht;
        set => SeiteWaehlen(uebersichtGewaehlt: !value, gesetzt: value);
    }

    /// <inheritdoc/>
    public Zoom Zoom { get; } = new();

    /// <summary>Liest die Terminwünsche neu (nach Öffnen oder geänderten Optionen).</summary>
    internal void Laden()
    {
        Uebersicht = hauptfenster.Terminwuensche();
        Zeilen = Terminwunschzeile.Bilden(Uebersicht);
        Karten = Wunschkarte.Bilden(Uebersicht);
        Hinweise = Uebersicht?.Hinweise ?? [];
    }

    /// <summary>
    /// Wechselt die Seite; ein Klick auf die schon gewählte Seite lässt sie gewählt (Umschalter wie Optionsfelder).
    /// </summary>
    private void SeiteWaehlen(bool uebersichtGewaehlt, bool gesetzt)
    {
        if (gesetzt)
        {
            istUebersicht = uebersichtGewaehlt;
        }

        OnPropertyChanged(nameof(IstUebersicht));
        OnPropertyChanged(nameof(IstKarten));
    }
}
