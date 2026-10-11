// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Zeile der Seite „Heimrecht“: Gegner mit Auswahl Vorrunde, egal oder Rückrunde.</summary>
public sealed class HeimrechtZeile : ObservableObject
{
    private static readonly int[] Werte = [Heimrecht.Vorrunde, Heimrecht.Egal, Heimrecht.Rueckrunde];
    private readonly Action geaendert;
    private int index;

    /// <summary>Initialisiert die Zeile.</summary>
    /// <param name="gegner">Name der gegnerischen Mannschaft.</param>
    /// <param name="wert">Heimrecht (0 = egal, 1 = Vorrunde, 2 = Rückrunde).</param>
    /// <param name="geaendert">Wird nach einer Änderung aufgerufen.</param>
    public HeimrechtZeile(string gegner, int wert, Action geaendert)
    {
        Gegner = gegner;
        index = IndexVon(wert);
        this.geaendert = geaendert;
    }

    /// <summary>Holt die Auswahl wie im Original.</summary>
    public static IReadOnlyList<string> Auswahl { get; } = ["Vorrunde", "egal", "Rückrunde"];

    /// <summary>Holt den Namen der gegnerischen Mannschaft.</summary>
    public string Gegner { get; }

    /// <summary>Holt oder setzt die gewählte Position in <see cref="Auswahl"/>.</summary>
    public int Index
    {
        get => index;
        set
        {
            if (value >= 0 && SetProperty(ref index, value))
            {
                WahlGeaendert();
                geaendert();
            }
        }
    }

    /// <summary>Holt oder setzt, ob die gegnerische Mannschaft in der Vorrunde Heimrecht hat (Umschalter).</summary>
    public bool IstVorrunde
    {
        get => index == 0;
        set => Waehlen(value, 0);
    }

    /// <summary>Holt oder setzt, ob das Heimrecht egal ist (Umschalter).</summary>
    public bool IstEgal
    {
        get => index == 1;
        set => Waehlen(value, 1);
    }

    /// <summary>Holt oder setzt, ob die gegnerische Mannschaft in der Rückrunde Heimrecht hat (Umschalter).</summary>
    public bool IstRueckrunde
    {
        get => index == 2;
        set => Waehlen(value, 2);
    }

    /// <summary>Holt das Heimrecht (0 = egal, 1 = Vorrunde, 2 = Rückrunde).</summary>
    public int Wert => Werte[index];

    /// <summary>Setzt das Heimrecht ohne Änderungsmeldung (Standard).</summary>
    /// <param name="wert">Neues Heimrecht.</param>
    internal void Setzen(int wert)
    {
        if (SetProperty(ref index, IndexVon(wert), nameof(Index)))
        {
            WahlGeaendert();
        }
    }

    private static int IndexVon(int wert) => Math.Max(0, Array.IndexOf(Werte, wert));

    private void Waehlen(bool gewaehlt, int wahl)
    {
        if (gewaehlt)
        {
            Index = wahl;
        }
    }

    private void WahlGeaendert()
    {
        OnPropertyChanged(nameof(IstVorrunde));
        OnPropertyChanged(nameof(IstEgal));
        OnPropertyChanged(nameof(IstRueckrunde));
    }
}
