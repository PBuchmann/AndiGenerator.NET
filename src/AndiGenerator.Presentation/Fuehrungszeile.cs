// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>Ein Schritt in der Begleitleiste „Schritt für Schritt“: Nummer bzw. Haken, Titel, beim aktuellen Schritt der Text.</summary>
public sealed class Fuehrungszeile : ObservableObject
{
    private bool istAktuell;
    private bool istErledigt;

    /// <summary>Initialisiert die Zeile.</summary>
    /// <param name="nummer">Nummer ab 1.</param>
    /// <param name="schritt">Der Schritt.</param>
    /// <param name="waehlen">Wählt den Schritt (Klick auf die Zeile).</param>
    internal Fuehrungszeile(int nummer, Fuehrungsschritt schritt, Action<Fuehrungszeile> waehlen)
    {
        Nummer = nummer;
        Schritt = schritt;
        WaehlenCommand = new RelayCommand(() => waehlen(this));
    }

    /// <summary>Holt die Nummer ab 1.</summary>
    public int Nummer { get; }

    /// <summary>Holt den Schritt.</summary>
    public Fuehrungsschritt Schritt { get; }

    /// <summary>Holt den Titel.</summary>
    public string Titel => Schritt.Titel;

    /// <summary>Holt den Text.</summary>
    public string Text => Schritt.Text;

    /// <summary>Holt den Verweis auf die Anleitung, z. B. „Mehr dazu: Anleitung, Abschnitt 5.7“.</summary>
    public string Verweis => "Mehr dazu: Anleitung, Abschnitt " + Schritt.Abschnitt;

    /// <summary>Holt die Marke links: die Nummer oder ✓.</summary>
    public string Marke => IstErledigt ? "✓" : Nummer.ToString(CultureInfo.InvariantCulture);

    /// <summary>Holt den Befehl „Schritt wählen“.</summary>
    public IRelayCommand WaehlenCommand { get; }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob der Schritt gerade dran ist (dann steht sein Text da).</summary>
    public bool IstAktuell
    {
        get => istAktuell;
        set => SetProperty(ref istAktuell, value);
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob der Schritt erledigt ist.</summary>
    public bool IstErledigt
    {
        get => istErledigt;
        set
        {
            if (SetProperty(ref istErledigt, value))
            {
                OnPropertyChanged(nameof(Marke));
            }
        }
    }
}
