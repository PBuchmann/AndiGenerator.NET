// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>
/// Dialog „Mannschaft“ (Original <c>TFormEditTeamName</c>): Name, IDs, Nummer und Spiellokal. OK schließt erst, wenn die
/// Eingaben gültig und eindeutig sind; sonst erscheint die Meldung des Originals im Dialog.
/// </summary>
public sealed class MannschaftsdialogViewModel : ObservableObject
{
    private readonly Func<Mannschaftsdaten, string?> pruefen;
    private string name;
    private string id;
    private string vereinsId;
    private decimal? nummer;
    private string spiellokal;
    private string meldung = string.Empty;

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="titel">Fenstertitel.</param>
    /// <param name="vorgabe">Anfangswerte.</param>
    /// <param name="pruefen">Prüfung beim OK; liefert die Fehlermeldung oder <c>null</c>.</param>
    public MannschaftsdialogViewModel(string titel, Mannschaftsdaten vorgabe, Func<Mannschaftsdaten, string?> pruefen)
    {
        ArgumentNullException.ThrowIfNull(vorgabe);
        ArgumentNullException.ThrowIfNull(pruefen);
        Titel = titel;
        this.pruefen = pruefen;
        name = vorgabe.Name;
        id = vorgabe.Id;
        vereinsId = vorgabe.VereinsId;
        nummer = vorgabe.Nummer;
        spiellokal = vorgabe.Spiellokal;
    }

    /// <summary>Holt die wählbaren Spiellokale.</summary>
    public static IReadOnlyList<string> Spiellokale => Mannschaftsdaten.Spiellokale;

    /// <summary>Holt den Fenstertitel.</summary>
    public string Titel { get; }

    /// <summary>Holt oder setzt den Mannschaftsnamen.</summary>
    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    /// <summary>Holt oder setzt die Mannschafts-ID.</summary>
    public string Id
    {
        get => id;
        set => SetProperty(ref id, value);
    }

    /// <summary>Holt oder setzt die Vereins-ID.</summary>
    public string VereinsId
    {
        get => vereinsId;
        set => SetProperty(ref vereinsId, value);
    }

    /// <summary>Holt oder setzt die Mannschaftsnummer (1 bis 100 wie im Original).</summary>
    public decimal? Nummer
    {
        get => nummer;
        set => SetProperty(ref nummer, value);
    }

    /// <summary>Holt oder setzt das Spiellokal.</summary>
    public string? Spiellokal
    {
        get => spiellokal;
        set => SetProperty(ref spiellokal, value ?? string.Empty);
    }

    /// <summary>Holt die Meldung der letzten Prüfung.</summary>
    public string Meldung
    {
        get => meldung;
        private set => SetProperty(ref meldung, value);
    }

    /// <summary>Holt die eingegebenen Daten.</summary>
    public Mannschaftsdaten Ergebnis => new(Name, Id, VereinsId, (int)(Nummer ?? 1), spiellokal);

    /// <summary>Prüft die Eingaben (OK im Original).</summary>
    /// <returns><c>true</c>, wenn der Dialog geschlossen werden darf.</returns>
    public bool Bestaetigen()
    {
        Meldung = pruefen(Ergebnis) ?? string.Empty;
        return Meldung.Length == 0;
    }
}
