// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>
/// Ein Tag der Terminübersicht. Der Termin steht als Kurztext wie im Original (z. B. <c>18:00 A K1:14:00</c>); für die
/// Anzeige als Kärtchen liefert die Zelle seine Art (Heimspiel, Ausweichtermin, Sperrtermin), die Uhrzeit und die Zusätze.
/// </summary>
public sealed class WunschterminZelle : ObservableObject
{
    private readonly Action geaendert;
    private string text;
    private bool istZiel;
    private bool istQuelle;

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
                OnPropertyChanged(nameof(IstHeimspiel));
                OnPropertyChanged(nameof(IstAusweich));
                OnPropertyChanged(nameof(IstSperre));
                OnPropertyChanged(nameof(IstLeer));
                OnPropertyChanged(nameof(Hauptzeile));
                OnPropertyChanged(nameof(Zusatz));
                OnPropertyChanged(nameof(Hinweis));
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

    /// <summary>Holt einen Wert, der angibt, ob kein Termin eingetragen ist (Auswärtsspiele möglich).</summary>
    public bool IstLeer => !HatTermin;

    /// <summary>Holt einen Wert, der angibt, ob ein Heimspiel möglich ist (kein Ausweichtermin).</summary>
    public bool IstHeimspiel => Termin is { Sperrtermin: false, Ausweichtermin: false };

    /// <summary>Holt einen Wert, der angibt, ob es ein Ausweichtermin ist (nur im Notfall einplanen).</summary>
    public bool IstAusweich => Termin is { Sperrtermin: false, Ausweichtermin: true };

    /// <summary>Holt einen Wert, der angibt, ob es ein Sperrtermin ist (gar kein Spiel).</summary>
    public bool IstSperre => Termin is { Sperrtermin: true };

    /// <summary>Holt die erste Zeile des Kärtchens: die Uhrzeit bzw. „gesperrt“.</summary>
    public string Hauptzeile => Termin switch
    {
        null => string.Empty,
        { Sperrtermin: true } => "gesperrt",
        { } h => h.Datum.ToString("HH:mm", CultureInfo.InvariantCulture),
    };

    /// <summary>Holt die zweite Zeile: Ausweich, Koppel, Doppelspieltag, höchstens n Heimspiele, Spiellokal (kurz).</summary>
    public string Zusatz
    {
        get
        {
            if (Termin is not { Sperrtermin: false } h)
            {
                return string.Empty;
            }

            var teile = new List<string>();
            if (h.Ausweichtermin)
            {
                teile.Add("Ausweich");
            }

            if (h.Koppeltermin)
            {
                teile.Add("Koppel " + h.KoppelZweitzeit.ToString("HH:mm", CultureInfo.InvariantCulture));
            }
            else if (h.AuswaertsKoppelZweitzeit)
            {
                teile.Add("2. Zeit " + h.KoppelZweitzeit.ToString("HH:mm", CultureInfo.InvariantCulture));
            }

            if (h.Doppeltermin)
            {
                teile.Add("Doppel");
            }

            if (h.MaxParallel > 0)
            {
                teile.Add("max " + h.MaxParallel.ToString(CultureInfo.InvariantCulture));
            }

            if (h.Spiellokal.Length > 0)
            {
                teile.Add("Lokal " + h.Spiellokal);
            }

            return string.Join(" · ", teile);
        }
    }

    /// <summary>Holt den Tooltip: Tag, Termin ausgeschrieben und was ein Klick bzw. Ziehen tut.</summary>
    public string Hinweis
    {
        get
        {
            string tag = Tag.ToString("dddd, dd.MM.yyyy", Deutsch);
            string termin = Termin switch
            {
                null => "kein Heimspiel (Auswärtsspiele möglich)",
                { Sperrtermin: true } => "Sperrtermin: an diesem Tag kein Spiel",
                _ => (IstAusweich ? "Ausweichtermin " : "Heimspiel möglich ") + Hauptzeile + (Zusatz.Length > 0 ? " (" + Zusatz + ")" : string.Empty),
            };
            string bedienung = HatTermin
                ? "Klick: bearbeiten · Ziehen: auf einen anderen Tag verschieben (mit Strg kopieren)"
                : "Klick: Termin eintragen";
            return $"{tag}: {termin}{Environment.NewLine}{bedienung}";
        }
    }

    /// <summary>Holt oder setzt, ob beim Ziehen ein Termin über diesem Tag schwebt (Ablagestelle hervorheben).</summary>
    public bool IstZiel
    {
        get => istZiel;
        set => SetProperty(ref istZiel, value);
    }

    /// <summary>Holt oder setzt, ob der Termin dieses Tages gerade gezogen wird.</summary>
    public bool IstQuelle
    {
        get => istQuelle;
        set => SetProperty(ref istQuelle, value);
    }

    private static CultureInfo Deutsch { get; } = CultureInfo.GetCultureInfo("de-DE");

    private Heimtag? Termin => Heimtagtext.Lesen(Tag, text);
}
