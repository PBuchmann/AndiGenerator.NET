// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;
using Dock.Model.Mvvm.Controls;

namespace AndiGenerator.Presentation;

/// <summary>
/// Termine der Nachbarmannschaften (Original: Tab „Termine der Nachbarmannschaften“, <c>PaintSisterTeams</c>): je
/// Mannschaft die gemeldeten Spiele ihrer Nachbarmannschaften aus anderen Staffeln, nach Tagen gruppiert. Spiele echter
/// Nachbarmannschaften (keine parallelen Spiele) sind wie im Original blau. Hängt nur von den Stammdaten ab, nicht vom Plan.
/// </summary>
public sealed class NachbarterminAnsichtViewModel : Document, IZoombar
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private readonly HauptfensterViewModel hauptfenster;
    private IReadOnlyList<Terminzeile> zeilen = [];
    private IReadOnlyList<Nachbarkarte> karten = [];
    private string zusammenfassung = string.Empty;

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="hauptfenster">Hauptfenster.</param>
    public NachbarterminAnsichtViewModel(HauptfensterViewModel hauptfenster)
    {
        ArgumentNullException.ThrowIfNull(hauptfenster);
        this.hauptfenster = hauptfenster;
        Id = "Nachbartermine";
        Title = "Nachbarmannschaften";
        CanClose = true;
        CanFloat = true;
        Laden();
    }

    /// <summary>Holt die Zeilen: Mannschaft als Überschrift, darunter je Tag die Spiele der Nachbarmannschaften.</summary>
    public IReadOnlyList<Terminzeile> Zeilen
    {
        get => zeilen;
        private set => SetProperty(ref zeilen, value);
    }

    /// <summary>Holt die Karten je Mannschaft.</summary>
    public IReadOnlyList<Nachbarkarte> Karten
    {
        get => karten;
        private set
        {
            if (SetProperty(ref karten, value))
            {
                OnPropertyChanged(nameof(KeineTermine));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob keine Mannschaft Nachbartermine hat.</summary>
    public bool KeineTermine => Karten.Count == 0;

    /// <summary>Holt die Zusammenfassung, z. B. <c>57 Spiele von Nachbarmannschaften an 31 Tagen</c>.</summary>
    public string Zusammenfassung
    {
        get => zusammenfassung;
        private set => SetProperty(ref zusammenfassung, value);
    }

    /// <inheritdoc/>
    public Zoom Zoom { get; } = new();

    /// <summary>Liest die Nachbartermine neu (nach Öffnen einer Staffel).</summary>
    internal void Laden()
    {
        IReadOnlyList<MannschaftsNachbartermine> mannschaften = hauptfenster.Nachbartermine();
        int spiele = mannschaften.Sum(m => m.Tage.Sum(t => t.Count));
        int tage = mannschaften.Sum(m => m.Tage.Count);
        Zeilen = Terminzeilen.Nachbartermine(mannschaften);
        Karten = Nachbarkarte.Bilden(mannschaften);
        Zusammenfassung = mannschaften.Count == 0
            ? string.Empty
            : string.Create(Deutsch, $"{spiele} Spiele von Nachbarmannschaften an {tage} Tagen");
    }
}
