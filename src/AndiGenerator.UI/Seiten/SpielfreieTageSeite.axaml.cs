// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AndiGenerator.UI.Seiten;

/// <summary>
/// Seite „Spielfreie Tage“ des Dialogs „Spielplandaten bearbeiten“. Der Dialog scrollt auch waagrecht; damit die Monate
/// trotzdem umbrechen statt nach rechts zu wachsen (Legende und Monate), folgt die Breite der Seite dem sichtbaren Bereich.
/// </summary>
public partial class SpielfreieTageSeite : UserControl
{
    /// <summary>Rand links und rechts der Seite innerhalb des sichtbaren Bereichs.</summary>
    private const double Rand = 40;

    private ScrollViewer? rahmen;

    /// <summary>Initialisiert die Seite.</summary>
    public SpielfreieTageSeite()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        rahmen = this.FindAncestorOfType<ScrollViewer>();
        if (rahmen is not null)
        {
            rahmen.PropertyChanged += RahmenGeaendert;
            BreiteSetzen();
        }
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (rahmen is not null)
        {
            rahmen.PropertyChanged -= RahmenGeaendert;
            rahmen = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void RahmenGeaendert(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.ViewportProperty)
        {
            BreiteSetzen();
        }
    }

    private void BreiteSetzen()
    {
        if (rahmen is { Viewport.Width: > Rand })
        {
            Seite.MaxWidth = rahmen.Viewport.Width - Rand;
        }
    }
}
