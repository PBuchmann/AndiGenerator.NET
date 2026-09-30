// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace AndiGenerator.Engine.Referenz;

/// <summary>Zahlenformate der Kostenanzeige wie im Original (<c>MyFormatFloat</c>, <c>MyFormatFloatShort</c>, deutsche Schreibweise).</summary>
public static class Kostenanzeige
{
    private static readonly NumberFormatInfo Deutsch = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = [3],
        NegativeSign = "-",
    };

    /// <summary>Original <c>MyFormatFloat</c>: unter 10 mit zwei Nachkommastellen, sonst ganzzahlig mit Tausenderpunkten.</summary>
    /// <param name="wert">Zahl.</param>
    /// <returns>Der Text, z. B. <c>4,00</c> oder <c>35.738</c>.</returns>
    public static string Format(double wert) =>
        wert < 10 && wert != 0.0 ? Runden(wert, 2) : Runden(wert, 0);

    /// <summary>Original <c>MyFormatFloatShort</c>: ab 100 000 in „T“, ab 1 000 000 in „Mio“.</summary>
    /// <param name="wert">Kostenwert.</param>
    /// <returns>Der Text, z. B. <c>104 T</c>, <c>1,8 Mio</c> oder <c>3,33</c>.</returns>
    public static string Kurz(double wert)
    {
        if (wert >= 1000000.0)
        {
            return wert >= 10000000.0 ? Runden(wert / 1000000.0, 0) + " Mio" : Runden(wert / 1000000.0, 1) + " Mio";
        }

        if (wert >= 100000.0)
        {
            return Runden(wert / 1000.0, 0) + " T";
        }

        return Format(wert);
    }

    /// <summary>Delphi <c>Format('%1.Nn')</c>: kaufmännisch gerundet, Tausenderpunkte.</summary>
    private static string Runden(double wert, int stellen) =>
        Math.Round(wert, stellen, MidpointRounding.AwayFromZero).ToString("N" + stellen.ToString(CultureInfo.InvariantCulture), Deutsch);
}
