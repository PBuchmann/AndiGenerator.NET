// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Application;

/// <summary>
/// Eine einzelne Gewichtung in den Berechnungsoptionen, wie sie im Original per Klick in der Kostenansicht geändert wird
/// (Plan-Kostenart, Kostenart für alle Mannschaften, eine Mannschaft insgesamt, eine Mannschaft je Kostenart).
/// </summary>
public abstract record Gewichtungsziel
{
    /// <summary>Holt die Beschriftung im Gewichtungsdialog, z. B. den Namen der Kostenart.</summary>
    public abstract string Beschriftung { get; }

    /// <summary>Liest die Gewichtung.</summary>
    /// <param name="optionen">Die Optionen.</param>
    /// <returns>Die aktuelle Gewichtung.</returns>
    public abstract Gewichtung Lesen(Berechnungsoptionen optionen);

    /// <summary>Setzt die Gewichtung.</summary>
    /// <param name="optionen">Die bisherigen Optionen.</param>
    /// <param name="wert">Die neue Gewichtung.</param>
    /// <returns>Neue Optionen; die übrigen Werte bleiben unverändert.</returns>
    public abstract Berechnungsoptionen Setzen(Berechnungsoptionen optionen, Gewichtung wert);

    /// <summary>Sucht die Gewichtungen einer Mannschaft (Original <c>TGewichtungMannschaften</c>).</summary>
    /// <param name="optionen">Die Optionen.</param>
    /// <param name="mannschaftsId">click-TT-Id der Mannschaft.</param>
    /// <returns>Der Eintrag oder <c>null</c> (dann gilt überall „normal“).</returns>
    protected static MannschaftsGewichtung? Eintrag(Berechnungsoptionen optionen, string mannschaftsId)
    {
        ArgumentNullException.ThrowIfNull(optionen);
        return optionen.Mannschaften.LastOrDefault(m => m.MannschaftsId == mannschaftsId);
    }

    /// <summary>Ändert den Eintrag einer Mannschaft; fehlt er, wird er mit „normal“ angelegt (Original <c>addMain</c>/<c>addDetail</c>).</summary>
    /// <param name="optionen">Die bisherigen Optionen.</param>
    /// <param name="mannschaftsId">click-TT-Id der Mannschaft.</param>
    /// <param name="aendern">Änderung des Eintrags.</param>
    /// <returns>Neue Optionen.</returns>
    protected static Berechnungsoptionen EintragSetzen(Berechnungsoptionen optionen, string mannschaftsId, Func<MannschaftsGewichtung, MannschaftsGewichtung> aendern)
    {
        ArgumentNullException.ThrowIfNull(optionen);
        ArgumentNullException.ThrowIfNull(aendern);
        var liste = optionen.Mannschaften.ToList();
        int index = liste.FindLastIndex(m => m.MannschaftsId == mannschaftsId);
        MannschaftsGewichtung bisher = index >= 0
            ? liste[index]
            : new MannschaftsGewichtung(mannschaftsId, Gewichtung.Normal, Enumerable.Repeat(Gewichtung.Normal, Berechnungsoptionen.AnzahlKostenarten).ToArray());
        MannschaftsGewichtung neu = aendern(bisher);
        if (index >= 0)
        {
            liste[index] = neu;
        }
        else
        {
            liste.Add(neu);
        }

        return optionen with { Mannschaften = liste };
    }
}
