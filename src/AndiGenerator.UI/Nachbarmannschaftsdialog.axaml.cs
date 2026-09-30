// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;

namespace AndiGenerator.UI;

/// <summary>Dialog „Nachbarmannschaft“ (Original <c>TDialogEditSisterTeam</c>); Doppelklick bearbeitet eine Begegnung.</summary>
public partial class Nachbarmannschaftsdialog : Window
{
    /// <summary>Initialisiert den Dialog (für den XAML-Designer).</summary>
    public Nachbarmannschaftsdialog()
    {
        InitializeComponent();
    }

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="dialog">ViewModel.</param>
    public Nachbarmannschaftsdialog(NachbarmannschaftsdialogViewModel dialog)
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
        Liste.DoubleTapped += (_, _) =>
        {
            if (dialog.BearbeitenCommand.CanExecute(null))
            {
                dialog.BearbeitenCommand.Execute(null);
            }
        };
    }
}
