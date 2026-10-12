// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Ein Rechenkern im Maschinenraum: wie viele Pläne er gerade schafft, im Vergleich zum schnellsten Kern.</summary>
public sealed class Kernanzeige : ObservableObject
{
    /// <summary>Höhe eines ruhenden Kerns in der Kachel (Pixel).</summary>
    private const double Ruhe = 3;

    /// <summary>Zusätzliche Höhe bei voller Last in der Kachel (Pixel).</summary>
    private const double Vollast = 19;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private double hoehe = Ruhe;
    private double last;
    private string tempo = string.Empty;

    /// <summary>Initialisiert den Kern bzw. eine Gruppe von Kernen.</summary>
    /// <param name="name">Name für den Hinweis, z. B. „Kern 3“ oder „Kerne 1–12“.</param>
    public Kernanzeige(string name) => Name = name;

    /// <summary>Holt den Namen für den Hinweis.</summary>
    public string Name { get; }

    /// <summary>Holt die Last im Verhältnis zum schnellsten Kern (0 bis 1).</summary>
    public double Last
    {
        get => last;
        private set
        {
            if (SetProperty(ref last, value))
            {
                OnPropertyChanged(nameof(Deckkraft));
            }
        }
    }

    /// <summary>Holt die Deckkraft des Kästchens in der Rasterdarstellung vieler Kerne (kräftig = volle Last).</summary>
    public double Deckkraft => 0.12 + (0.88 * Last);

    /// <summary>Holt die Höhe des Balkens in der Kachel.</summary>
    public double Hoehe
    {
        get => hoehe;
        private set => SetProperty(ref hoehe, value);
    }

    /// <summary>Holt den Hinweis: Kern und Pläne je Sekunde.</summary>
    public string Tempo
    {
        get => tempo;
        private set => SetProperty(ref tempo, value);
    }

    /// <summary>Übernimmt die Last des Kerns.</summary>
    /// <param name="anteil">Pläne je Sekunde im Verhältnis zum schnellsten Kern (0 bis 1; 0 = ruht).</param>
    /// <param name="plaeneProSekunde">Pläne je Sekunde des Kerns.</param>
    internal void Setzen(double anteil, double plaeneProSekunde)
    {
        Last = Math.Clamp(anteil, 0, 1);
        Hoehe = Ruhe + (Vollast * Last);
        Tempo = string.Create(Deutsch, $"{Name}: {plaeneProSekunde:N0} Pläne/s");
    }
}
