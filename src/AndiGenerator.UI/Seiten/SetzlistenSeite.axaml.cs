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
    /// <summary>So weit (Pixel, im Fenster) muss sich die Maus bewegen, bis die Zeile wirklich verschoben wird.</summary>
    private const double Ziehschwelle = 4;

    private bool ziehen;
    private bool bewegt;
    private Point start;
    private double griffhoehe;

    /// <summary>Initialisiert die Seite.</summary>
    public SetzlistenSeite()
    {
        InitializeComponent();

        // Den Fokus (für Escape) holen, ohne dass die umgebende Bildlaufleiste dorthin springt: Sonst rutscht die Liste unter
        // dem ruhenden Mauszeiger weg, und ein Klick auf den Griff verschiebt die Zeile.
        AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled |= ReferenceEquals(e.TargetObject, this));
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
            bewegt = false;
            start = e.GetPosition(TopLevel.GetTopLevel(this));
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

        // Erst eine echte Mausbewegung (in Fensterkoordinaten, nicht bloßer Bildlauf) verschiebt die Zeile.
        bewegt |= Point.Distance(e.GetPosition(TopLevel.GetTopLevel(this)), start) >= Ziehschwelle;
        if (!bewegt)
        {
            return;
        }

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
        Modell?.ZiehenBeenden(uebernehmen && bewegt);
    }
}
