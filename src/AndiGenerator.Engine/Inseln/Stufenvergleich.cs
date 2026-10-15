// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Inseln;

/// <summary>
/// Die eine Regel, nach der Pläne im Automodus verglichen werden – für den besten Plan, die Teilneuplanung, den Stillstand der
/// Suche und das Zurückziehen beim Glätten: harte Fehler, A, B, C-Verstöße, C-Ausreißer, zuletzt die C-Kosten.
/// </summary>
public static class Stufenvergleich
{
    /// <summary>
    /// Beim Glätten darf B im Plan der Suche um so viel über dem Stand zu Beginn des Kriteriums liegen, bevor die Anhebung
    /// zurückgezogen wird: Ein einzelner B-Verstoß mehr ist oft nur ein Zwischenstand der Suche.
    /// </summary>
    public const int BSpielraumBeimGlaetten = 1;

    /// <summary>Rangschlüssel eines Werts ohne C-Kosten: kleiner ist besser.</summary>
    /// <param name="wert">Der Wert.</param>
    /// <returns>Harte Fehler, A, B, C-Verstöße, zuletzt die Ausreißer.</returns>
    public static (int Harte, int A, int B, int C, int Ausreisser) Schluessel(Stufenwert wert) =>
        (wert.Harte, wert.A, wert.B, wert.CAnzahl, wert.Ausreisser);

    /// <summary>Gibt an, ob <paramref name="neu"/> besser ist als <paramref name="alt"/> (bei gleichen Anzahlen: kleinere C-Kosten).</summary>
    /// <param name="neu">Der neue Wert.</param>
    /// <param name="alt">Der bisherige.</param>
    /// <returns><c>true</c>, wenn er besser ist.</returns>
    public static bool Besser(Stufenwert neu, Stufenwert alt)
    {
        int vergleich = Schluessel(neu).CompareTo(Schluessel(alt));
        return vergleich != 0 ? vergleich < 0 : neu.C < alt.C;
    }

    /// <summary>
    /// Gibt an, ob der Plan der Suche beim Glätten in A oder B schlechter ist als zu Beginn des Kriteriums (dann wird die
    /// Anhebung zurückgezogen): A steigt oder B um mehr als <see cref="BSpielraumBeimGlaetten"/>.
    /// </summary>
    /// <param name="wert">Der Plan der Suche.</param>
    /// <param name="bezug">Der beste Plan zu Beginn des Kriteriums.</param>
    /// <returns><c>true</c>, wenn er schlechter ist.</returns>
    public static bool AOderBSchlechter(Stufenwert wert, Stufenwert bezug) =>
        wert.A > bezug.A || wert.B > bezug.B + BSpielraumBeimGlaetten;
}
