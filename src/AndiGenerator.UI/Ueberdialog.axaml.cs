// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;

namespace AndiGenerator.UI;

/// <summary>Dialog „Über AndiGenerator.NET“: Urheber, Entstehung, Lizenz.</summary>
public partial class Ueberdialog : Window
{
    /// <summary>Initialisiert den Dialog (für den XAML-Designer).</summary>
    public Ueberdialog()
    {
        InitializeComponent();
    }

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="ueber">ViewModel.</param>
    public Ueberdialog(UeberViewModel ueber)
        : this()
    {
        DataContext = ueber;
        Schliessen.Click += (_, _) => Close();
    }
}
