// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Zeitraum mit Mindestzahl an Spielen (Knoten <c>mandatorygames</c>, Original <c>TPlanMandatoryTime</c>).</summary>
/// <param name="Von">Erster Tag.</param>
/// <param name="Bis">Letzter Tag.</param>
/// <param name="Anzahl">Minimale Anzahl Spiele (1 bis 10).</param>
public sealed record Pflichtspielzeit(DateOnly Von, DateOnly Bis, int Anzahl)
{
    /// <summary>Holt die wählbaren Anzahlen.</summary>
    public static IReadOnlyList<int> Anzahlen { get; } = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    /// <summary>Holt die Beschriftung wie im Original, z. B. <c>Sa 05.09.2026 - So 06.09.2026</c>.</summary>
    public string Bezeichnung => Datumsanzeige.Tag(Von) + " - " + Datumsanzeige.Tag(Bis);

    /// <summary>
    /// Prüft einen Zeitraum wie das Original (<c>ButtonOKClick</c>). Zusätzlich darf es denselben Zeitraum nicht zweimal
    /// geben, sonst ließe sich die Änderungsdatei nicht eindeutig schreiben.
    /// </summary>
    /// <param name="andere">Die übrigen Zeiträume.</param>
    /// <returns>Die Fehlermeldung oder <c>null</c>.</returns>
    public string? Pruefen(IEnumerable<Pflichtspielzeit> andere)
    {
        ArgumentNullException.ThrowIfNull(andere);
        if (Von > Bis)
        {
            return "Das \"bis\" Datum darf nicht kleiner als das \"von\" Datum sein";
        }

        return andere.Any(a => a.Von == Von && a.Bis == Bis) ? "Diesen Zeitraum gibt es schon" : null;
    }
}
