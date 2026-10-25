// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Rendering;

/// <summary>Das Diagramm „Setzliste“ als Treppe je Runde (nicht im Original).</summary>
public sealed partial class Diagrammzeichner
{
    private const double Setzzeile = 64;
    private const double Setzbalken = 12;
    private const double Setzluecke = 4;
    private const double Rundenabstand = 44;

    private static readonly Farbe Treppenflaeche = new(0xE6, 0xF2, 0xE9);
    private static readonly Farbe Kleingruen = new(0x4F, 0x9A, 0x6B);

    /// <summary>
    /// Farbe eines Balkens wie die Bewertung zählt: grün, wenn der Gegner höchstens so weit entfernt ist wie gewünscht (kein
    /// Verstoß); sonst nach dem Überschuss orange (1 bis 2 Plätze zu weit) oder rot (ab 3).
    /// </summary>
    /// <param name="ueberschuss">Abstand zum Gegner minus gewünschter Abstand; höchstens 0 = kein Verstoß.</param>
    /// <returns>Die Farbe.</returns>
    public static Farbe Setzlistenfarbe(int ueberschuss)
    {
        if (ueberschuss <= 0)
        {
            return Kleingruen;
        }

        return ueberschuss <= 2 ? Pausenorange : Schlecht;
    }

    /// <summary>Name einer Runde über ihrer Treppe.</summary>
    /// <param name="runde">Index der Runde (ab 0).</param>
    /// <param name="stelle">Stelle der Treppe (ab 0).</param>
    /// <param name="runden">Anzahl der Runden mit Spielen.</param>
    /// <param name="namen">Die Namen der Runden aus den Diagrammdaten oder <c>null</c>.</param>
    /// <returns>Der Name.</returns>
    private static string Rundenname(int runde, int stelle, int runden, IReadOnlyList<string>? namen)
    {
        // Eigene Namen nur bei einer Doppelrunde (sonst heißen die Runden hier wie bisher Hin- und Rückrunde).
        if (namen is { Count: > 0 } && runde < namen.Count && namen[^1] != "Rückrunde")
        {
            return namen[runde];
        }

        if (runden != 2)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{stelle + 1}. Runde");
        }

        return stelle == 0 ? "Hinrunde" : "Rückrunde";
    }

    private static string Setzlistenhinweis(int k, Diagrammspiel s, int ziel, bool verstoss)
    {
        string gegen = s.Gegner.Length == 0 ? string.Empty : " gegen " + s.Gegner;
        var zeilen = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"{k}. Spiel der Runde, ") + s.Zeitpunkt!.Value.ToString("ddd dd.MM.yyyy", Deutsch) + gegen,
            string.Create(CultureInfo.InvariantCulture, $"{s.Setzlistenabstand} Plätze entfernt, gewünscht {ziel}"),
        };
        if (verstoss)
        {
            zeilen.Add("zählt als Verstoß: Gegner zu weit entfernt für dieses späte Spiel");
        }

        return string.Join(Environment.NewLine, zeilen);
    }

    /// <summary>
    /// Je Mannschaft und Runde eine Treppe: die Stufe ist der gewünschte Abstand zum Gegner in der Setzliste (das k-te Spiel
    /// der Runde gegen einen N − k Plätze entfernten Gegner), der Balken der tatsächliche. Gefärbt wie die Bewertung zählt:
    /// grün ohne Verstoß, orange oder rot, wenn der Gegner weiter entfernt ist als die Stufe. Rechts die Verstöße wie in der
    /// Qualitätsansicht.
    /// </summary>
    private Zeichengroesse Setzliste(double y)
    {
        Titel("Setzliste", y);
        y += 30;
        int stufen = Math.Max(1, Anzahl - 1);
        double einheit = Math.Min(5, 44.0 / stufen);
        double rundenbreite = (stufen * (Setzbalken + Setzluecke)) + Rundenabstand;

        // Je Runde (Halbrunde) der Klasse eine Treppe nebeneinander; die Runden sind Indizes der Bewertung (ab 0).
        List<int> runden = d.Mannschaften.SelectMany(m => m.Spiele).Where(s => s.Zeitpunkt is not null && s.Runde >= 0).Select(s => s.Runde).Distinct().Order().ToList();
        if (runden.Count == 0)
        {
            runden.Add(0);
        }

        for (int r = 0; r < runden.Count; r++)
        {
            Text(Rundenname(runden[r], r, runden.Count, d.Rundennamen), Links + (r * rundenbreite), y, Normal, Beschriftung);
        }

        double rechts = Links + (runden.Count * rundenbreite);
        Text("Verstöße", rechts, y, Normal, Beschriftung);
        double breite = rechts + 80;
        y += 22;
        for (int i = 0; i < d.Mannschaften.Count; i++)
        {
            double oben = y + (i * Setzzeile);
            if (i % 2 == 1)
            {
                Rechteck(Band, 0, oben, breite, oben + Setzzeile);
            }

            Setzlistenzeile(d.Mannschaften[i], oben + Setzzeile - 10, runden, rundenbreite, einheit, rechts);
        }

        y += (d.Mannschaften.Count * Setzzeile) + 16;
        double x = 0;
        Rechteck(Treppenflaeche, x, y, x + 30, y + 16);
        x = Legendentext("gewünschter Abstand (k-tes Spiel der Runde: N − k Plätze)", x + 36, y);
        foreach ((string text, int ueberschuss) in new[] { ("kein Verstoß", 0), ("Verstoß: 1 bis 2 Plätze zu weit", 1), ("ab 3 Plätze zu weit", 3) })
        {
            f?.Vieleck(Rundrechteck.Punkte(x, y + 1, x + 12, y + 15, 2), Setzlistenfarbe(ueberschuss), null, 0, geschlossen: true);
            x = Legendentext(text, x + 18, y) - 12;
        }

        x = Legendentext("Verstöße wie in der Qualitätsansicht", x + 12, y);
        return new Zeichengroesse(Math.Max(breite, x), y + 26);
    }

    private void Setzlistenzeile(Diagrammmannschaft m, double grund, List<int> runden, double rundenbreite, double einheit, double rechts)
    {
        Name(m.Name, grund - 16);
        List<Diagrammspiel> spiele = m.Spiele.Where(s => s.Zeitpunkt is not null).ToList();
        int runde = -1;
        int k = 0;
        int verstoesse = 0;
        double x0 = Links;
        foreach (Diagrammspiel s in spiele)
        {
            if (s.Runde != runde)
            {
                runde = s.Runde;
                k = 0;
                x0 = Links + (Math.Max(0, runden.IndexOf(runde)) * rundenbreite);
                Treppe(x0, grund, einheit);
            }

            k++;
            int ziel = Anzahl - k;
            int abweichung = s.Setzlistenabstand - ziel;
            bool verstoss = abweichung > 0 && ziel > 0;
            verstoesse += verstoss ? 1 : 0;
            double x = x0 + ((k - 1) * (Setzbalken + Setzluecke));
            double hoehe = Math.Max(1.5, s.Setzlistenabstand * einheit);
            f?.Vieleck(Rundrechteck.Punkte(x, grund - hoehe, x + Setzbalken, grund, 2), Setzlistenfarbe(verstoss ? abweichung : 0), null, 0, geschlossen: true);

            hinweise.Add(new Diagrammhinweis(
                x + (Setzbalken / 2), grund - 25, 0, Setzlistenhinweis(k, s, ziel, verstoss), Setzbalken + Setzluecke, 50));
        }

        int zahl = d.Verstoesse?.Von(m.Name)?.Setzliste ?? verstoesse;
        f?.Text(zahl.ToString(CultureInfo.InvariantCulture), rechts + 8, grund - 16, Normal, zahl > 0 ? Schlecht : Gut, true);
    }

    /// <summary>Die Stufen der gewünschten Abstände einer Runde als Fläche.</summary>
    private void Treppe(double x0, double grund, double einheit)
    {
        var punkte = new List<(double X, double Y)> { (x0 - 2, grund) };
        for (int k = 1; k < Anzahl; k++)
        {
            double links = x0 + ((k - 1) * (Setzbalken + Setzluecke)) - 2;
            double hoehe = (Anzahl - k) * einheit;
            punkte.Add((links, grund - hoehe));
            punkte.Add((links + Setzbalken + Setzluecke, grund - hoehe));
        }

        punkte.Add((punkte[^1].X, grund));
        f?.Vieleck(punkte, Treppenflaeche, null, 0, geschlossen: true);
        Linie(Spannengrau, x0 - 4, grund, punkte[^1].X, grund);
    }
}
