// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AndiGenerator.UI.Ansichten;

/// <summary>
/// Qualitätsansicht: Verstöße je Kriterium nach der Rangfolge A/B/C. Im Automodus lassen sich Zeilen am Griff ziehen:
/// Die Zeile hängt dann am Mauszeiger, und die Tabelle zeigt an der Ablagestelle eine Lücke mit der Nummerierung, die beim
/// Ablegen entstünde (Vorschau im ViewModel).
/// </summary>
public partial class QualitaetAnsicht : UserControl
{
    private bool ziehen;
    private double griffhoehe;

    /// <summary>Initialisiert die Ansicht.</summary>
    public QualitaetAnsicht()
    {
        InitializeComponent();
        Mausradzoom.Anmelden(this);
        Focusable = true;
        KeyDown += (_, e) =>
        {
            if (ziehen && e.Key == Key.Escape)
            {
                Beenden(uebernehmen: false, null);
                e.Handled = true;
            }
        };
    }

    private QualitaetAnsichtViewModel? Modell => DataContext as QualitaetAnsichtViewModel;

    /// <summary>
    /// Druck auf einen Griff (Border der Klasse <c>griff</c>) beginnt das Ziehen seiner Zeile; Klick sonst auf die Zeile
    /// wählt sie (Verstöße rechts daneben). Knöpfe der Zeile behandeln ihren Klick selbst.
    /// </summary>
    private void ListeGedrueckt(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Border? griff = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("griff"));
        if (griff is { DataContext: Qualitaetszeile zeile } && Modell is { } modell && modell.ZiehenBeginnen(zeile))
        {
            ziehen = true;
            Border? zeilenrand = griff.GetVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("zeile"));
            griffhoehe = zeilenrand is null ? 0 : e.GetPosition(zeilenrand).Y;
            Schwebezeile.Width = Liste.Bounds.Width;
            Mitfuehren(e);
            e.Pointer.Capture(Liste);
            Focus();
            e.Handled = true;
            return;
        }

        Border? zeilenrahmen = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("zeile"));
        if (zeilenrahmen is { DataContext: Qualitaetszeile gewaehlt } && Modell is { } m)
        {
            m.Waehlen(gewaehlt);
            e.Handled = true;
        }
    }

    private void ListeBewegt(object? sender, PointerEventArgs e)
    {
        if (!ziehen || Modell is not { } modell)
        {
            return;
        }

        Mitfuehren(e);

        // Zeile unter dem Zeiger: obere Hälfte = davor, untere Hälfte = dahinter.
        foreach (Control behaelter in Liste.GetRealizedContainers())
        {
            Point p = e.GetPosition(behaelter);
            if (p.Y >= 0 && p.Y < behaelter.Bounds.Height && behaelter.DataContext is Qualitaetszeile ziel)
            {
                modell.ZiehenUeber(ziel, p.Y < behaelter.Bounds.Height / 2);
                return;
            }
        }
    }

    /// <summary>Hängt die schwebende Zeile an den Mauszeiger (senkrecht beweglich, bündig mit der Tabelle).</summary>
    private void Mitfuehren(PointerEventArgs e)
    {
        Canvas.SetLeft(Schwebezeile, Liste.TranslatePoint(default, Schwebeebene)?.X ?? 0);
        Canvas.SetTop(Schwebezeile, e.GetPosition(Schwebeebene).Y - griffhoehe);
    }

    private void ListeLosgelassen(object? sender, PointerReleasedEventArgs e) => Beenden(uebernehmen: true, e.Pointer);

    private void ListeVerloren(object? sender, PointerCaptureLostEventArgs e) => Beenden(uebernehmen: false, null);

    private void Beenden(bool uebernehmen, IPointer? zeiger)
    {
        if (!ziehen)
        {
            return;
        }

        ziehen = false;
        zeiger?.Capture(null);
        Modell?.ZiehenBeenden(uebernehmen);
    }
}
