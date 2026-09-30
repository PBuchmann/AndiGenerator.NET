// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;

namespace AndiGenerator.UI.Seiten;

/// <summary>Nachbarmannschaften einer Mannschaft; Doppelklick bearbeitet wie im Original.</summary>
public partial class NachbarmannschaftDetail : UserControl
{
    /// <summary>Initialisiert die Ansicht.</summary>
    public NachbarmannschaftDetail()
    {
        InitializeComponent();
        Liste.DoubleTapped += (_, _) =>
        {
            if (DataContext is NachbarmannschaftDetailViewModel detail && detail.BearbeitenCommand.CanExecute(null))
            {
                detail.BearbeitenCommand.Execute(null);
            }
        };
    }
}
