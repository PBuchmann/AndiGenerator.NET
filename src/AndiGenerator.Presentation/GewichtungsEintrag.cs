// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Eine Zeile „Name – Gewichtungsstufe“ im Einstellungsdialog.</summary>
public sealed class GewichtungsEintrag : ObservableObject
{
    private readonly Action<Gewichtungsziel, Gewichtung> aendern;
    private int stufe;

    /// <summary>Initialisiert die Zeile.</summary>
    /// <param name="ziel">Die Gewichtung.</param>
    /// <param name="wert">Aktueller Wert.</param>
    /// <param name="aendern">Wird bei einer Änderung durch den Benutzer aufgerufen.</param>
    public GewichtungsEintrag(Gewichtungsziel ziel, Gewichtung wert, Action<Gewichtungsziel, Gewichtung> aendern)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        Ziel = ziel;
        stufe = (int)wert;
        this.aendern = aendern;
    }

    /// <summary>Holt die Namen aller Stufen (für die Auswahlliste).</summary>
    public static IReadOnlyList<string> Stufen { get; } = Gewichtungsnamen.Stufen.Select(Gewichtungsnamen.Name).ToList();

    /// <summary>Holt die Beschriftung.</summary>
    public string Name => Ziel.Beschriftung;

    /// <summary>Holt die Gewichtung, die diese Zeile ändert.</summary>
    public Gewichtungsziel Ziel { get; }

    /// <summary>Holt oder setzt die gewählte Stufe als Index in <see cref="Stufen"/>.</summary>
    public int Stufe
    {
        get => stufe;
        set
        {
            if (value >= 0 && SetProperty(ref stufe, value))
            {
                aendern(Ziel, (Gewichtung)value);
            }
        }
    }
}
