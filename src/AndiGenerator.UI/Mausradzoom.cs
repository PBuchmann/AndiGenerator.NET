// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AndiGenerator.UI;

/// <summary>Strg + Mausrad vergrößert bzw. verkleinert eine Ansicht, deren Datenkontext <see cref="IZoombar"/> ist.</summary>
internal static class Mausradzoom
{
    /// <summary>Meldet die Ansicht an; das Mausrad wird abgefangen, bevor die Bildlaufleiste es verarbeitet.</summary>
    /// <param name="ansicht">Die Ansicht.</param>
    public static void Anmelden(Control ansicht) =>
        ansicht.AddHandler(InputElement.PointerWheelChangedEvent, Rad, RoutingStrategies.Tunnel);

    private static void Rad(object? sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) == 0 || sender is not Control { DataContext: IZoombar ansicht })
        {
            return;
        }

        if (e.Delta.Y > 0)
        {
            ansicht.Zoom.Groesser();
        }
        else if (e.Delta.Y < 0)
        {
            ansicht.Zoom.Kleiner();
        }

        e.Handled = true;
    }
}
