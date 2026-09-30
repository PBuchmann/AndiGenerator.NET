// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>
/// Vergrößerung einer Ansicht (Original: Zoomstufen 25–200 % im Hauptfenster, dort für alle Reiter gemeinsam). Hier hat
/// jede Ansicht ihre eigene Stufe, weil mehrere Ansichten gleichzeitig sichtbar sein können. Der Schieberegler stellt
/// stufenlos in 5-%-Schritten ein, Tasten und Mausrad springen wie im Browser zur nächsten Stufe.
/// </summary>
public sealed class Zoom : ObservableObject
{
    /// <summary>Breite, für die die Ansichten gestaltet sind (größte Tabelle: Kosten mit allen Spalten).</summary>
    public const double Bezugsbreite = 1200;

    /// <summary>Höhe, für die die Ansichten gestaltet sind.</summary>
    public const double Bezugshoehe = 600;

    private int prozent = 100;

    /// <summary>Holt die Stufen in Prozent, zu denen Tasten und Mausrad springen (wie im Browser).</summary>
    public static IReadOnlyList<int> Stufen { get; } = [25, 33, 50, 67, 75, 80, 90, 100, 110, 125, 150, 175, 200, 250, 300];

    /// <summary>Holt die kleinste Vergrößerung in Prozent (auch Minimum des Schiebereglers in der Oberfläche).</summary>
    public static int Minimum => Stufen[0];

    /// <summary>Holt die größte Vergrößerung in Prozent (auch Maximum des Schiebereglers in der Oberfläche).</summary>
    public static int Maximum => Stufen[^1];

    /// <summary>Holt oder setzt die Vergrößerung in Prozent (begrenzt auf die kleinste und größte Stufe).</summary>
    public int Prozent
    {
        get => prozent;
        set
        {
            if (SetProperty(ref prozent, Math.Clamp(value, Minimum, Maximum)))
            {
                OnPropertyChanged(nameof(Faktor));
                OnPropertyChanged(nameof(Schieber));
                OnPropertyChanged(nameof(Text));
            }
        }
    }

    /// <summary>Holt oder setzt die Vergrößerung für den Schieberegler; gerundet auf 5 %.</summary>
    public double Schieber
    {
        get => Prozent;
        set => Prozent = (int)Math.Round(value / 5, MidpointRounding.AwayFromZero) * 5;
    }

    /// <summary>Holt die Vergrößerung als Text, z. B. <c>125 %</c>.</summary>
    public string Text => Prozent.ToString(CultureInfo.InvariantCulture) + " %";

    /// <summary>Holt den Vergrößerungsfaktor (1 = 100 %).</summary>
    public double Faktor => Prozent / 100.0;

    /// <summary>
    /// Schlägt die Vergrößerung beim Start vor: so, dass eine Ansicht den verfügbaren Platz sinnvoll füllt – auf kleinen
    /// Bildschirmen verkleinert, auf großen vergrößert, gerundet auf 5 % und begrenzt auf 75–175 %.
    /// </summary>
    /// <param name="breite">Verfügbare Breite für die Ansichten (geräteunabhängige Pixel).</param>
    /// <param name="hoehe">Verfügbare Höhe für die Ansichten.</param>
    /// <returns>Die Vergrößerung in Prozent.</returns>
    public static int Vorschlag(double breite, double hoehe)
    {
        if (breite <= 0 || hoehe <= 0)
        {
            return 100;
        }

        double faktor = Math.Min(breite / Bezugsbreite, hoehe / Bezugshoehe);
        int prozent = (int)(Math.Round(faktor * 20, MidpointRounding.AwayFromZero) * 5);
        return Math.Clamp(prozent, 75, 175);
    }

    /// <summary>Wechselt zur nächstgrößeren Stufe.</summary>
    public void Groesser() => Prozent = Stufen.FirstOrDefault(s => s > Prozent, Maximum);

    /// <summary>Wechselt zur nächstkleineren Stufe.</summary>
    public void Kleiner() => Prozent = Stufen.LastOrDefault(s => s < Prozent, Minimum);

    /// <summary>Setzt die Vergrößerung auf 100 % zurück.</summary>
    public void Zuruecksetzen() => Prozent = 100;
}
