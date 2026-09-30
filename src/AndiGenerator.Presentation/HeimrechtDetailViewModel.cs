// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>
/// Heimrecht einer Mannschaft (Original <c>TDialogPanelHomeRightDetail</c>): Zahl der Heimspiele in der Vorrunde und je
/// Gegner die Runde mit Heimrecht. „Standard“ bedeutet wie im Original „Automatisch“ und überall „egal“, nicht der
/// click-TT-Stand.
/// </summary>
public sealed class HeimrechtDetailViewModel : DatenSeiteViewModel
{
    private readonly string mannschaft;
    private int geladen;
    private int anzahlIndex;

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="mannschaft">Mannschaftsname.</param>
    public HeimrechtDetailViewModel(Datenbearbeitung bearbeitung, string mannschaft)
        : base(bearbeitung, "Heimrecht")
    {
        this.mannschaft = mannschaft;
        int n = bearbeitung.Mannschaftsnamen().Count;
        Anzahlen =
        [
            "Automatisch",
            .. Enumerable.Range(0, n).Select(i => string.Create(CultureInfo.InvariantCulture, $"Vorrunde: {i}      Rückrunde: {n - i - 1}")),
        ];
        Gegner = bearbeitung.HeimrechtLesen(mannschaft).Gegner.Select(g => new HeimrechtZeile(g.Key, g.Value, AenderungMelden)).ToList();
        Laden();
    }

    /// <summary>Holt die Auswahl „Anzahl Heimspiele“.</summary>
    public IReadOnlyList<string> Anzahlen { get; }

    /// <summary>Holt oder setzt die gewählte Anzahl (0 = Automatisch, sonst Heimspiele in der Vorrunde + 1).</summary>
    public int AnzahlIndex
    {
        get => anzahlIndex;
        set
        {
            if (value >= 0 && SetProperty(ref anzahlIndex, value))
            {
                AenderungMelden();
            }
        }
    }

    /// <summary>Holt die Gegner mit ihrem Heimrecht.</summary>
    public IReadOnlyList<HeimrechtZeile> Gegner { get; }

    /// <inheritdoc/>
    public override bool IstStandard => AnzahlIndex == 0 && Gegner.All(g => g.Wert == Heimrecht.Egal);

    /// <inheritdoc/>
    public override void Laden()
    {
        Heimrecht daten = Bearbeitung.HeimrechtLesen(mannschaft);
        geladen = daten.HeimspieleVorrunde;

        // Im Original blieb die Auswahl bei einem Wert außerhalb der Liste leer; ein Speichern hätte ihn zerstört.
        anzahlIndex = geladen >= Heimrecht.Automatisch && geladen < Anzahlen.Count - 1 ? geladen + 1 : -1;
        OnPropertyChanged(nameof(AnzahlIndex));
        foreach (HeimrechtZeile g in Gegner)
        {
            g.Setzen(daten.Gegner.GetValueOrDefault(g.Gegner, Heimrecht.Egal));
        }
    }

    /// <inheritdoc/>
    public override void Speichern()
    {
        int anzahl = AnzahlIndex < 0 ? geladen : AnzahlIndex - 1;
        Bearbeitung.HeimrechtSpeichern(mannschaft, new Heimrecht(anzahl, Gegner.ToDictionary(g => g.Gegner, g => g.Wert)));
    }

    /// <inheritdoc/>
    public override void Standard()
    {
        anzahlIndex = 0;
        OnPropertyChanged(nameof(AnzahlIndex));
        foreach (HeimrechtZeile g in Gegner)
        {
            g.Setzen(Heimrecht.Egal);
        }

        AenderungMelden();
    }
}
