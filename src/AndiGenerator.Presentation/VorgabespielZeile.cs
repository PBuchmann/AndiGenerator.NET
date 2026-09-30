// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Zeile der Seite „Manuell festgelegte Begegnungen“.</summary>
/// <param name="Spiel">Die Begegnung.</param>
public sealed record VorgabespielZeile(Vorgabespiel Spiel)
{
    /// <summary>Holt das Datum mit Wochentag.</summary>
    public string Datum => Datumsanzeige.Tag(DateOnly.FromDateTime(Spiel.Termin));

    /// <summary>Holt die Uhrzeit.</summary>
    public string Uhrzeit => Spiel.Termin.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Holt die Heimmannschaft.</summary>
    public string Heim => Spiel.Heim;

    /// <summary>Holt die Gastmannschaft.</summary>
    public string Gast => Spiel.Gast;

    /// <summary>Holt das Spiellokal.</summary>
    public string Spiellokal => Spiel.Spiellokal;
}
