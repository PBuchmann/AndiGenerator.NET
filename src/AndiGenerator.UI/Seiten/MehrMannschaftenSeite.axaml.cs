// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.ComponentModel;
using AndiGenerator.Presentation;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AndiGenerator.UI.Seiten;

/// <summary>
/// Seite mit Mannschaftsliste und Detailansicht (Original <c>TDialogPanelMultiTeams</c>). Die Auswahl wird von Hand
/// gesetzt, damit sie bei ungültigen Eingaben zurückspringen kann.
/// </summary>
public partial class MehrMannschaftenSeite : UserControl
{
    private MehrMannschaftenSeiteViewModel? seite;

    /// <summary>Initialisiert die Seite.</summary>
    public MehrMannschaftenSeite()
    {
        InitializeComponent();
        Liste.SelectionChanged += (_, _) => Waehlen();
        DataContextChanged += (_, _) => Verbinden();
    }

    private void Verbinden()
    {
        if (seite is not null)
        {
            seite.PropertyChanged -= Geaendert;
        }

        seite = DataContext as MehrMannschaftenSeiteViewModel;
        if (seite is not null)
        {
            seite.PropertyChanged += Geaendert;
            Liste.SelectedItem = seite.Mannschaft;
        }
    }

    private void Waehlen()
    {
        if (seite is null || Liste.SelectedItem is not string name || name == seite.Mannschaft)
        {
            return;
        }

        seite.Mannschaft = name;
        if (name != seite.Mannschaft)
        {
            Dispatcher.UIThread.Post(() => Liste.SelectedItem = seite.Mannschaft);
        }
    }

    private void Geaendert(object? sender, PropertyChangedEventArgs e)
    {
        if (seite is not null && e.PropertyName == nameof(MehrMannschaftenSeiteViewModel.Mannschaft) && !Equals(Liste.SelectedItem, seite.Mannschaft))
        {
            Liste.SelectedItem = seite.Mannschaft;
        }
    }
}
