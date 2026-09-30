// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Zeile der Pflichtspieltage: Zeitraum mit direkt wählbarer Anzahl Spiele.</summary>
public sealed class PflichtspielzeitZeile : ObservableObject
{
    private readonly Action geaendert;
    private int anzahl;

    /// <summary>Initialisiert die Zeile.</summary>
    /// <param name="zeit">Der Zeitraum.</param>
    /// <param name="geaendert">Wird nach einer Änderung der Anzahl aufgerufen.</param>
    public PflichtspielzeitZeile(Pflichtspielzeit zeit, Action geaendert)
    {
        ArgumentNullException.ThrowIfNull(zeit);
        Von = zeit.Von;
        Bis = zeit.Bis;
        anzahl = zeit.Anzahl;
        this.geaendert = geaendert;
    }

    /// <summary>Holt die wählbaren Anzahlen.</summary>
    public static IReadOnlyList<int> Anzahlen => Pflichtspielzeit.Anzahlen;

    /// <summary>Holt die Beschriftung des Zeitraums.</summary>
    public string Bezeichnung => Wert.Bezeichnung;

    /// <summary>Holt oder setzt die Anzahl Spiele.</summary>
    public int Anzahl
    {
        get => anzahl;
        set
        {
            if (value > 0 && SetProperty(ref anzahl, value))
            {
                geaendert();
            }
        }
    }

    /// <summary>Holt den Zeitraum mit der aktuellen Anzahl.</summary>
    public Pflichtspielzeit Wert => new(Von, Bis, anzahl);

    private DateOnly Von { get; }

    private DateOnly Bis { get; }
}
