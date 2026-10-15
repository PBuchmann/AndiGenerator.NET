// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;

namespace AndiGenerator.UI;

/// <summary>
/// Dialog „B-Varianten“: bildschirmfüllend, der aktuelle Plan und alle Alternativen aus HiGHS nebeneinander, darunter
/// die betroffenen Mannschaften der ausgewählten Alternative; „Übernehmen“ legt A und B mit ihr fest.
/// </summary>
public partial class Alternativendialog : Window
{
    /// <summary>Initialisiert den Dialog (für den XAML-Designer).</summary>
    public Alternativendialog()
    {
        InitializeComponent();
    }

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="dialog">Der Dialog.</param>
    public Alternativendialog(AlternativendialogViewModel dialog)
        : this()
    {
        DataContext = dialog;
        Uebernehmen.Click += (_, _) => Close(true);
        Abbrechen.Click += (_, _) => Close(false);
    }
}
