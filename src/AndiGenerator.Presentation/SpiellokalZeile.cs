// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Zeile „Beschriftung – Spiellokal“ der Spiellokal-Seite (Heimspieltermin oder Nachbarmannschaft).</summary>
public sealed class SpiellokalZeile : ObservableObject
{
    private readonly Action geaendert;
    private string spiellokal;

    /// <summary>Initialisiert die Zeile.</summary>
    /// <param name="beschriftung">Termin oder Nachbarmannschaft.</param>
    /// <param name="spiellokal">Spiellokal.</param>
    /// <param name="geaendert">Wird nach einer Änderung aufgerufen.</param>
    public SpiellokalZeile(string beschriftung, string spiellokal, Action geaendert)
    {
        Beschriftung = beschriftung;
        this.spiellokal = spiellokal;
        this.geaendert = geaendert;
    }

    /// <summary>Holt die Beschriftung.</summary>
    public string Beschriftung { get; }

    /// <summary>Holt oder setzt das Spiellokal.</summary>
    public string? Spiellokal
    {
        get => spiellokal;
        set
        {
            if (SetProperty(ref spiellokal, value ?? string.Empty))
            {
                geaendert();
            }
        }
    }

    /// <summary>Holt das Spiellokal ohne <c>null</c>.</summary>
    internal string Wert => spiellokal;
}
