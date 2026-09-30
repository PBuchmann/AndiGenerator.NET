// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;

namespace AndiGenerator.UI;

/// <summary>Auswahl der Vergrößerung einer Ansicht (Datenkontext: <see cref="Presentation.Zoom"/>).</summary>
public partial class Zoomauswahl : UserControl
{
    /// <summary>Initialisiert die Auswahl.</summary>
    public Zoomauswahl()
    {
        InitializeComponent();
        Zuruecksetzen.Click += (_, _) => (DataContext as Presentation.Zoom)?.Zuruecksetzen();
        Kleiner.Click += (_, _) => (DataContext as Presentation.Zoom)?.Kleiner();
        Groesser.Click += (_, _) => (DataContext as Presentation.Zoom)?.Groesser();
    }
}
