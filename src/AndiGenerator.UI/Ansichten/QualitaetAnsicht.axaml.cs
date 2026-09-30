// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;

namespace AndiGenerator.UI.Ansichten;

/// <summary>Qualitätsansicht: Verstöße je Kriterium nach der Rangfolge A/B/C.</summary>
public partial class QualitaetAnsicht : UserControl
{
    /// <summary>Initialisiert die Ansicht.</summary>
    public QualitaetAnsicht()
    {
        InitializeComponent();
        Mausradzoom.Anmelden(this);
    }
}
