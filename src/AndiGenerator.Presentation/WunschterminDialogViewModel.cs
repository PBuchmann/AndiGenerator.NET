// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>
/// Dialog „Wunschtermin“ (Original <c>TDialogEditOneHomeDay</c>): kein Heimspiel, Heimspiel mit Uhrzeit, maximalen
/// Heimspielen, Spiellokal, Ausweichtermin, Koppel/Doppelspieltag und alternativer Startzeit für Auswärtskoppeln, oder
/// Sperrtermin.
/// </summary>
public sealed class WunschterminDialogViewModel : ObservableObject
{
    private readonly DateOnly tag;
    private readonly Heimtag vorlage;
    private int art;
    private TimeSpan? uhrzeit;
    private int maxSpiele;
    private bool ausweich;
    private string? spiellokal;
    private int koppel;
    private int prio;
    private TimeSpan? koppelzeit;
    private bool auswaertskoppel;
    private TimeSpan? auswaertszeit;
    private string meldung = string.Empty;

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="tag">Der Tag.</param>
    /// <param name="text">Bisheriger Kurztext des Tages.</param>
    public WunschterminDialogViewModel(DateOnly tag, string text)
    {
        this.tag = tag;
        Heimtag? h = Heimtagtext.Lesen(tag, text);
        vorlage = h ?? new Heimtag(tag.ToDateTime(TimeOnly.MinValue), false, false, false, 0, false, TimeOnly.MinValue, 0, false, string.Empty);
        if (h is null)
        {
            art = 0;
        }
        else if (h.Sperrtermin)
        {
            art = 2;
        }
        else
        {
            art = 1;
            uhrzeit = TimeOnly.FromDateTime(h.Datum).ToTimeSpan();
            maxSpiele = Math.Clamp(h.MaxParallel, 0, 10);
            ausweich = h.Ausweichtermin;
            spiellokal = Mannschaftsdaten.Spiellokale.Contains(h.Spiellokal) ? h.Spiellokal : null;
            prio = h.KoppelPrio;
            if (h.Koppeltermin)
            {
                koppel = 1;
                koppelzeit = h.KoppelZweitzeit.ToTimeSpan();
            }
            else if (h.AuswaertsKoppelZweitzeit)
            {
                auswaertskoppel = true;
                auswaertszeit = h.KoppelZweitzeit.ToTimeSpan();
            }

            if (h.Doppeltermin)
            {
                koppel = 2;
            }
        }
    }

    /// <summary>Holt die Arten des Tages.</summary>
    public static IReadOnlyList<string> Arten { get; } = ["kein Heimspiel, Auswärtsspiele möglich", "Heimspiel möglich", "Sperrtermin"];

    /// <summary>Holt die Auswahl für maximale Heimspiele.</summary>
    public static IReadOnlyList<string> MaxSpieleAuswahl { get; } = ["keine Beschränkung", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10"];

    /// <summary>Holt die Koppelarten.</summary>
    public static IReadOnlyList<string> Koppelarten { get; } = ["kein", "Koppelspieltag (zwei Spiele an einem Tag)", "Doppelspieltag (zwei Spiele am Wochenende)"];

    /// <summary>Holt die Prioritäten.</summary>
    public static IReadOnlyList<string> Prioritaeten { get; } = ["möglich", "normal", "hoch"];

    /// <summary>Holt die Spiellokale.</summary>
    public static IReadOnlyList<string> Spiellokale => Mannschaftsdaten.Spiellokale;

    /// <summary>Holt die Beschriftung, z. B. <c>Datum: 05.09.2026</c>.</summary>
    public string Datum => "Datum: " + tag.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>Holt oder setzt die Art als Index in <see cref="Arten"/>.</summary>
    public int Art
    {
        get => art;
        set
        {
            if (SetProperty(ref art, value))
            {
                OnPropertyChanged(nameof(IstHeimspiel));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob die Heimspielangaben sichtbar sind.</summary>
    public bool IstHeimspiel => Art == 1;

    /// <summary>Holt oder setzt die Uhrzeit.</summary>
    public TimeSpan? Uhrzeit
    {
        get => uhrzeit;
        set => SetProperty(ref uhrzeit, value);
    }

    /// <summary>Holt oder setzt die maximalen Heimspiele (0 = keine Beschränkung).</summary>
    public int MaxSpiele
    {
        get => maxSpiele;
        set => SetProperty(ref maxSpiele, value);
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob es ein Ausweichtermin ist (nur im Notfall einplanen).</summary>
    public bool Ausweich
    {
        get => ausweich;
        set => SetProperty(ref ausweich, value);
    }

    /// <summary>Holt oder setzt das Spiellokal (leer: Standardspiellokal der Mannschaft).</summary>
    public string? Spiellokal
    {
        get => spiellokal;
        set => SetProperty(ref spiellokal, value);
    }

    /// <summary>Holt oder setzt die Koppelart als Index in <see cref="Koppelarten"/>.</summary>
    public int Koppel
    {
        get => koppel;
        set
        {
            if (SetProperty(ref koppel, value))
            {
                if (value == 1)
                {
                    // Wie im Original: Koppelspieltag schließt die Auswärtskoppel-Zeit aus.
                    Auswaertskoppel = false;
                }

                OnPropertyChanged(nameof(PrioAktiv));
                OnPropertyChanged(nameof(KoppelzeitAktiv));
                OnPropertyChanged(nameof(AuswaertskoppelMoeglich));
            }
        }
    }

    /// <summary>Holt oder setzt die Priorität als Index in <see cref="Prioritaeten"/>.</summary>
    public int Prio
    {
        get => prio;
        set => SetProperty(ref prio, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob die Priorität wählbar ist.</summary>
    public bool PrioAktiv => Koppel != 0;

    /// <summary>Holt oder setzt die Startzeit des zweiten Spiels.</summary>
    public TimeSpan? Koppelzeit
    {
        get => koppelzeit;
        set => SetProperty(ref koppelzeit, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob die zweite Startzeit eingegeben werden kann.</summary>
    public bool KoppelzeitAktiv => Koppel == 1;

    /// <summary>Holt einen Wert, der angibt, ob die Auswärtskoppel-Option wählbar ist.</summary>
    public bool AuswaertskoppelMoeglich => Koppel != 1;

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Startzeit für Auswärtskoppeln geändert werden darf.</summary>
    public bool Auswaertskoppel
    {
        get => auswaertskoppel;
        set => SetProperty(ref auswaertskoppel, value);
    }

    /// <summary>Holt oder setzt die alternative Startzeit.</summary>
    public TimeSpan? Auswaertszeit
    {
        get => auswaertszeit;
        set => SetProperty(ref auswaertszeit, value);
    }

    /// <summary>Holt die Meldung der letzten Prüfung.</summary>
    public string Meldung
    {
        get => meldung;
        private set => SetProperty(ref meldung, value);
    }

    /// <summary>Holt den eingegebenen Termin als Kurztext.</summary>
    public string Ergebnis { get; private set; } = string.Empty;

    /// <summary>Übernimmt und prüft die Eingaben (Original <c>ButtonOKClick</c> mit <c>CheckValueData</c>).</summary>
    /// <returns><c>true</c>, wenn der Dialog geschlossen werden darf.</returns>
    public bool Bestaetigen()
    {
        if (Art == 0)
        {
            Ergebnis = string.Empty;
            return true;
        }

        if (Art == 2)
        {
            Ergebnis = "FREI";
            return true;
        }

        TimeOnly zeit = Zeit(Uhrzeit);
        TimeOnly zweite = Koppel == 1 ? Zeit(Koppelzeit) : vorlage.KoppelZweitzeit;
        bool auswaerts = Koppel != 1 && Auswaertskoppel;
        if (auswaerts)
        {
            zweite = Zeit(Auswaertszeit);
        }

        Heimtag h = vorlage with
        {
            Datum = tag.ToDateTime(zeit),
            Sperrtermin = false,
            MaxParallel = MaxSpiele,
            Ausweichtermin = Ausweich,
            Spiellokal = Spiellokal ?? string.Empty,
            Koppeltermin = Koppel == 1,
            Doppeltermin = Koppel == 2,
            AuswaertsKoppelZweitzeit = auswaerts,
            KoppelPrio = Koppel != 0 ? Prio : vorlage.KoppelPrio,
            KoppelZweitzeit = zweite,
        };
        Meldung = Pruefen(h) ?? string.Empty;
        Ergebnis = Heimtagtext.Text(h);
        return Meldung.Length == 0;
    }

    /// <summary>Original <c>CheckValueData</c>.</summary>
    private static string? Pruefen(Heimtag h)
    {
        TimeOnly zeit = TimeOnly.FromDateTime(h.Datum);
        if (zeit == TimeOnly.MinValue)
        {
            return "Bitte geben Sie eine Zeit ein";
        }

        if (h.Koppeltermin || h.AuswaertsKoppelZweitzeit)
        {
            if (h.KoppelZweitzeit == TimeOnly.MinValue)
            {
                return "Bitte geben Sie eine zweite Zeit ein";
            }

            if ((zeit.ToTimeSpan() - h.KoppelZweitzeit.ToTimeSpan()).Duration() <= new TimeSpan(3, 59, 0))
            {
                return "Zwischen dem ersten und zweiten Spiel müssen mindestens 4 Stunden liegen";
            }
        }

        return null;
    }

    private static TimeOnly Zeit(TimeSpan? wert) => wert is TimeSpan t ? new TimeOnly(t.Hours, t.Minutes) : TimeOnly.MinValue;
}
