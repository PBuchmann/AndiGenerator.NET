// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;

namespace AndiGenerator.UI.Seiten;

/// <summary>Seite „Pflichtspieltage“ des Dialogs „Spielplandaten bearbeiten“; Doppelklick bearbeitet wie im Original.</summary>
public partial class PflichtspieltageSeite : UserControl
{
    /// <summary>Initialisiert die Seite.</summary>
    public PflichtspieltageSeite()
    {
        InitializeComponent();
        Liste.DoubleTapped += (_, _) =>
        {
            if (DataContext is PflichtspieltageSeiteViewModel seite && seite.BearbeitenCommand.CanExecute(null))
            {
                seite.BearbeitenCommand.Execute(null);
            }
        };
    }
}
