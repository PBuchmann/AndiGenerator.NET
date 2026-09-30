// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AndiGenerator.UI.Seiten;

/// <summary>
/// Seite „Setzliste“ des Dialogs „Spielplandaten bearbeiten“. Wie im Original lässt sich eine Mannschaft mit der Maus
/// auf eine andere ziehen; sie landet dann vor dieser.
/// </summary>
public partial class SetzlistenSeite : UserControl
{
    private static readonly Cursor Ziehcursor = new(StandardCursorType.DragMove);

    private int gezogen = -1;
    private Point start;

    /// <summary>Initialisiert die Seite.</summary>
    public SetzlistenSeite()
    {
        InitializeComponent();
        Liste.AddHandler(PointerPressedEvent, Gedrueckt, RoutingStrategies.Tunnel);
        Liste.AddHandler(PointerMovedEvent, Bewegt, RoutingStrategies.Tunnel);
        Liste.AddHandler(PointerReleasedEvent, Losgelassen, RoutingStrategies.Tunnel);
    }

    private void Gedrueckt(object? sender, PointerPressedEventArgs e)
    {
        gezogen = e.GetCurrentPoint(Liste).Properties.IsLeftButtonPressed ? Index(e.GetPosition(Liste)) : -1;
        start = e.GetPosition(Liste);
    }

    private void Bewegt(object? sender, PointerEventArgs e)
    {
        if (gezogen >= 0 && Math.Abs(e.GetPosition(Liste).Y - start.Y) > 4)
        {
            Liste.Cursor = Ziehcursor;
        }
    }

    private void Losgelassen(object? sender, PointerReleasedEventArgs e)
    {
        int ziel = Index(e.GetPosition(Liste));
        if (gezogen >= 0 && ziel >= 0 && ReferenceEquals(Liste.Cursor, Ziehcursor) && DataContext is SetzlistenSeiteViewModel seite)
        {
            seite.Ziehen(gezogen, ziel);
        }

        gezogen = -1;
        Liste.Cursor = Cursor.Default;
    }

    /// <summary>Index der Mannschaft unter dem Mauszeiger oder -1 (Original <c>ItemAtPos</c>).</summary>
    private int Index(Point punkt) =>
        Liste.InputHitTest(punkt) is Visual treffer && treffer.FindAncestorOfType<ListBoxItem>(includeSelf: true) is ListBoxItem eintrag
            ? Liste.IndexFromContainer(eintrag)
            : -1;
}
