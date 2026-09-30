// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Zeile mit Häkchen, z. B. ein Gegner der 60-km-Regel.</summary>
public sealed class AuswahlZeile : ObservableObject
{
    private readonly Action geaendert;
    private bool gewaehlt;

    /// <summary>Initialisiert die Zeile.</summary>
    /// <param name="name">Angezeigter Name.</param>
    /// <param name="gewaehlt">Anfangszustand des Häkchens.</param>
    /// <param name="geaendert">Wird nach einer Änderung aufgerufen.</param>
    public AuswahlZeile(string name, bool gewaehlt, Action geaendert)
    {
        Name = name;
        this.gewaehlt = gewaehlt;
        this.geaendert = geaendert;
    }

    /// <summary>Holt den Namen.</summary>
    public string Name { get; }

    /// <summary>Holt oder setzt das Häkchen.</summary>
    public bool Gewaehlt
    {
        get => gewaehlt;
        set
        {
            if (SetProperty(ref gewaehlt, value))
            {
                geaendert();
            }
        }
    }

    /// <summary>Setzt das Häkchen ohne Änderungsmeldung (Laden, Standard).</summary>
    /// <param name="wert">Neuer Zustand.</param>
    internal void Setzen(bool wert) => SetProperty(ref gewaehlt, wert, nameof(Gewaehlt));
}
