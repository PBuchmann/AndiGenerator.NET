// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AndiGenerator.UI.Seiten;

/// <summary>Wunschtermine einer Mannschaft: Wochenübersicht, Doppelklick oder Kontextmenü öffnet den Dialog „Wunschtermin“.</summary>
public partial class WunschterminDetail : UserControl
{
    /// <summary>Initialisiert die Ansicht.</summary>
    public WunschterminDetail()
    {
        InitializeComponent();

        // Das Textfeld wertet einen Doppelklick selbst aus (Wort markieren) und gibt ihn nicht weiter. Deshalb wird er
        // hier schon auf dem Weg zum Textfeld abgefangen.
        AddHandler(PointerPressedEvent, BeiMausdruck, RoutingStrategies.Tunnel);
    }

    private void BeiMausdruck(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount != 2 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if ((e.Source as Visual)?.FindAncestorOfType<TextBox>(includeSelf: true) is TextBox feld)
        {
            e.Handled = true;
            Ausfuehren(feld, bearbeiten: true);
        }
    }

    private void Bearbeiten(object? sender, RoutedEventArgs e) => Ausfuehren(sender, bearbeiten: true);

    private void Loeschen(object? sender, RoutedEventArgs e) => Ausfuehren(sender, bearbeiten: false);

    private void Ausfuehren(object? sender, bool bearbeiten)
    {
        if (sender is not Control { DataContext: WunschterminZelle zelle } || DataContext is not WunschterminDetailViewModel detail)
        {
            return;
        }

        if (bearbeiten)
        {
            detail.BearbeitenCommand.Execute(zelle);
        }
        else
        {
            detail.LoeschenCommand.Execute(zelle);
        }
    }
}
