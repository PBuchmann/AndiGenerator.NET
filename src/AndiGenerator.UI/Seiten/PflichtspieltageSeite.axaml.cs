// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;

namespace AndiGenerator.UI.Seiten;

/// <summary>Seite „Pflichtspieltage“ des Dialogs „Spielplandaten bearbeiten“; Doppelklick oder Eingabetaste bearbeitet, Entf löscht.</summary>
public partial class PflichtspieltageSeite : UserControl
{
    /// <summary>Initialisiert die Seite.</summary>
    public PflichtspieltageSeite()
    {
        InitializeComponent();
        Listenbedienung.Anmelden(Liste, () => DataContext is PflichtspieltageSeiteViewModel seite ? (seite.BearbeitenCommand, seite.LoeschenCommand) : default);
    }
}
