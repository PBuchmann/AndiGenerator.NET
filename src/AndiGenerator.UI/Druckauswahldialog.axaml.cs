// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;

namespace AndiGenerator.UI;

/// <summary>Dialog „Drucken“ (Original <c>TFormPrintSelection</c>).</summary>
public partial class Druckauswahldialog : Window
{
    /// <summary>Initialisiert den Dialog (für den XAML-Designer).</summary>
    public Druckauswahldialog()
    {
        InitializeComponent();
    }

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="auswahl">ViewModel.</param>
    public Druckauswahldialog(DruckauswahlViewModel auswahl)
        : this()
    {
        DataContext = auswahl;
        Ok.Click += (_, _) => Close(true);
        Abbrechen.Click += (_, _) => Close(false);
    }
}
