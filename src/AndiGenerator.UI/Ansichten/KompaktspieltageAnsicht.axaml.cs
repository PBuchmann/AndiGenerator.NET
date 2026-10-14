// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AndiGenerator.UI.Ansichten;

/// <summary>
/// Ansicht „Kompaktspieltage“: Pläne, Raster aus Meldungen und Plan, Karte, Spieltage und Hinweise. Im Bearbeitungsmodus
/// wählt ein Klick oder eine Pfeiltaste ein Feld des Rasters; A, H, B und Entf setzen die Meldung.
/// </summary>
public partial class KompaktspieltageAnsicht : UserControl
{
    /// <summary>Initialisiert die Ansicht.</summary>
    public KompaktspieltageAnsicht()
    {
        InitializeComponent();
        Raster.AddHandler(PointerPressedEvent, RasterGedrueckt, RoutingStrategies.Tunnel);
        Raster.KeyDown += RasterTaste;

        // Den Fokus holen, ohne dass die Ansicht zum Raster rollt (sonst springt sie bei jedem Klick).
        Raster.AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled |= ReferenceEquals(e.TargetObject, Raster));
    }

    private KompaktspieltageAnsichtViewModel? Modell => DataContext as KompaktspieltageAnsichtViewModel;

    private void RasterGedrueckt(object? sender, PointerPressedEventArgs e)
    {
        if (Modell is not { Bearbeiten: true } modell
            || (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("zelle")) is not { DataContext: Kompaktzelle zelle })
        {
            return;
        }

        modell.ZelleWaehlenCommand.Execute(zelle);
        Raster.Focus();
        e.Handled = true;
    }

    private void RasterTaste(object? sender, KeyEventArgs e)
    {
        if (Modell is not { Bearbeiten: true } modell || e.Source is TextBox or ComboBox or CalendarDatePicker)
        {
            return;
        }

        string? art = e.Key switch
        {
            Key.A => "A",
            Key.H => "H",
            Key.B => "B",
            Key.Delete or Key.Back or Key.Space => "-",
            _ => null,
        };
        (int Spalten, int Zeilen) schritt = e.Key switch
        {
            Key.Left => (-1, 0),
            Key.Right => (1, 0),
            Key.Up => (0, -1),
            Key.Down => (0, 1),
            _ => (0, 0),
        };
        if (art is not null)
        {
            modell.FeldSetzenCommand.Execute(art);
        }
        else if (schritt != (0, 0))
        {
            modell.CursorBewegen(schritt.Spalten, schritt.Zeilen);
        }
        else
        {
            return;
        }

        e.Handled = true;
    }
}
