// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Tabellen;

/// <summary>Zelle eines Arbeitsblatts.</summary>
/// <param name="Wert">
/// Inhalt: <see cref="string"/>, <see cref="int"/>, <see cref="double"/>, <see cref="DateTime"/> (Datum),
/// <see cref="TimeSpan"/> (Uhrzeit) oder <c>null</c> für eine leere Zelle.
/// </param>
/// <param name="Format">Zahlenformat; für Datum und Uhrzeit passend setzen.</param>
/// <param name="Fett">Fettschrift.</param>
/// <param name="Hintergrund">Hintergrundfarbe als <c>0xRRGGBB</c> oder <c>null</c>.</param>
/// <param name="Schriftfarbe">Schriftfarbe als <c>0xRRGGBB</c> oder <c>null</c> (schwarz).</param>
public sealed record Blattzelle(object? Wert, Zellformat Format = Zellformat.Standard, bool Fett = false, int? Hintergrund = null, int? Schriftfarbe = null)
{
    /// <summary>Holt eine leere Zelle.</summary>
    public static Blattzelle Leer { get; } = new((object?)null);

    /// <summary>Erzeugt eine Textzelle.</summary>
    /// <param name="text">Der Text.</param>
    /// <param name="fett">Fettschrift.</param>
    /// <returns>Die Zelle.</returns>
    public static Blattzelle Text(string text, bool fett = false) => new(text, Fett: fett);

    /// <summary>Erzeugt eine Datumszelle (leer ohne Datum).</summary>
    /// <param name="datum">Das Datum oder <c>null</c>.</param>
    /// <returns>Die Zelle.</returns>
    public static Blattzelle Datum(DateTime? datum) => new(datum?.Date, Zellformat.Datum);

    /// <summary>Erzeugt eine Uhrzeitzelle (leer ohne Zeitpunkt).</summary>
    /// <param name="zeitpunkt">Der Zeitpunkt oder <c>null</c>.</param>
    /// <returns>Die Zelle.</returns>
    public static Blattzelle Uhrzeit(DateTime? zeitpunkt) => new(zeitpunkt?.TimeOfDay, Zellformat.Uhrzeit);

    /// <summary>Erzeugt eine Zelle mit einer ganzen Zahl.</summary>
    /// <param name="zahl">Die Zahl.</param>
    /// <returns>Die Zelle.</returns>
    public static Blattzelle Zahl(int zahl) => new(zahl, Zellformat.Ganzzahl);
}
