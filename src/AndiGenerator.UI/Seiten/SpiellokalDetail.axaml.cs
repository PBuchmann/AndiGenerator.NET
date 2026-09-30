// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;
using Avalonia.Input;

namespace AndiGenerator.UI.Seiten;

/// <summary>Spiellokale einer Mannschaft; Doppelklick auf eine Nachbarmannschaft öffnet sie wie im Original.</summary>
public partial class SpiellokalDetail : UserControl
{
    /// <summary>Initialisiert die Ansicht.</summary>
    public SpiellokalDetail()
    {
        InitializeComponent();
    }

    private void NachbarDoppelklick(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: SpiellokalZeile zeile } && DataContext is SpiellokalDetailViewModel detail)
        {
            detail.NachbarBearbeitenCommand.Execute(zeile);
        }
    }
}
