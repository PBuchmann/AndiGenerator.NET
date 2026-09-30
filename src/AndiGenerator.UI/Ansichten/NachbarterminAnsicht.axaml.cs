// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;

namespace AndiGenerator.UI.Ansichten;

/// <summary>Ansicht der Termine der Nachbarmannschaften.</summary>
public partial class NachbarterminAnsicht : UserControl
{
    /// <summary>Initialisiert die Ansicht.</summary>
    public NachbarterminAnsicht()
    {
        InitializeComponent();
        Mausradzoom.Anmelden(this);
    }
}
