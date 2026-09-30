// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Anzeigezeile einer Begegnung im Dialog „Nachbarmannschaft“.</summary>
/// <param name="Zeile">Die Begegnung.</param>
public sealed record NachbarspielZeile(Nachbarspielzeile Zeile)
{
    /// <summary>Holt das Datum.</summary>
    public string Datum => Zeile.Spiel.Termin.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>Holt die Uhrzeit.</summary>
    public string Uhrzeit => Zeile.Spiel.Termin.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Holt die Heimmannschaft.</summary>
    public string Heim => Zeile.Spiel.Heim;

    /// <summary>Holt die Gastmannschaft.</summary>
    public string Gast => Zeile.Spiel.Gast;

    /// <summary>Holt das Spiellokal.</summary>
    public string Spiellokal => Zeile.Spiel.Spiellokal;
}
