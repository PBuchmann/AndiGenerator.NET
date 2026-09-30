// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;

namespace AndiGenerator.UI.Ansichten;

/// <summary>Terminwunschansicht: Zeitachse und Auswertung der Terminwünsche.</summary>
public partial class TerminwunschAnsicht : UserControl
{
    /// <summary>Initialisiert die Ansicht.</summary>
    public TerminwunschAnsicht()
    {
        InitializeComponent();
        Mausradzoom.Anmelden(this);
    }
}
