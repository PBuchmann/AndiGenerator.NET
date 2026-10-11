// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;

namespace AndiGenerator.UI.Seiten;

/// <summary>Auswärtskoppelwünsche einer Mannschaft; Doppelklick oder Eingabetaste bearbeitet, Entf löscht.</summary>
public partial class AuswaertskoppelDetail : UserControl
{
    /// <summary>Initialisiert die Ansicht.</summary>
    public AuswaertskoppelDetail()
    {
        InitializeComponent();
        Listenbedienung.Anmelden(Liste, () => DataContext is AuswaertskoppelDetailViewModel detail ? (detail.BearbeitenCommand, detail.LoeschenCommand) : default);
    }
}
