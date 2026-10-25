// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

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

    /// <summary>Ersatz, wenn ein Ziel gerade nicht zu sehen ist (z. B. „B-Varianten“ ohne Varianten: die Kachel von HiGHS).</summary>
    private static readonly Dictionary<string, string> Ersatzziele = new(StringComparer.Ordinal)
    {
        [Fuehrungsziel.BVarianten] = Fuehrungsziel.KachelLeitstand,
        [Fuehrungsziel.Stopp] = Fuehrungsziel.Start,
    };

    private bool startzoomGesetzt;
    private bool beendenBestaetigt;
    private HauptfensterViewModel? angemeldet;

    /// <summary>Initialisiert das Fenster.</summary>
    public HauptfensterView()
    {
        InitializeComponent();
        Closing += BeimSchliessen;
        DataContextChanged += (_, _) =>
        {
            if (angemeldet is not null)
            {
                angemeldet.ZielHervorheben -= Hervorheben;
            }

            angemeldet = DataContext as HauptfensterViewModel;
            if (angemeldet is not null)
            {
                angemeldet.ZielHervorheben += Hervorheben;
            }
        };
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

    /// <summary>Sucht das sichtbare Element mit einer Fuehrungsmarke (auch in den Ansichten).</summary>
    /// <param name="ziel">Name aus <see cref="Fuehrungsziel"/>.</param>
    /// <returns>Das Element oder <c>null</c>.</returns>
    internal Control? ZielSuchen(string ziel) =>
        this.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => Fuehrungsmarke.GetZiel(c) == ziel && c.IsEffectivelyVisible && c.Bounds.Width > 0);

    /// <summary>Richtet den Scheinwerfer auf ein Element (rollt es vorher ins Bild).</summary>
    /// <param name="ziel">Name aus <see cref="Fuehrungsziel"/>.</param>
    /// <returns><c>true</c>, wenn es das Element gibt.</returns>
    internal bool ScheinwerferRichten(string ziel)
    {
        if ((ZielSuchen(ziel) ?? (Ersatzziele.TryGetValue(ziel, out string? ersatz) ? ZielSuchen(ersatz) : null)) is not { } element)
        {
            Licht.Ausschalten();
            return false;
        }

        element.BringIntoView();
        UpdateLayout();
        if (element.TranslatePoint(default, Licht) is not Point oben)
        {
            return false;
        }

        // Nur der sichtbare Teil (große Elemente wie das Raster reichen über das Fenster hinaus).
        Rect bereich = new Rect(oben, element.Bounds.Size).Intersect(new Rect(Licht.Bounds.Size).Deflate(8));
        Licht.Zeigen(bereich);
        return true;
    }

    /// <summary>
    /// Vor dem Beenden des Programms: Ein noch nicht gesicherter Plan wird gesichert bzw. es wird gefragt, ob er den
    /// gesicherten ersetzen soll. Dafür wird das Schließen zunächst abgebrochen und nach der Antwort wiederholt.
    /// </summary>
    // Ereignisbehandlung des Fensters: muss void sein; Fehler landen über den Dispatcher in der Fehlerbehandlung.
#pragma warning disable VSTHRD100
    private async void BeimSchliessen(object? sender, WindowClosingEventArgs e)
#pragma warning restore VSTHRD100
    {
        if (beendenBestaetigt || DataContext is not HauptfensterViewModel modell)
        {
            return;
        }

        e.Cancel = true;
        if (await modell.BeendenErlaubtAsync())
        {
            beendenBestaetigt = true;
            Close();
        }
    }

    /// <summary>Die Führung zeigt ein Element: Links die Abschnitte aufklappen, nach dem Aufbau der Ansichten den Scheinwerfer richten.</summary>
    private void Hervorheben(object? sender, string ziel)
    {
        if (ziel.StartsWith("Nav", StringComparison.Ordinal))
        {
            KopfStaffel.IsChecked = true;
            KopfAnsichten.IsChecked = true;
            KopfAusgabe.IsChecked = true;
        }

        Dispatcher.UIThread.Post(() => ScheinwerferRichten(ziel), DispatcherPriority.Background);
    }
}
