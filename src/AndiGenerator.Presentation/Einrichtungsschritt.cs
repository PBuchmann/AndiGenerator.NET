// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Ein Punkt der Einrichtungsseite: entweder eine Entscheidung (gespeicherte Einstellungen, Rundenplanung) mit
/// wählbaren Optionen oder ein Punkt des Erststart-Assistenten mit eingebetteter Seite der Spielplandaten.
/// </summary>
public sealed class Einrichtungsschritt : ObservableObject
{
    private Schrittzustand zustand;
    private bool istAktiv;
    private int auswahl;
    private DatenDialogViewModel? seite;

    /// <summary>Initialisiert eine Entscheidung.</summary>
    /// <param name="nummer">Laufende Nummer ab 1.</param>
    /// <param name="titel">Titel in der Liste.</param>
    /// <param name="text">Erklärung bzw. Frage.</param>
    /// <param name="details">Aufzählung unter dem Text, z. B. die gespeicherten Einstellungen.</param>
    /// <param name="optionen">Die Wahlmöglichkeiten; die erste ist vorgewählt.</param>
    public Einrichtungsschritt(int nummer, string titel, string text, IReadOnlyList<string> details, IReadOnlyList<Einrichtungsoption> optionen)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(optionen);
        Nummer = nummer;
        Titel = titel;
        Text = text;
        Details = Detailzeile.Aus(details);
        Optionen = optionen;
        WaehlenCommand = new RelayCommand(() => Gewaehlt?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Initialisiert einen Punkt des Erststart-Assistenten.</summary>
    /// <param name="nummer">Laufende Nummer ab 1.</param>
    /// <param name="hinweis">Der Hinweis des Originals.</param>
    public Einrichtungsschritt(int nummer, Erststarthinweis hinweis)
        : this(nummer, hinweis.Titel, hinweis.Text, [], [])
    {
        Punkt = hinweis.Punkt;
    }

    /// <summary>Tritt ein, wenn der Schritt in der Liste angeklickt wird.</summary>
    public event EventHandler? Gewaehlt;

    /// <summary>Holt die laufende Nummer.</summary>
    public int Nummer { get; }

    /// <summary>Holt den Titel.</summary>
    public string Titel { get; }

    /// <summary>Holt die Erklärung bzw. Frage.</summary>
    public string Text { get; }

    /// <summary>Holt die Aufzählung unter dem Text.</summary>
    public IReadOnlyList<Detailzeile> Details { get; }

    /// <summary>Holt einen Wert, der angibt, ob es eine Aufzählung gibt.</summary>
    public bool HatDetails => Details.Count > 0;

    /// <summary>Holt die Wahlmöglichkeiten einer Entscheidung (leer bei Datenpunkten).</summary>
    public IReadOnlyList<Einrichtungsoption> Optionen { get; }

    /// <summary>Holt einen Wert, der angibt, ob der Schritt eine Entscheidung ist.</summary>
    public bool IstEntscheidung => Optionen.Count > 0;

    /// <summary>Holt den Punkt des Erststart-Assistenten oder <c>null</c> bei Entscheidungen.</summary>
    public Erststartpunkt? Punkt { get; }

    /// <summary>Holt oder setzt die gewählte Option (Index).</summary>
    public int Auswahl
    {
        get => auswahl;
        set
        {
            if (SetProperty(ref auswahl, value < 0 ? 0 : value))
            {
                IstGewaehlt = true;
            }
        }
    }

    /// <summary>
    /// Holt einen Wert, der angibt, ob der Anwender bei dieser Entscheidung eine andere Wahl angeklickt hat; sie gilt dann
    /// auch beim Abschließen, ohne dass „Auswahl übernehmen“ gedrückt wurde.
    /// </summary>
    public bool IstGewaehlt { get; private set; }

    /// <summary>Holt die eingebettete Seite der Spielplandaten (nur Datenpunkte, solange der Schritt gewählt ist).</summary>
    public DatenDialogViewModel? Seite
    {
        get => seite;
        internal set => SetProperty(ref seite, value);
    }

    /// <summary>Holt den Stand.</summary>
    public Schrittzustand Zustand
    {
        get => zustand;
        internal set
        {
            if (SetProperty(ref zustand, value))
            {
                Anzeigeaendern();
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob der Schritt gerade gezeigt wird.</summary>
    public bool IstAktiv
    {
        get => istAktiv;
        internal set
        {
            if (SetProperty(ref istAktiv, value))
            {
                Anzeigeaendern();
            }
        }
    }

    /// <summary>Holt den Stand als Text, z. B. „jetzt dran“ oder „erledigt“.</summary>
    public string Status => (Zustand, IstAktiv) switch
    {
        (Schrittzustand.Erledigt, _) => "erledigt",
        (Schrittzustand.Uebersprungen, _) => "übersprungen",
        (_, true) => "jetzt dran",
        _ => "offen",
    };

    /// <summary>Holt die Marke im Kreis: Haken, Strich oder die Nummer.</summary>
    public string Marke => Zustand switch
    {
        Schrittzustand.Erledigt => "✓",
        Schrittzustand.Uebersprungen => "–",
        _ => Nummer.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>Holt einen Wert, der angibt, ob der Schritt erledigt ist (grüne Marke).</summary>
    public bool IstErledigt => Zustand == Schrittzustand.Erledigt;

    /// <summary>Holt den Befehl, der den Schritt zeigt.</summary>
    public IRelayCommand WaehlenCommand { get; }

    private void Anzeigeaendern()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Marke));
        OnPropertyChanged(nameof(IstErledigt));
    }
}
