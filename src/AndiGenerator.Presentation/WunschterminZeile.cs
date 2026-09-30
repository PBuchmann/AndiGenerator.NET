// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Zeile der Terminübersicht: Woche mit sieben Tagen.</summary>
public sealed class WunschterminZeile
{
    /// <summary>Initialisiert die Zeile.</summary>
    /// <param name="woche">Montag und Texte.</param>
    /// <param name="geaendert">Wird nach einer Änderung aufgerufen.</param>
    public WunschterminZeile(Wunschterminwoche woche, Action geaendert)
    {
        ArgumentNullException.ThrowIfNull(woche);
        Montag = woche.Montag;
        Tage = woche.Texte.Select((t, i) => new WunschterminZelle(woche.Montag.AddDays(i), t, geaendert)).ToList();
    }

    /// <summary>Holt die Beschriftung wie im Original, z. B. <c>31.08 - 06.09.2026</c>.</summary>
    public string Beschriftung =>
        Montag.ToString("dd.MM", CultureInfo.InvariantCulture) + " - " + Montag.AddDays(6).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>Holt die sieben Tage (Montag bis Sonntag).</summary>
    public IReadOnlyList<WunschterminZelle> Tage { get; }

    private DateOnly Montag { get; }

    /// <summary>Liefert den Stand als Daten.</summary>
    /// <returns>Montag und die sieben Texte.</returns>
    public Wunschterminwoche Wert() => new(Montag, Tage.Select(t => t.Text).ToList());
}
