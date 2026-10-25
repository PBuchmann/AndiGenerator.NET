// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.CompilerServices;
using AndiGenerator.Presentation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;

namespace AndiGenerator.UI;

/// <summary>
/// Vergrößert bzw. verkleinert eine Ansicht, deren Datenkontext <see cref="IZoombar"/> ist: mit Strg + Mausrad (auf dem Mac
/// auch ⌘ + Mausrad) und mit zwei Fingern – auf dem Touchbildschirm wie auf dem Touchpad auseinander- oder zusammenschieben.
/// </summary>
internal static class Ansichtszoom
{
    /// <summary>Pause, nach der eine Touchpad-Geste als neue Geste gilt.</summary>
    private static readonly TimeSpan Gestenpause = TimeSpan.FromMilliseconds(400);

    /// <summary>Je Ansicht die laufende Geste: Vergrößerung zu Beginn, bisherige Skala und Zeit der letzten Meldung.</summary>
    private static readonly ConditionalWeakTable<Control, Geste> Gesten = [];

    /// <summary>Meldet die Ansicht an; Mausrad und Gesten werden abgefangen, bevor die Bildlaufleiste sie verarbeitet.</summary>
    /// <param name="ansicht">Die Ansicht.</param>
    public static void Anmelden(Control ansicht)
    {
        ansicht.AddHandler(InputElement.PointerWheelChangedEvent, Rad, RoutingStrategies.Tunnel);
        ansicht.GestureRecognizers.Add(new PinchGestureRecognizer());
        ansicht.AddHandler(InputElement.PinchEvent, Finger, RoutingStrategies.Bubble, handledEventsToo: true);
        ansicht.AddHandler(InputElement.PinchEndedEvent, FingerEnde, RoutingStrategies.Bubble, handledEventsToo: true);
        ansicht.AddHandler(InputElement.PointerTouchPadGestureMagnifyEvent, Touchpad, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    }

    private static void Rad(object? sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0 || sender is not Control { DataContext: IZoombar ansicht })
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

    /// <summary>Zwei Finger auf dem Bildschirm: die Skala gilt seit Beginn der Geste.</summary>
    private static void Finger(object? sender, PinchEventArgs e)
    {
        if (sender is not Control { DataContext: IZoombar ansicht } steuerelement)
        {
            return;
        }

        Geste geste = Gesten.GetValue(steuerelement, _ => new Geste(ansicht.Zoom.Prozent));
        ansicht.Zoom.Skalieren(geste.Start, e.Scale);
        e.Handled = true;
    }

    private static void FingerEnde(object? sender, PinchEndedEventArgs e)
    {
        if (sender is Control steuerelement)
        {
            Gesten.Remove(steuerelement);
        }
    }

    /// <summary>Touchpad: jede Meldung bringt nur die Änderung seit der vorigen; nach einer Pause beginnt eine neue Geste.</summary>
    private static void Touchpad(object? sender, PointerDeltaEventArgs e)
    {
        if (sender is not Control { DataContext: IZoombar ansicht } steuerelement || e.Handled)
        {
            return;
        }

        DateTime jetzt = DateTime.UtcNow;
        if (!Gesten.TryGetValue(steuerelement, out Geste? geste) || jetzt - geste.Zuletzt > Gestenpause)
        {
            geste = new Geste(ansicht.Zoom.Prozent);
            Gesten.AddOrUpdate(steuerelement, geste);
        }

        geste.Skala *= 1 + e.Delta.X;
        geste.Zuletzt = jetzt;
        ansicht.Zoom.Skalieren(geste.Start, geste.Skala);
        e.Handled = true;
    }

    /// <summary>Eine laufende Geste.</summary>
    /// <param name="start">Die Vergrößerung zu Beginn in Prozent.</param>
    private sealed class Geste(int start)
    {
        public int Start { get; } = start;

        public double Skala { get; set; } = 1;

        public DateTime Zuletzt { get; set; } = DateTime.UtcNow;
    }
}
