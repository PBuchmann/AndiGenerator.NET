// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;
using AndiGenerator.Rendering;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Diagramme (Original: Tab „Diagramme“, <c>PaintVerteilung</c>): Spieltage mit Überlappungen, Wechsel Heim/Auswärts,
/// Spielverteilung, Abstand Heimspiel zu Auswärtsspiel, Anzahl Spiele pro Woche und Setzliste für den gewählten Plan.
/// </summary>
public sealed class DiagrammAnsichtViewModel : AnsichtViewModel
{
    private Diagrammdaten? daten;
    private IReadOnlyList<Diagrammwahl> auswahl = [];
    private IReadOnlyList<Diagrammteil> teile = [];
    private Diagrammteil? gewaehlt = Diagrammteil.Spieltage;

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="hauptfenster">Hauptfenster.</param>
    /// <param name="id">Eindeutige Kennung im Layout.</param>
    public DiagrammAnsichtViewModel(HauptfensterViewModel hauptfenster, string id)
        : base(hauptfenster, "Diagramme", id)
    {
        WaehlenCommand = new RelayCommand<Diagrammwahl>(Waehlen);
    }

    /// <summary>Holt die Diagrammdaten des gewählten Plans oder <c>null</c>.</summary>
    public Diagrammdaten? Daten
    {
        get => daten;
        private set => SetProperty(ref daten, value);
    }

    /// <summary>Holt die Karten der Diagrammauswahl, je Diagramm eine.</summary>
    public IReadOnlyList<Diagrammwahl> Auswahl
    {
        get => auswahl;
        private set => SetProperty(ref auswahl, value);
    }

    /// <summary>Holt die zu zeichnenden Diagramme: das gewählte (leer ohne Plan).</summary>
    public IReadOnlyList<Diagrammteil> Teile
    {
        get => teile;
        private set => SetProperty(ref teile, value);
    }

    /// <summary>Holt die Beschreibung des gewählten Diagramms.</summary>
    public string Beschreibung => gewaehlt is Diagrammteil teil && Teile.Count > 0 ? Diagrammwahl.BeschreibungVon(teil) : string.Empty;

    /// <summary>Holt einen Wert, der angibt, ob eine Beschreibung gezeigt wird.</summary>
    public bool HatBeschreibung => Beschreibung.Length > 0;

    /// <summary>Holt den Befehl, ein Diagramm zu wählen.</summary>
    public IRelayCommand<Diagrammwahl> WaehlenCommand { get; }

    /// <inheritdoc/>
    protected override void Anzeigen(Planstand? stand)
    {
        Daten = stand is null ? null : Hauptfenster.Diagramme(stand);
        Aufbauen();
    }

    private void Waehlen(Diagrammwahl? wahl)
    {
        if (wahl?.Teil is Diagrammteil teil)
        {
            gewaehlt = teil;
            Aufbauen();
        }
    }

    private void Aufbauen()
    {
        IReadOnlyList<Diagrammteil> vorhanden = Daten is Diagrammdaten d ? Diagrammzeichner.Teile(d) : [];
        if (gewaehlt is not Diagrammteil g || !vorhanden.Contains(g))
        {
            gewaehlt = vorhanden.Count > 0 ? vorhanden[0] : null;
        }

        Auswahl = vorhanden
            .Select(t => new Diagrammwahl(t, Diagrammwahl.TitelVon(t), Diagrammwahl.BeschreibungVon(t), t == gewaehlt))
            .ToList();
        Teile = gewaehlt is Diagrammteil einzeln ? [einzeln] : [];
        OnPropertyChanged(nameof(Beschreibung));
        OnPropertyChanged(nameof(HatBeschreibung));
    }
}
