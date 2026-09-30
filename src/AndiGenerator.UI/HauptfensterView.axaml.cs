// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;

namespace AndiGenerator.UI;

/// <summary>
/// Hauptfenster: Navigation links, Kopf mit Staffel und Generierung, Kacheln mit dem Stand, Andock-Bereich für die
/// Ansichten; ohne geöffnete Staffel die Startseite. Passt sich der Fenstergröße an und wählt beim ersten Anzeigen
/// der Ansichten eine Vergrößerung passend zum Bildschirm.
/// </summary>
public partial class HauptfensterView : Window
{
    /// <summary>Unterhalb dieser Fensterbreite zeigt die Navigation nur Symbole (Tablet hochkant, kleine Fenster).</summary>
    private const double SchmaleBreite = 1100;

    /// <summary>Unterhalb dieser Fensterbreite stehen die Kacheln 2 × 2.</summary>
    private const double EngeBreite = 1000;

    /// <summary>Unterhalb dieser Fensterhöhe werden Kopf und Kacheln kompakt (z. B. Laptop mit 768 Pixeln).</summary>
    private const double NiedrigeHoehe = 820;

    /// <summary>Platz, den Reiter, Leiste und Ränder einer Ansicht über ihrem Inhalt brauchen.</summary>
    private const double Ansichtskopf = 140;

    private bool startzoomGesetzt;

    /// <summary>Initialisiert das Fenster.</summary>
    public HauptfensterView()
    {
        InitializeComponent();
        SizeChanged += (_, e) =>
        {
            Navigation.Classes.Set("schmal", e.NewSize.Width < SchmaleBreite);
            Classes.Set("eng", e.NewSize.Width < EngeBreite);
            Classes.Set("niedrig", e.NewSize.Height < NiedrigeHoehe);
        };
        Ansichten.SizeChanged += (_, e) =>
        {
            // Einmal, sobald die Ansichten zum ersten Mal sichtbar sind (nach dem Öffnen einer Staffel).
            if (!startzoomGesetzt && e.NewSize.Width > 0 && DataContext is HauptfensterViewModel modell)
            {
                startzoomGesetzt = true;
                modell.StartzoomFestlegen(Zoom.Vorschlag(e.NewSize.Width - 40, e.NewSize.Height - Ansichtskopf));
            }
        };
    }
}
