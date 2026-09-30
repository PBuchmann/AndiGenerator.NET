// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eine Zeile der Aufzählung eines Einrichtungsschritts.</summary>
/// <param name="Text">Der Text ohne Einrückung.</param>
/// <param name="Art">Überschrift, Punkt oder Unterpunkt.</param>
public sealed record Detailzeile(string Text, Detailart Art)
{
    /// <summary>Holt einen Wert, der angibt, ob die Zeile eine Überschrift ist.</summary>
    public bool IstUeberschrift => Art == Detailart.Ueberschrift;

    /// <summary>Holt einen Wert, der angibt, ob die Zeile ein Aufzählungspunkt ist.</summary>
    public bool IstPunkt => Art != Detailart.Ueberschrift;

    /// <summary>Holt einen Wert, der angibt, ob die Zeile ein eingerückter Unterpunkt ist.</summary>
    public bool IstUnterpunkt => Art == Detailart.Unterpunkt;

    /// <summary>
    /// Wandelt die Zeilen einer Meldung im Stil des Originals (Leerzeilen trennen Gruppen, zwei Leerzeichen rücken ein)
    /// in eine Aufzählung um: Leerzeilen entfallen, eine Zeile vor eingerückten Zeilen wird zur Überschrift.
    /// </summary>
    /// <param name="zeilen">Die Zeilen der Meldung.</param>
    /// <returns>Die Aufzählung.</returns>
    public static IReadOnlyList<Detailzeile> Aus(IReadOnlyList<string> zeilen)
    {
        ArgumentNullException.ThrowIfNull(zeilen);
        var ergebnis = new List<Detailzeile>();
        for (int i = 0; i < zeilen.Count; i++)
        {
            string zeile = zeilen[i];
            if (string.IsNullOrWhiteSpace(zeile))
            {
                continue;
            }

            Detailart art;
            if (IstEingerueckt(zeile))
            {
                art = Detailart.Unterpunkt;
            }
            else if (i + 1 < zeilen.Count && IstEingerueckt(zeilen[i + 1]))
            {
                art = Detailart.Ueberschrift;
            }
            else
            {
                art = Detailart.Punkt;
            }

            ergebnis.Add(new Detailzeile(zeile.Trim(), art));
        }

        return ergebnis;
    }

    private static bool IstEingerueckt(string zeile) => zeile.Length > 0 && char.IsWhiteSpace(zeile[0]) && !string.IsNullOrWhiteSpace(zeile);
}
