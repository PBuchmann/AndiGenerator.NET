// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Dock.Model.Mvvm.Controls;

namespace AndiGenerator.Presentation;

/// <summary>
/// Gemeinsame Grundlage aller Ansichten im Andock-Layout (MIGRATIONSPLAN E15): Jede Ansicht ist ein Dokument,
/// wählt ihre Planquelle selbst und zeigt den Plan dieser Quelle.
/// </summary>
public abstract class AnsichtViewModel : Document, IZoombar
{
    private readonly HauptfensterViewModel hauptfenster;
    private readonly string art;
    private IReadOnlyList<PlanQuelle> quellen;
    private PlanQuelle? quelle;
    private string hinweis = string.Empty;

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="hauptfenster">Hauptfenster, das Pläne und Quellen liefert.</param>
    /// <param name="art">Art der Ansicht für den Titel, z. B. <c>Kosten</c>.</param>
    /// <param name="id">Eindeutige Kennung im Layout.</param>
    protected AnsichtViewModel(HauptfensterViewModel hauptfenster, string art, string id)
    {
        ArgumentNullException.ThrowIfNull(hauptfenster);
        this.hauptfenster = hauptfenster;
        this.art = art;
        Id = id;
        CanClose = true;
        CanFloat = true;
        quellen = hauptfenster.Planquellen;
        quelle = PlanQuelle.Laufend;
        Title = TitelBilden();
    }

    /// <summary>Holt die auswählbaren Planquellen.</summary>
    public IReadOnlyList<PlanQuelle> Quellen
    {
        get => quellen;
        private set => SetProperty(ref quellen, value);
    }

    /// <summary>Holt oder setzt die gewählte Planquelle.</summary>
    public PlanQuelle? Quelle
    {
        get => quelle;
        set
        {
            if (SetProperty(ref quelle, value))
            {
                Title = TitelBilden();
                Aktualisieren();
            }
        }
    }

    /// <summary>Holt den Hinweis, wenn kein Plan angezeigt werden kann (sonst leer).</summary>
    public string Hinweis
    {
        get => hinweis;
        protected set
        {
            if (SetProperty(ref hinweis, value))
            {
                OnPropertyChanged(nameof(HatHinweis));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob ein Hinweis statt eines Plans angezeigt wird.</summary>
    public bool HatHinweis => Hinweis.Length > 0;

    /// <inheritdoc/>
    public Zoom Zoom { get; } = new();

    /// <summary>Holt die Quelle als Art (für die Aktualisierung bei Verbesserungen).</summary>
    internal PlanQuellenArt? QuellenArt => Quelle?.Art;

    /// <summary>Holt das Hauptfenster (Pläne, Optionen, Gewichtungsänderungen).</summary>
    protected HauptfensterViewModel Hauptfenster => hauptfenster;

    /// <summary>Zeigt den aktuellen Plan der gewählten Quelle neu an.</summary>
    internal void Aktualisieren()
    {
        Planstand? stand = Quelle is null ? null : hauptfenster.PlanFuer(Quelle);
        Hinweis = stand is null ? HinweisOhnePlan() : string.Empty;
        Anzeigen(stand);
        hauptfenster.ExportAktualisieren();
    }

    /// <summary>Übernimmt neue Planquellen und behält die gewählte bei, sofern es sie noch gibt.</summary>
    /// <param name="neu">Die neuen Quellen.</param>
    internal void QuellenUebernehmen(IReadOnlyList<PlanQuelle> neu)
    {
        PlanQuelle? bisher = Quelle;
        Quellen = neu;
        PlanQuelle gewaehlt = neu.FirstOrDefault(q => q == bisher) ?? neu[0];
        if (gewaehlt == quelle)
        {
            // Die Auswahlliste kann die Auswahl beim Tausch der Liste verworfen haben: erneut melden.
            OnPropertyChanged(nameof(Quelle));
            Aktualisieren();
        }
        else
        {
            Quelle = gewaehlt;
        }
    }

    /// <summary>Zeigt einen Plan an.</summary>
    /// <param name="stand">Der Plan oder <c>null</c>, wenn die Quelle (noch) keinen Plan liefert.</param>
    protected abstract void Anzeigen(Planstand? stand);

    private string HinweisOhnePlan() => Quelle?.Art switch
    {
        PlanQuellenArt.LaufendeGenerierung when !hauptfenster.IstGeoeffnet => "Bitte zuerst eine click-TT-Datei oder Plandatei öffnen.",
        PlanQuellenArt.LaufendeGenerierung => "Noch kein Plan – bitte die Generierung starten.",
        null => "Bitte eine Planquelle wählen.",
        _ => "Dieser Plan ist nicht verfügbar.",
    };

    private string TitelBilden() => Quelle is null ? art : $"{art} – {Quelle.Name}";
}
