// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Zeile der Heimkoppel-Seite: Tag bzw. Tagespaar mit Auswahl des Wunschs.</summary>
public sealed class HeimkoppelZeile : ObservableObject
{
    private readonly Action geaendert;
    private int index;

    /// <summary>Initialisiert die Zeile.</summary>
    /// <param name="koppel">Der Wunsch.</param>
    /// <param name="geaendert">Wird nach einer Änderung aufgerufen.</param>
    public HeimkoppelZeile(Heimkoppel koppel, Action geaendert)
    {
        ArgumentNullException.ThrowIfNull(koppel);
        Koppel = koppel;
        Auswahl = koppel.Auswahl();
        index = koppel.Index;
        this.geaendert = geaendert;
    }

    /// <summary>Holt die Beschriftung.</summary>
    public string Beschriftung => Koppel.Beschriftung;

    /// <summary>Holt die Wahlmöglichkeiten.</summary>
    public IReadOnlyList<string> Auswahl { get; }

    /// <summary>Holt oder setzt die gewählte Möglichkeit.</summary>
    public int Index
    {
        get => index;
        set
        {
            if (value >= 0 && SetProperty(ref index, value))
            {
                geaendert();
            }
        }
    }

    /// <summary>Holt den Wunsch mit der aktuellen Wahl.</summary>
    public Heimkoppel Wert => Koppel.MitIndex(index);

    private Heimkoppel Koppel { get; }
}
