// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Dialog „Begegnung“ für eine manuell festgelegte Begegnung (Original <c>TDialogEditOneGame</c>).</summary>
public sealed class VorgabespieldialogViewModel : ObservableObject
{
    private readonly Func<Vorgabespiel, string?> pruefen;
    private readonly int index;
    private DateTime? datum;
    private TimeSpan? uhrzeit;
    private string? heim;
    private string? gast;
    private string? spiellokal;
    private string meldung = string.Empty;

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="mannschaften">Die wählbaren Mannschaften.</param>
    /// <param name="vorgabe">Bisherige Begegnung oder <c>null</c> für eine neue.</param>
    /// <param name="saisonbeginn">Vorbelegung des Datums bei einer neuen Begegnung.</param>
    /// <param name="pruefen">Prüfung beim OK; liefert die Fehlermeldung oder <c>null</c>.</param>
    public VorgabespieldialogViewModel(IReadOnlyList<string> mannschaften, Vorgabespiel? vorgabe, DateOnly saisonbeginn, Func<Vorgabespiel, string?> pruefen)
    {
        ArgumentNullException.ThrowIfNull(mannschaften);
        ArgumentNullException.ThrowIfNull(pruefen);
        Mannschaften = mannschaften;
        this.pruefen = pruefen;
        datum = saisonbeginn.ToDateTime(TimeOnly.MinValue);
        if (vorgabe is not null)
        {
            index = vorgabe.Index;
            datum = vorgabe.Termin.Date;
            uhrzeit = vorgabe.Termin.TimeOfDay;
            heim = mannschaften.Contains(vorgabe.Heim) ? vorgabe.Heim : null;
            gast = mannschaften.Contains(vorgabe.Gast) ? vorgabe.Gast : null;
            spiellokal = Mannschaftsdaten.Spiellokale.Contains(vorgabe.Spiellokal) ? vorgabe.Spiellokal : null;
        }
    }

    /// <summary>Holt die Spiellokale.</summary>
    public static IReadOnlyList<string> Spiellokale => Mannschaftsdaten.Spiellokale;

    /// <summary>Holt die wählbaren Mannschaften.</summary>
    public IReadOnlyList<string> Mannschaften { get; }

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

    /// <summary>Holt oder setzt die Heimmannschaft.</summary>
    public string? Heim
    {
        get => heim;
        set => SetProperty(ref heim, value);
    }

    /// <summary>Holt oder setzt die Gastmannschaft.</summary>
    public string? Gast
    {
        get => gast;
        set => SetProperty(ref gast, value);
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

    /// <summary>Holt die eingegebene Begegnung (Original <c>GetValue</c>).</summary>
    public Vorgabespiel Ergebnis
    {
        get
        {
            TimeSpan zeit = Uhrzeit ?? TimeSpan.Zero;
            DateTime termin = (Datum ?? DateTime.Today).Date + new TimeSpan(zeit.Hours, zeit.Minutes, 0);
            return new Vorgabespiel(index, termin, Heim ?? string.Empty, Gast ?? string.Empty, Spiellokal ?? string.Empty);
        }
    }

    /// <summary>Prüft die Eingaben (OK im Original).</summary>
    /// <returns><c>true</c>, wenn der Dialog geschlossen werden darf.</returns>
    public bool Bestaetigen()
    {
        Meldung = Datum is null ? "Bitte geben Sie ein Datum ein" : pruefen(Ergebnis) ?? string.Empty;
        return Meldung.Length == 0;
    }
}
