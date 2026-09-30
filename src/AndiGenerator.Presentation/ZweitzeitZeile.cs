// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Zeile „Heimspieltermin – alternative Zeit“ der Auswärtskoppel-Seite.</summary>
public sealed class ZweitzeitZeile : ObservableObject
{
    private readonly Action geaendert;
    private string text;

    /// <summary>Initialisiert die Zeile.</summary>
    /// <param name="zeit">Termin und alternative Zeit.</param>
    /// <param name="geaendert">Wird nach einer Änderung aufgerufen.</param>
    public ZweitzeitZeile(Zweitzeit zeit, Action geaendert)
    {
        ArgumentNullException.ThrowIfNull(zeit);
        Termin = zeit.Termin;
        Vorschlag = zeit.Vorschlag;
        text = Format(zeit.Zeit);
        this.geaendert = geaendert;
    }

    /// <summary>Holt die Beschriftung, z. B. <c>Sa 05.09.2026 18:00</c>.</summary>
    public string Beschriftung => Datumsanzeige.Termin(Termin);

    /// <summary>Holt oder setzt die alternative Zeit als Text (<c>hh:mm</c>, leer = keine).</summary>
    public string Text
    {
        get => text;
        set
        {
            if (SetProperty(ref text, value ?? string.Empty))
            {
                geaendert();
            }
        }
    }

    /// <summary>Holt den Termin mit der eingegebenen Zeit (ungültige Eingaben gelten wie im Original als keine Zeit).</summary>
    public Zweitzeit Wert => new(Termin, Zweitzeit.Lesen(text));

    /// <summary>Holt den Vorschlag für „Alles setzen“.</summary>
    internal TimeOnly Vorschlag { get; }

    private DateTime Termin { get; }

    /// <summary>Formatiert eine Zeit wie das Original (<c>FormatTimeForExport</c>).</summary>
    /// <param name="zeit">Die Zeit.</param>
    /// <returns>Der Text oder leer.</returns>
    internal static string Format(TimeOnly? zeit) => zeit?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
}
