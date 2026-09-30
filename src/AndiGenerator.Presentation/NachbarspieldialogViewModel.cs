// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Dialog „Begegnung“ einer Nachbarmannschaft (Original <c>TDialogEditOneSisterGame</c>).</summary>
public sealed class NachbarspieldialogViewModel : ObservableObject
{
    private readonly string nachbar;
    private readonly Func<Nachbarspieldaten, string, string?> pruefen;
    private DateTime? datum;
    private TimeSpan? uhrzeit;
    private int art;
    private string gegner = string.Empty;
    private string? spiellokal;
    private string meldung = string.Empty;

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="nachbar">Name der Nachbarmannschaft.</param>
    /// <param name="vorgabe">Bisherige Begegnung oder <c>null</c> für eine neue.</param>
    /// <param name="saisonbeginn">Vorbelegung des Datums bei einer neuen Begegnung.</param>
    /// <param name="pruefen">Prüfung beim OK (Spiel, Gegner); liefert die Fehlermeldung oder <c>null</c>.</param>
    public NachbarspieldialogViewModel(string nachbar, Nachbarspieldaten? vorgabe, DateOnly? saisonbeginn, Func<Nachbarspieldaten, string, string?> pruefen)
    {
        ArgumentNullException.ThrowIfNull(pruefen);
        this.nachbar = nachbar;
        this.pruefen = pruefen;
        datum = saisonbeginn?.ToDateTime(TimeOnly.MinValue);
        if (vorgabe is not null)
        {
            datum = vorgabe.Termin.Date;
            uhrzeit = vorgabe.Termin.TimeOfDay;
            art = vorgabe.Heim == nachbar ? 0 : 1;
            gegner = art == 0 ? vorgabe.Gast : vorgabe.Heim;
            spiellokal = Mannschaftsdaten.Spiellokale.Contains(vorgabe.Spiellokal) ? vorgabe.Spiellokal : null;
        }
    }

    /// <summary>Holt die Arten.</summary>
    public static IReadOnlyList<string> Arten { get; } = ["Heimspiel", "Auswärtsspiel"];

    /// <summary>Holt die Spiellokale.</summary>
    public static IReadOnlyList<string> Spiellokale => Mannschaftsdaten.Spiellokale;

    /// <summary>Holt oder setzt das Datum.</summary>
    public DateTime? Datum
    {
        get => datum;
        set => SetProperty(ref datum, value);
    }

    /// <summary>Holt oder setzt die Uhrzeit.</summary>
    public TimeSpan? Uhrzeit
    {
        get => uhrzeit;
        set => SetProperty(ref uhrzeit, value);
    }

    /// <summary>Holt oder setzt die Art (0 = Heimspiel, 1 = Auswärtsspiel der Nachbarmannschaft).</summary>
    public int Art
    {
        get => art;
        set => SetProperty(ref art, value);
    }

    /// <summary>Holt oder setzt den Gegner.</summary>
    public string Gegner
    {
        get => gegner;
        set => SetProperty(ref gegner, value ?? string.Empty);
    }

    /// <summary>Holt oder setzt das Spiellokal.</summary>
    public string? Spiellokal
    {
        get => spiellokal;
        set => SetProperty(ref spiellokal, value);
    }

    /// <summary>Holt die Meldung der letzten Prüfung.</summary>
    public string Meldung
    {
        get => meldung;
        private set => SetProperty(ref meldung, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob die Nachbarmannschaft Gastgeber ist.</summary>
    public bool Heimspiel => Art == 0;

    /// <summary>Holt die eingegebene Begegnung (Original <c>GetValue</c>).</summary>
    public Nachbarspieldaten Ergebnis
    {
        get
        {
            DateTime termin = (Datum ?? DateTime.Today).Date + new TimeSpan((Uhrzeit ?? TimeSpan.Zero).Hours, (Uhrzeit ?? TimeSpan.Zero).Minutes, 0);
            return Heimspiel
                ? new Nachbarspieldaten(termin, nachbar, Gegner, Spiellokal ?? string.Empty)
                : new Nachbarspieldaten(termin, Gegner, nachbar, Spiellokal ?? string.Empty);
        }
    }

    /// <summary>Prüft die Eingaben (OK im Original).</summary>
    /// <returns><c>true</c>, wenn der Dialog geschlossen werden darf.</returns>
    public bool Bestaetigen()
    {
        Meldung = Datum is null ? "Bitte geben Sie ein Datum ein" : pruefen(Ergebnis, Gegner) ?? string.Empty;
        return Meldung.Length == 0;
    }
}
