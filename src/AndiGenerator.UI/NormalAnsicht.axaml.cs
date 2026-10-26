// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace AndiGenerator.UI;

/// <summary>Die Seiten des normalen Modus (Dateien, Planung, Ergebnis); Dateien lassen sich auf die Ablage ziehen.</summary>
public partial class NormalAnsicht : UserControl
{
    /// <summary>Initialisiert die Seiten.</summary>
    public NormalAnsicht()
    {
        InitializeComponent();
        Ablage.AddHandler(DragDrop.DragOverEvent, Ueberziehen);
        Ablage.AddHandler(DragDrop.DropEvent, Ablegen);
    }

    private static void Ueberziehen(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.TryGetFiles() is { Length: > 0 } ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Ablegen(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        if (DataContext is not HauptfensterViewModel { Normal: var normal } || e.DataTransfer.TryGetFiles() is not { Length: > 0 } dateien)
        {
            return;
        }

        string[] pfade = [.. dateien.Select(d => d.TryGetLocalPath()).OfType<string>()];
        if (pfade.Length > 0)
        {
            _ = normal.AblegenAsync(pfade);
        }
    }
}
