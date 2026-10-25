// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;

namespace AndiGenerator.UI;

/// <summary>
/// Markiert ein Element, auf das die Führung „Schritt für Schritt“ zeigen kann: <c>ui:Fuehrungsmarke.Ziel="Start"</c>. Die
/// Namen stehen in <c>Fuehrungsziel</c> (Presentation).
/// </summary>
public static class Fuehrungsmarke
{
    /// <summary>Der Name des Ziels.</summary>
    public static readonly AttachedProperty<string?> ZielProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Ziel", typeof(Fuehrungsmarke));

    /// <summary>Liest den Namen des Ziels.</summary>
    /// <param name="element">Das Element.</param>
    /// <returns>Der Name oder <c>null</c>.</returns>
    public static string? GetZiel(Control element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(ZielProperty);
    }

    /// <summary>Setzt den Namen des Ziels.</summary>
    /// <param name="element">Das Element.</param>
    /// <param name="wert">Der Name.</param>
    public static void SetZiel(Control element, string? wert)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(ZielProperty, wert);
    }
}
