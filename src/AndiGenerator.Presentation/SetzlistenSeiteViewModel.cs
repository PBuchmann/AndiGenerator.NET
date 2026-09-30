// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using AndiGenerator.Application;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Seite „Setzliste“ (Original <c>TDialogPanelRanking</c>): Setzliste aktivieren und die erwartete Reihenfolge mit
/// „Nach oben“/„Nach unten“ festlegen. Standard ist wie im Original „nicht aktiv“; die Reihenfolge bleibt dabei erhalten.
/// </summary>
public sealed class SetzlistenSeiteViewModel : DatenSeiteViewModel
{
    private bool aktiv;
    private int auswahl = -1;

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    public SetzlistenSeiteViewModel(Datenbearbeitung bearbeitung)
        : base(bearbeitung, "Setzliste")
    {
        HochCommand = new RelayCommand(() => Verschieben(-1), () => Auswahl > 0);
        RunterCommand = new RelayCommand(() => Verschieben(1), () => Auswahl >= 0 && Auswahl < Reihenfolge.Count - 1);
        Laden();
    }

    /// <summary>Holt den Erklärungstext des Originals.</summary>
    public static string Erklaerung =>
        "Mit der Setzliste können Sie die erwartete Tabelle am Ende der Saison vorgeben.\n"
        + "Das Ziel ist, dass am Ende der Saison die stärksten und schwächsten Mannschaften gegeneinander spielen sollen. "
        + "Damit soll verhindert werden, dass in den wichtigen Spielen um den Auf- und Abstieg Mannschaften beteiligt sind, "
        + "bei denen es um nichts mehr geht.\n"
        + "Mit der Setzliste soll die Spannung bis zum Schluss erhalten bleiben und, falls es so etwas überhaupt geben sollte, "
        + "Mauscheleien verhindert werden.";

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Setzliste aktiv ist.</summary>
    public bool Aktiv
    {
        get => aktiv;
        set
        {
            if (SetProperty(ref aktiv, value))
            {
                AenderungMelden();
            }
        }
    }

    /// <summary>Holt die erwartete Reihenfolge (Platz 1 zuerst).</summary>
    public ObservableCollection<string> Reihenfolge { get; } = [];

    /// <summary>Holt oder setzt den Index der gewählten Mannschaft.</summary>
    public int Auswahl
    {
        get => auswahl;
        set
        {
            if (SetProperty(ref auswahl, value))
            {
                HochCommand.NotifyCanExecuteChanged();
                RunterCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Holt den Befehl „Nach oben“.</summary>
    public IRelayCommand HochCommand { get; }

    /// <summary>Holt den Befehl „Nach unten“.</summary>
    public IRelayCommand RunterCommand { get; }

    /// <inheritdoc/>
    public override bool IstStandard => !Aktiv;

    /// <inheritdoc/>
    public override void Laden()
    {
        Setzlistendaten daten = Bearbeitung.Setzliste();
        aktiv = daten.Aktiv;
        OnPropertyChanged(nameof(Aktiv));
        Reihenfolge.Clear();
        foreach (string name in daten.Reihenfolge)
        {
            Reihenfolge.Add(name);
        }

        Auswahl = Reihenfolge.Count > 0 ? 0 : -1;
    }

    /// <inheritdoc/>
    public override void Speichern() => Bearbeitung.SetzlisteSpeichern(new Setzlistendaten(Aktiv, Reihenfolge.ToList()));

    /// <inheritdoc/>
    public override void Standard() => Aktiv = false;

    /// <summary>
    /// Zieht eine Mannschaft auf eine andere (Original <c>ListRankingDragDrop</c>): Sie landet vor der Zielmannschaft.
    /// </summary>
    /// <param name="von">Index der gezogenen Mannschaft.</param>
    /// <param name="ziel">Index der Mannschaft, auf die sie gezogen wurde.</param>
    public void Ziehen(int von, int ziel)
    {
        if (von < 0 || ziel < 0 || von >= Reihenfolge.Count || ziel >= Reihenfolge.Count || von == ziel)
        {
            return;
        }

        int nach = ziel > von ? ziel - 1 : ziel;
        Reihenfolge.Move(von, nach);
        Auswahl = nach;
    }

    /// <summary>Verschiebt die gewählte Mannschaft (Original <c>ButtonUpClick</c>/<c>ButtonDownClick</c>).</summary>
    /// <param name="richtung">-1 nach oben, 1 nach unten.</param>
    internal void Verschieben(int richtung)
    {
        int von = Auswahl;
        int nach = von + richtung;
        if (von < 0 || nach < 0 || nach >= Reihenfolge.Count)
        {
            return;
        }

        Reihenfolge.Move(von, nach);
        Auswahl = nach;
    }
}
