// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;

namespace AndiGenerator.UI.Tests;

/// <summary>Zoom der Ansichten: Strg und Mausrad, zwei Finger auf dem Bildschirm und auf dem Touchpad.</summary>
[Collection(OberflaechenSammlung.Name)]
public sealed class ZoomgestenTests
{
    private readonly Oberflaeche oberflaeche;

    public ZoomgestenTests(Oberflaeche oberflaeche) => this.oberflaeche = oberflaeche;

    [Fact]
    public Task Mausrad_mit_Strg_zoomt_in_Stufen() => oberflaeche.Ausfuehren(() =>
    {
        (Window fenster, Border flaeche, Zoom zoom) = Anzeigen();
        try
        {
            var mitte = new Point(200, 150);
            fenster.MouseWheel(mitte, new Vector(0, 1));
            Assert.Equal(100, zoom.Prozent);

            fenster.MouseWheel(mitte, new Vector(0, 1), RawInputModifiers.Control);
            Assert.Equal(110, zoom.Prozent);
            fenster.MouseWheel(mitte, new Vector(0, -1), RawInputModifiers.Control);
            fenster.MouseWheel(mitte, new Vector(0, -1), RawInputModifiers.Control);
            Assert.Equal(90, zoom.Prozent);
            fenster.MouseWheel(mitte, new Vector(1, 0), RawInputModifiers.Control);
            Assert.Equal(90, zoom.Prozent);
            Assert.NotNull(flaeche);
        }
        finally
        {
            fenster.Close();
        }

        return Task.CompletedTask;
    });

    [Fact]
    public Task Zwei_Finger_zoomen_ab_dem_Beginn_der_Geste() => oberflaeche.Ausfuehren(() =>
    {
        (Window fenster, Border flaeche, Zoom zoom) = Anzeigen();
        try
        {
            flaeche.RaiseEvent(new PinchEventArgs(1.2, default, 0, 0) { RoutedEvent = InputElement.PinchEvent });
            int nachErster = zoom.Prozent;
            Assert.True(nachErster > 100);

            // Die Skala gilt seit Beginn: 1,5 nach 1,2 ist nicht 1,2 · 1,5.
            flaeche.RaiseEvent(new PinchEventArgs(1.5, default, 0, 0) { RoutedEvent = InputElement.PinchEvent });
            Assert.InRange(zoom.Prozent, 145, 155);
            flaeche.RaiseEvent(new PinchEndedEventArgs { RoutedEvent = InputElement.PinchEndedEvent });

            // Neue Geste: Start ist die jetzige Vergrößerung.
            int start = zoom.Prozent;
            flaeche.RaiseEvent(new PinchEventArgs(0.5, default, 0, 0) { RoutedEvent = InputElement.PinchEvent });
            Assert.InRange(zoom.Prozent, (start / 2) - 5, (start / 2) + 5);
        }
        finally
        {
            fenster.Close();
        }

        return Task.CompletedTask;
    });

    [Fact]
    public Task Touchpad_meldet_die_Aenderung_seit_der_vorigen_Meldung() => oberflaeche.Ausfuehren(() =>
    {
        (Window fenster, Border flaeche, Zoom zoom) = Anzeigen();
        try
        {
            Touchpad(flaeche, 0.1);
            Touchpad(flaeche, 0.1);
            Assert.InRange(zoom.Prozent, 115, 126);
        }
        finally
        {
            fenster.Close();
        }

        return Task.CompletedTask;
    });

    private static void Touchpad(Border flaeche, double delta)
    {
        var zeiger = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        flaeche.RaiseEvent(new PointerDeltaEventArgs(
            InputElement.PointerTouchPadGestureMagnifyEvent,
            flaeche,
            zeiger,
            flaeche,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
            KeyModifiers.None,
            new Vector(delta, 0)));
    }

    private static (Window Fenster, Border Flaeche, Zoom Zoom) Anzeigen()
    {
        var ansicht = new Zoomansicht();
        var flaeche = new Border { DataContext = ansicht, Background = Avalonia.Media.Brushes.White };
        Ansichtszoom.Anmelden(flaeche);
        var fenster = new Window { Width = 400, Height = 300, Content = flaeche };
        fenster.Show();
        Oberflaeche.Zeichnen(fenster);
        return (fenster, flaeche, ansicht.Zoom);
    }

    private sealed class Zoomansicht : IZoombar
    {
        public Zoom Zoom { get; } = new();
    }
}
