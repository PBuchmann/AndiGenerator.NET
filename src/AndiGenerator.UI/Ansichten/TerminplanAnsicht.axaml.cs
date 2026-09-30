// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;

namespace AndiGenerator.UI.Ansichten;

/// <summary>Terminplanansicht: alle Spiele des gewählten Plans.</summary>
public partial class TerminplanAnsicht : UserControl
{
    /// <summary>Initialisiert die Ansicht.</summary>
    public TerminplanAnsicht()
    {
        InitializeComponent();
        Mausradzoom.Anmelden(this);
    }
}
