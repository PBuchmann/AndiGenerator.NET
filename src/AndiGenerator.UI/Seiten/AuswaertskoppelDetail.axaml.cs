// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;

namespace AndiGenerator.UI.Seiten;

/// <summary>Auswärtskoppelwünsche einer Mannschaft; Doppelklick bearbeitet wie im Original.</summary>
public partial class AuswaertskoppelDetail : UserControl
{
    /// <summary>Initialisiert die Ansicht.</summary>
    public AuswaertskoppelDetail()
    {
        InitializeComponent();
        Liste.DoubleTapped += (_, _) =>
        {
            if (DataContext is AuswaertskoppelDetailViewModel detail && detail.BearbeitenCommand.CanExecute(null))
            {
                detail.BearbeitenCommand.Execute(null);
            }
        };
    }
}
