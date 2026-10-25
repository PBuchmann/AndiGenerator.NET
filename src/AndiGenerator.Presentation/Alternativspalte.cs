// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Schranke;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>Eine Spalte im Dialog der Alternativen: der aktuelle Plan oder eine Alternative aus HiGHS.</summary>
public sealed class Alternativspalte : ObservableObject
{
    private bool istGewaehlt;

    /// <summary>Initialisiert die Spalte.</summary>
    /// <param name="titel">Z. B. <c>Aktueller Plan</c> oder <c>Heimkoppel bestmöglich</c>.</param>
    /// <param name="summen">Z. B. <c>A 10 · B 14 · C 255</c>.</param>
    /// <param name="alternative">Die Alternative; <c>null</c> beim aktuellen Plan.</param>
    /// <param name="auswaehlen">Wählt die Spalte aus.</param>
    internal Alternativspalte(string titel, string summen, Planalternative? alternative, Action<Alternativspalte> auswaehlen)
    {
        Titel = titel;
        Summen = summen;
        Alternative = alternative;
        AuswaehlenCommand = new RelayCommand(() => auswaehlen(this));
    }

    /// <summary>Holt die Überschrift.</summary>
    public string Titel { get; }

    /// <summary>Holt die Summen der Stufen, z. B. <c>A 10 · B 14 · C 255</c>.</summary>
    public string Summen { get; }

    /// <summary>Holt die Alternative; <c>null</c> beim aktuellen Plan.</summary>
    public Planalternative? Alternative { get; }

    /// <summary>Holt einen Wert, der angibt, ob die Spalte der aktuelle Plan ist.</summary>
    public bool IstAktuell => Alternative is null;

    /// <summary>Holt den Befehl, der die Spalte auswählt (ihre Mannschaften unten, „Übernehmen“ gilt ihr).</summary>
    public IRelayCommand AuswaehlenCommand { get; }

    /// <summary>Holt einen Wert, der angibt, ob die Spalte ausgewählt ist.</summary>
    public bool IstGewaehlt
    {
        get => istGewaehlt;
        internal set => SetProperty(ref istGewaehlt, value);
    }
}
