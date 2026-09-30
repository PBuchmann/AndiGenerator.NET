// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;

namespace AndiGenerator.UI;

/// <summary>Dialog „Pflichtspieltag“ (Original <c>TFormEditMandatoryDatesDialog</c>).</summary>
public partial class Pflichtspielzeitdialog : Window
{
    /// <summary>Initialisiert den Dialog (für den XAML-Designer).</summary>
    public Pflichtspielzeitdialog()
    {
        InitializeComponent();
    }

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="dialog">ViewModel.</param>
    public Pflichtspielzeitdialog(PflichtspielzeitdialogViewModel dialog)
        : this()
    {
        DataContext = dialog;
        Ok.Click += (_, _) =>
        {
            if (dialog.Bestaetigen())
            {
                Close(true);
            }
        };
        Abbrechen.Click += (_, _) => Close(false);
    }
}
