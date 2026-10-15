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

    /// <summary>Fügt eine Zeile mit Faktoren hinzu: <paramref name="unten"/> ≤ Summe (Faktor × Spalte) ≤ <paramref name="oben"/>.</summary>
    /// <param name="eintraege">Spalten mit Faktor (eine Spalte höchstens einmal).</param>
    /// <param name="unten">Untere Grenze.</param>
    /// <param name="oben">Obere Grenze.</param>
    public void Zeile(IEnumerable<(int Spalte, double Faktor)> eintraege, double unten, double oben)
    {
        foreach ((int s, double faktor) in eintraege)
        {
            spalten.Add(s);
            werte.Add(faktor);
        }

        zeilenAnfang.Add(spalten.Count);
        zeileUnten.Add(unten);
        zeileOben.Add(oben);
    }

    /// <summary>Fügt eine ganzzahlige Spalte von 0 bis <paramref name="oben"/> hinzu (z. B. eine Zahl von Verstößen).</summary>
    /// <param name="preis">Kosten je Einheit.</param>
    /// <param name="oben">Obere Grenze.</param>
    /// <returns>Die Nummer der Spalte.</returns>
    public int Zahl(double preis, int oben)
    {
        kosten.Add(preis);
        spalteOben.Add(oben);
        return kosten.Count - 1;
    }

    /// <summary>Erstellt eine unabhängige Kopie (z. B. um mehrere Ziele gleichzeitig auf eigenen Modellen zu lösen).</summary>
    /// <returns>Die Kopie.</returns>
    public Ganzzahlmodell Kopie()
    {
        var kopie = new Ganzzahlmodell();
        kopie.kosten.AddRange(kosten);
        kopie.spalteOben.AddRange(spalteOben);
        kopie.zeilenAnfang.Clear();
        kopie.zeilenAnfang.AddRange(zeilenAnfang);
        kopie.spalten.AddRange(spalten);
        kopie.werte.AddRange(werte);
        kopie.zeileUnten.AddRange(zeileUnten);
        kopie.zeileOben.AddRange(zeileOben);
        return kopie;
    }

    /// <summary>
    /// Fingerabdruck des Modells ohne die Kosten (Spalten, Zeilen, Grenzen): Gleiche Modelle haben denselben, z. B. um sich
    /// Ergebnisse über mehrere Rechnungen zu merken.
    /// </summary>
    /// <returns>Der Fingerabdruck (SHA-256, hexadezimal).</returns>
    public string Fingerabdruck()
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        void Hinzu<T>(List<T> liste)
            where T : struct
        {
            hash.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(liste)));
            hash.AppendData(BitConverter.GetBytes(liste.Count));
        }

        Hinzu(spalteOben);
        Hinzu(zeilenAnfang);
        Hinzu(spalten);
        Hinzu(werte);
        Hinzu(zeileUnten);
        Hinzu(zeileOben);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>Ändert die Kosten einer Spalte (für mehrere Ziele nacheinander im selben Modell).</summary>
    /// <param name="spalte">Die Spalte.</param>
    /// <param name="preis">Die neuen Kosten.</param>
    public void Preis(int spalte, double preis) => kosten[spalte] = preis;

    /// <summary>Ändert die obere Grenze einer Zeile (z. B. um das Ergebnis einer früheren Stufe festzuhalten).</summary>
    /// <param name="zeile">Die Zeile.</param>
    /// <param name="oben">Die neue Grenze.</param>
    public void ZeileBegrenzen(int zeile, double oben) => zeileOben[zeile] = oben;

    /// <summary>Ändert beide Grenzen einer Zeile (z. B. um eine vorübergehende Festlegung wieder zu lösen).</summary>
    /// <param name="zeile">Die Zeile.</param>
    /// <param name="unten">Die neue untere Grenze.</param>
    /// <param name="oben">Die neue obere Grenze.</param>
    public void ZeileGrenzen(int zeile, double unten, double oben)
    {
        zeileUnten[zeile] = unten;
        zeileOben[zeile] = oben;
    }
}
