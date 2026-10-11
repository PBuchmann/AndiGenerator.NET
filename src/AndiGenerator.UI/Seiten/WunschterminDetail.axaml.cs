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
/// Wunschtermine einer Mannschaft: Wochenraster mit einem Kärtchen je Tag. Ein Klick öffnet den Dialog „Wunschtermin“;
/// ein Termin lässt sich auf einen anderen Tag ziehen (das Kärtchen hängt am Mauszeiger, der Zieltag ist umrandet; mit
/// Strg wird kopiert, Escape bricht ab). Rechtsklick öffnet das Kontextmenü (Bearbeiten, Löschen).
/// </summary>
public partial class WunschterminDetail : UserControl
{
    /// <summary>So weit (Pixel) muss sich die Maus bewegen, bis aus einem Klick ein Ziehen wird.</summary>
    private const double Ziehschwelle = 5;

    private WunschterminZelle? gedrueckt;
    private Point start;
    private Point griff;
    private bool ziehen;
    private WunschterminZelle? ziel;

    /// <summary>Initialisiert die Ansicht.</summary>
    public WunschterminDetail()
    {
        InitializeComponent();
        Focusable = true;
        AddHandler(PointerPressedEvent, Gedrueckt, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, Bewegt, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, Losgelassen, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, (_, _) => Abbrechen(), RoutingStrategies.Bubble, handledEventsToo: true);
        KeyDown += (_, e) =>
        {
            if (gedrueckt is not null && e.Key == Key.Escape)
            {
                Abbrechen();
                e.Handled = true;
            }
        };
    }

    private WunschterminDetailViewModel? Modell => DataContext as WunschterminDetailViewModel;

    private static Border? Kaertchen(object? quelle) =>
        (quelle as Visual)?.GetSelfAndVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("kaertchen"));

    private void Gedrueckt(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Kaertchen(e.Source) is not { DataContext: WunschterminZelle zelle } karte)
        {
            return;
        }

        gedrueckt = zelle;
        start = e.GetPosition(this);
        griff = e.GetPosition(karte);
        e.Pointer.Capture(this);
        Focus();
        e.Handled = true;
    }

    private void Bewegt(object? sender, PointerEventArgs e)
    {
        if (gedrueckt is not { } zelle)
        {
            return;
        }

        Point jetzt = e.GetPosition(this);
        if (!ziehen)
        {
            if (!zelle.HatTermin || Point.Distance(jetzt, start) < Ziehschwelle)
            {
                return;
            }

            // Ab hier wird gezogen: eine Kopie des Kärtchens hängt am Mauszeiger, das Original wird blass.
            ziehen = true;
            zelle.IstQuelle = true;
            Schwebeinhalt.Content = new WunschterminZelle(zelle.Tag, zelle.Text, () => { });
            Schwebekarte.IsVisible = true;
        }

        Canvas.SetLeft(Schwebekarte, e.GetPosition(Schwebeebene).X - griff.X);
        Canvas.SetTop(Schwebekarte, e.GetPosition(Schwebeebene).Y - griff.Y);
        ZielSetzen(Kaertchen(Raster.InputHitTest(e.GetPosition(Raster)))?.DataContext as WunschterminZelle);
    }

    private void Losgelassen(object? sender, PointerReleasedEventArgs e)
    {
        if (gedrueckt is not { } zelle)
        {
            return;
        }

        bool gezogen = ziehen;
        WunschterminZelle? ablage = ziel;
        Aufraeumen(e.Pointer);
        e.Handled = true;
        if (!gezogen)
        {
            // Klick ohne Ziehen: Termin eintragen bzw. bearbeiten.
            Modell?.BearbeitenCommand.Execute(zelle);
        }
        else if (ablage is not null)
        {
            WunschterminDetailViewModel.Verschieben(zelle, ablage, kopieren: e.KeyModifiers.HasFlag(KeyModifiers.Control));
        }
    }

    private void ZielSetzen(WunschterminZelle? neu)
    {
        if (ReferenceEquals(neu, gedrueckt))
        {
            neu = null;
        }

        if (!ReferenceEquals(neu, ziel))
        {
            ziel?.IstZiel = false;
            ziel = neu;
            ziel?.IstZiel = true;
        }
    }

    private void Abbrechen() => Aufraeumen(null);

    private void Aufraeumen(IPointer? zeiger)
    {
        WunschterminZelle? zelle = gedrueckt;
        gedrueckt = null;
        ziehen = false;
        ZielSetzen(null);
        zelle?.IstQuelle = false;
        Schwebekarte.IsVisible = false;
        Schwebeinhalt.Content = null;
        zeiger?.Capture(null);
    }

    private void Bearbeiten(object? sender, RoutedEventArgs e) => Ausfuehren(sender, bearbeiten: true);

    private void Loeschen(object? sender, RoutedEventArgs e) => Ausfuehren(sender, bearbeiten: false);

    private void Ausfuehren(object? sender, bool bearbeiten)
    {
        if (sender is not Control { DataContext: WunschterminZelle zelle } || Modell is not { } detail)
        {
            return;
        }

        if (bearbeiten)
        {
            detail.BearbeitenCommand.Execute(zelle);
        }
        else
        {
            detail.LoeschenCommand.Execute(zelle);
        }
    }
}
