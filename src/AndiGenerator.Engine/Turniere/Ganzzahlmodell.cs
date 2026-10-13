// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Ein ganzzahliges lineares Programm (nicht im Original): Spalten sind Ja/Nein-Entscheidungen (0 oder 1) mit Kosten,
/// Zeilen sind Summen von Spalten zwischen einer unteren und oberen Grenze; gesucht ist die Wahl mit den kleinsten Kosten.
/// Gelöst wird es mit einem <see cref="IGanzzahlloeser"/>.
/// </summary>
public sealed class Ganzzahlmodell
{
    private readonly List<double> kosten = [];
    private readonly List<double> spalteOben = [];
    private readonly List<int> zeilenAnfang = [0];
    private readonly List<int> spalten = [];
    private readonly List<double> werte = [];
    private readonly List<double> zeileUnten = [];
    private readonly List<double> zeileOben = [];

    /// <summary>Holt die Zahl der Spalten.</summary>
    public int Spalten => kosten.Count;

    /// <summary>Holt die Zahl der Zeilen.</summary>
    public int Zeilen => zeileUnten.Count;

    /// <summary>Holt die Kosten der Spalten.</summary>
    public IReadOnlyList<double> Kosten => kosten;

    /// <summary>Holt die oberen Grenzen der Spalten (0 = ausgeschlossen, 1 = möglich).</summary>
    public IReadOnlyList<double> SpalteOben => spalteOben;

    /// <summary>Holt, wo die Einträge jeder Zeile beginnen (zeilenweise, ein Eintrag mehr als Zeilen).</summary>
    public IReadOnlyList<int> ZeilenAnfang => zeilenAnfang;

    /// <summary>Holt die Spalten der Einträge.</summary>
    public IReadOnlyList<int> EintragSpalten => spalten;

    /// <summary>Holt die Werte der Einträge.</summary>
    public IReadOnlyList<double> EintragWerte => werte;

    /// <summary>Holt die unteren Grenzen der Zeilen.</summary>
    public IReadOnlyList<double> ZeileUnten => zeileUnten;

    /// <summary>Holt die oberen Grenzen der Zeilen (<see cref="double.PositiveInfinity"/> = keine).</summary>
    public IReadOnlyList<double> ZeileOben => zeileOben;

    /// <summary>Fügt eine Ja/Nein-Spalte hinzu.</summary>
    /// <param name="preis">Kosten, wenn die Spalte gewählt wird.</param>
    /// <param name="moeglich"><c>false</c> schließt die Spalte aus.</param>
    /// <returns>Die Nummer der Spalte.</returns>
    public int Spalte(double preis, bool moeglich = true)
    {
        kosten.Add(preis);
        spalteOben.Add(moeglich ? 1 : 0);
        return kosten.Count - 1;
    }

    /// <summary>Fügt eine Zeile hinzu: <paramref name="unten"/> ≤ Summe der Spalten (mit Faktor 1) ≤ <paramref name="oben"/>.</summary>
    /// <param name="eintraege">Die Spalten der Summe.</param>
    /// <param name="unten">Untere Grenze.</param>
    /// <param name="oben">Obere Grenze.</param>
    public void Zeile(IEnumerable<int> eintraege, double unten, double oben)
    {
        foreach (int s in eintraege)
        {
            spalten.Add(s);
            werte.Add(1);
        }

        zeilenAnfang.Add(spalten.Count);
        zeileUnten.Add(unten);
        zeileOben.Add(oben);
    }
}
