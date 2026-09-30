// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Dialog „Pflichtspieltag“ (Original <c>TFormEditMandatoryDatesDialog</c>).</summary>
public sealed class PflichtspielzeitdialogViewModel : ObservableObject
{
    private readonly Func<Pflichtspielzeit, string?> pruefen;
    private DateTime? von;
    private DateTime? bis;
    private int anzahl;
    private string meldung = string.Empty;

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="vorgabe">Anfangswerte.</param>
    /// <param name="pruefen">Prüfung beim OK; liefert die Fehlermeldung oder <c>null</c>.</param>
    public PflichtspielzeitdialogViewModel(Pflichtspielzeit vorgabe, Func<Pflichtspielzeit, string?> pruefen)
    {
        ArgumentNullException.ThrowIfNull(vorgabe);
        ArgumentNullException.ThrowIfNull(pruefen);
        this.pruefen = pruefen;
        von = vorgabe.Von.ToDateTime(TimeOnly.MinValue);
        bis = vorgabe.Bis.ToDateTime(TimeOnly.MinValue);

        // Im Original stürzte der Dialog bei einer Anzahl außerhalb von 1 bis 10 ab (Befund #27).
        anzahl = Math.Clamp(vorgabe.Anzahl, 1, 10);
    }

    /// <summary>Holt die wählbaren Anzahlen.</summary>
    public static IReadOnlyList<int> Anzahlen => Pflichtspielzeit.Anzahlen;

    /// <summary>Holt oder setzt den ersten Tag.</summary>
    public DateTime? Von
    {
        get => von;
        set => SetProperty(ref von, value);
    }

    /// <summary>Holt oder setzt den letzten Tag.</summary>
    public DateTime? Bis
    {
        get => bis;
        set => SetProperty(ref bis, value);
    }

    /// <summary>Holt oder setzt die minimale Anzahl Spiele.</summary>
    public int Anzahl
    {
        get => anzahl;
        set => SetProperty(ref anzahl, value);
    }

    /// <summary>Holt die Meldung der letzten Prüfung.</summary>
    public string Meldung
    {
        get => meldung;
        private set => SetProperty(ref meldung, value);
    }

    /// <summary>Holt den eingegebenen Zeitraum.</summary>
    public Pflichtspielzeit Ergebnis =>
        new(DateOnly.FromDateTime(Von ?? DateTime.Today), DateOnly.FromDateTime(Bis ?? DateTime.Today), Math.Clamp(Anzahl, 1, 10));

    /// <summary>Prüft die Eingaben (OK im Original).</summary>
    /// <returns><c>true</c>, wenn der Dialog geschlossen werden darf.</returns>
    public bool Bestaetigen()
    {
        Meldung = pruefen(Ergebnis) ?? string.Empty;
        return Meldung.Length == 0;
    }
}
