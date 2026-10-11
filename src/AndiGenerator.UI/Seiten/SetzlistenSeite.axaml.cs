// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AndiGenerator.UI.Seiten;

/// <summary>
/// Seite „Setzliste“ des Dialogs „Spielplandaten bearbeiten“. Wie in der Qualitätsansicht wird eine Mannschaft am Griff
/// gezogen: Die Zeile hängt am Mauszeiger, und die Liste zeigt an der Ablagestelle eine Lücke mit den Plätzen, die beim
/// Ablegen entstünden. Escape bricht ab. Ein Klick auf eine Zeile wählt sie für „Nach oben“/„Nach unten“.
/// </summary>
public partial class SetzlistenSeite : UserControl
{
    private bool ziehen;
    private double griffhoehe;

    /// <summary>Initialisiert die Seite.</summary>
    public SetzlistenSeite()
    {
        InitializeComponent();
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

    private SetzlistenSeiteViewModel? Modell => DataContext as SetzlistenSeiteViewModel;

    private void ListeGedrueckt(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Modell is not { } modell)
        {
            return;
        }

        Border? zeile = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("setzzeile"));
        int index = zeile?.DataContext is Setzlistenzeile z ? modell.Zeilen.IndexOf(z) : -1;
        if (zeile is null || index < 0)
        {
            return;
        }

        bool amGriff = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<Border>().Any(b => b.Classes.Contains("griff")) == true;
        if (amGriff && modell.ZiehenBeginnen(index))
        {
            ziehen = true;
            griffhoehe = e.GetPosition(zeile).Y;
            Schwebezeile.Width = Liste.Bounds.Width;
            Mitfuehren(e);
            e.Pointer.Capture(Liste);
            Focus();
        }
        else
        {
            modell.Waehlen(index);
        }

        e.Handled = true;
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
            if (p.Y >= 0 && p.Y < behaelter.Bounds.Height && behaelter.DataContext is Setzlistenzeile ziel)
            {
                modell.ZiehenUeber(modell.Zeilen.IndexOf(ziel), p.Y < behaelter.Bounds.Height / 2);
                return;
            }
        }
    }

    /// <summary>Hängt die schwebende Zeile an den Mauszeiger (senkrecht beweglich, bündig mit der Liste).</summary>
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
