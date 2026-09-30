// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;

namespace AndiGenerator.UI.Ansichten;

/// <summary>Meldungen je Mannschaft: was die Kosten verursacht.</summary>
public partial class MeldungenAnsicht : UserControl
{
    /// <summary>Initialisiert die Ansicht.</summary>
    public MeldungenAnsicht()
    {
        InitializeComponent();
        Mausradzoom.Anmelden(this);
    }
}
