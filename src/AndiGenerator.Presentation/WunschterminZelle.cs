// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Ein Tag der Terminübersicht; die Eingabe wird wie im Original beim Verlassen der Zelle vereinheitlicht.</summary>
public sealed class WunschterminZelle : ObservableObject
{
    private readonly Action geaendert;
    private string text;

    /// <summary>Initialisiert die Zelle.</summary>
    /// <param name="tag">Der Tag.</param>
    /// <param name="text">Kurztext des Termins.</param>
    /// <param name="geaendert">Wird nach einer Änderung aufgerufen.</param>
    public WunschterminZelle(DateOnly tag, string text, Action geaendert)
    {
        Tag = tag;
        this.text = text;
        this.geaendert = geaendert;
    }

    /// <summary>Holt den Tag.</summary>
    public DateOnly Tag { get; }

    /// <summary>Holt oder setzt den Kurztext; ungültige Eingaben werden leer (Original <c>FixCellStringValue</c>).</summary>
    public string Text
    {
        get => text;
        set
        {
            string bereinigt = Heimtagtext.Bereinigen(value);
            if (SetProperty(ref text, bereinigt))
            {
                OnPropertyChanged(nameof(HatTermin));
                geaendert();
            }
            else if (bereinigt != value)
            {
                // Die Eingabe wurde verworfen oder umformatiert: Anzeige zurücksetzen.
                OnPropertyChanged(nameof(Text));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob ein Termin eingetragen ist.</summary>
    public bool HatTermin => text.Length > 0;
}
