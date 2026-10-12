// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>
/// Gegner auf der Seite „Wochenende (60-km-Regel)“: Häkchen wie im Original, dazu die Entfernung der Hallen (nicht im
/// Original) und ein Hinweis, wenn die Häkchen nicht zur Entfernung passen.
/// </summary>
public sealed class Gegnerzeile : ObservableObject
{
    private readonly Action geaendert;
    private readonly Hallenentfernung entfernung;
    private bool gewaehlt;

    /// <summary>Initialisiert die Zeile.</summary>
    /// <param name="entfernung">Gegner und Entfernung.</param>
    /// <param name="geaendert">Wird nach einer Änderung des Häkchens aufgerufen.</param>
    public Gegnerzeile(Hallenentfernung entfernung, Action geaendert)
    {
        ArgumentNullException.ThrowIfNull(entfernung);
        this.entfernung = entfernung;
        this.geaendert = geaendert;
    }

    /// <summary>Holt den Namen des Gegners.</summary>
    public string Name => entfernung.Gegner;

    /// <summary>Holt die Entfernung als Text, z. B. „72 km“, oder einen leeren Text ohne Adressen.</summary>
    public string Entfernung => entfernung.Luftlinie is double km
        ? km.ToString("0", CultureInfo.InvariantCulture) + " km"
        : string.Empty;

    /// <summary>Holt den Hinweis zur Zeile oder einen leeren Text.</summary>
    public string Hinweis => (entfernung.Luftlinie, Gewaehlt, entfernung.HeimUnterDerWoche) switch
    {
        (double km, false, true) when km > Datenbearbeitung.Wochenspielgrenze => "weit, Heimspiele auch unter der Woche",
        (double km, true, _) when km <= Datenbearbeitung.Wochenspielgrenze => "nah",
        _ => string.Empty,
    };

    /// <summary>Holt einen Wert, der angibt, ob ein Hinweis angezeigt wird.</summary>
    public bool HatHinweis => Hinweis.Length > 0;

    /// <summary>Holt oder setzt das Häkchen.</summary>
    public bool Gewaehlt
    {
        get => gewaehlt;
        set
        {
            if (SetProperty(ref gewaehlt, value))
            {
                HinweisMelden();
                geaendert();
            }
        }
    }

    /// <summary>Setzt das Häkchen ohne Änderungsmeldung (Laden, Standard).</summary>
    /// <param name="wert">Neuer Zustand.</param>
    internal void Setzen(bool wert)
    {
        if (SetProperty(ref gewaehlt, wert, nameof(Gewaehlt)))
        {
            HinweisMelden();
        }
    }

    private void HinweisMelden()
    {
        OnPropertyChanged(nameof(Hinweis));
        OnPropertyChanged(nameof(HatHinweis));
    }
}
