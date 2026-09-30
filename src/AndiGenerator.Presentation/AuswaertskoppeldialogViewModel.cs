// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Dialog „Auswärtskoppelwunsch“ (Original <c>TDialogEditOneAuswaertsKoppel</c>).</summary>
public sealed class AuswaertskoppeldialogViewModel : ObservableObject
{
    private readonly Func<Auswaertskoppeldaten, string?> pruefen;
    private string? mannschaftA;
    private string? mannschaftB;
    private int art;
    private string meldung = string.Empty;

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="partner">Wählbare Mannschaften.</param>
    /// <param name="vorgabe">Anfangswerte oder <c>null</c> für einen neuen Wunsch.</param>
    /// <param name="pruefen">Prüfung beim OK; liefert die Fehlermeldung oder <c>null</c>.</param>
    public AuswaertskoppeldialogViewModel(IReadOnlyList<string> partner, Auswaertskoppeldaten? vorgabe, Func<Auswaertskoppeldaten, string?> pruefen)
    {
        ArgumentNullException.ThrowIfNull(partner);
        ArgumentNullException.ThrowIfNull(pruefen);
        Partner = partner;
        this.pruefen = pruefen;
        if (vorgabe is not null)
        {
            mannschaftA = partner.Contains(vorgabe.MannschaftA) ? vorgabe.MannschaftA : null;
            mannschaftB = partner.Contains(vorgabe.MannschaftB) ? vorgabe.MannschaftB : null;
            art = vorgabe.Art;
        }
    }

    /// <summary>Holt die Arten.</summary>
    public static IReadOnlyList<string> Arten => Auswaertskoppeldaten.Arten;

    /// <summary>Holt die wählbaren Mannschaften.</summary>
    public IReadOnlyList<string> Partner { get; }

    /// <summary>Holt oder setzt Mannschaft 1.</summary>
    public string? MannschaftA
    {
        get => mannschaftA;
        set => SetProperty(ref mannschaftA, value);
    }

    /// <summary>Holt oder setzt Mannschaft 2.</summary>
    public string? MannschaftB
    {
        get => mannschaftB;
        set => SetProperty(ref mannschaftB, value);
    }

    /// <summary>Holt oder setzt die Art als Index in <see cref="Arten"/>.</summary>
    public int Art
    {
        get => art;
        set => SetProperty(ref art, value);
    }

    /// <summary>Holt die Meldung der letzten Prüfung.</summary>
    public string Meldung
    {
        get => meldung;
        private set => SetProperty(ref meldung, value);
    }

    /// <summary>Holt die eingegebenen Werte.</summary>
    public Auswaertskoppeldaten Ergebnis => new(mannschaftA ?? string.Empty, mannschaftB ?? string.Empty, Math.Max(0, art));

    /// <summary>Prüft die Eingaben (OK im Original).</summary>
    /// <returns><c>true</c>, wenn der Dialog geschlossen werden darf.</returns>
    public bool Bestaetigen()
    {
        Meldung = pruefen(Ergebnis) ?? string.Empty;
        return Meldung.Length == 0;
    }
}
