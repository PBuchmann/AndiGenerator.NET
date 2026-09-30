// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Application;

/// <summary>
/// Planqualität als Zahl der Verstöße je Kriterium in der Rangfolge aus MIGRATIONSPLAN E14 (A = Muss, B = Sperr- und
/// Ausweichtermine, C = übrige). Ergänzt die gewichtete Kostensumme, die je nach Gewichtung schwer zu deuten ist.
/// </summary>
public static class Planqualitaet
{
    /// <summary>Kosten je hartem Fehler im Original (10 × <c>cMaxKostenNoHardError</c>).</summary>
    private const double KostenJeHartemFehler = 10.0 * 1_000_000_000.0;

    private static readonly MannschaftsKostenart[] StufeA = [MannschaftsKostenart.Hallenbelegung, MannschaftsKostenart.ParalleleSpiele];

    private static readonly MannschaftsKostenart[] StufeB = [MannschaftsKostenart.Sperrtermine, MannschaftsKostenart.Ausweichtermine];

    /// <summary>Ermittelt die Kriterien aus einer Bewertung.</summary>
    /// <param name="bewertung">Bewertung des Plans.</param>
    /// <returns>Die Kriterien in der Rangfolge A1 … C.</returns>
    public static IReadOnlyList<Qualitaetskriterium> Kriterien(Planbewertung bewertung)
    {
        ArgumentNullException.ThrowIfNull(bewertung);
        var ergebnis = new List<Qualitaetskriterium>
        {
            HarterFehler("Spiele ohne Termin", bewertung.NichtTerminiert),
            HarterFehler("Spiele mit ungültigem Termin", bewertung.UngueltigeSpiele),
            HarterFehler("Spiele an spielfreien Tagen", bewertung.SpieleAnSpielfreienTagen),
            Kostenart(bewertung, "A2", MannschaftsKostenart.Hallenbelegung),
            Kostenart(bewertung, "A3", MannschaftsKostenart.ParalleleSpiele),
            new("A4", "Vereinsinterne Spiele am Anfang", null, null, bewertung.VereinsinterneSpieleAmAnfang),
            Kostenart(bewertung, "A5", MannschaftsKostenart.Pflichtspieltage),
            Kostenart(bewertung, "B1", MannschaftsKostenart.Sperrtermine),
            Kostenart(bewertung, "B2", MannschaftsKostenart.Ausweichtermine),
        };

        ergebnis.AddRange(bewertung.SichtbareKostenarten
            .Where(a => !StufeA.Contains(a) && !StufeB.Contains(a) && a != MannschaftsKostenart.Pflichtspieltage)
            .Select(a => Kostenart(bewertung, "C", a)));
        ergebnis.Add(new("C", "Überlappung Spieltage", null, null, bewertung.UeberlappungSpieltage));
        ergebnis.Add(new("C", "Länge Spieltage", null, null, bewertung.LaengeSpieltage));
        ergebnis.Add(new("C", "Überlappung letzter Spieltag", null, null, bewertung.UeberlappungLetzterSpieltag));
        ergebnis.Add(new("C", "Länge letzter Spieltag", null, null, bewertung.LaengeLetzterSpieltag));
        return ergebnis;
    }

    private static Qualitaetskriterium HarterFehler(string name, double kosten) =>
        new("A1", name, (int)Math.Round(kosten / KostenJeHartemFehler), null, kosten);

    private static Qualitaetskriterium Kostenart(Planbewertung bewertung, string stufe, MannschaftsKostenart art)
    {
        int anzahl = 0;
        int gesamtzahl = 0;
        bool mitAnzahl = false;
        bool mitGesamtzahl = false;
        double kosten = 0;
        foreach (Kostenzelle zelle in bewertung.Mannschaften.Select(m => m.JeKostenart[(int)art]))
        {
            if (zelle.Anzahl >= 0)
            {
                mitAnzahl = true;
                anzahl += zelle.Anzahl;
            }

            kosten += zelle.Kosten;
            if (zelle.Gesamtzahl >= 0)
            {
                mitGesamtzahl = true;
                gesamtzahl += zelle.Gesamtzahl;
            }
        }

        return new Qualitaetskriterium(stufe, Gewichtungsanzeige.Name(art), mitAnzahl ? anzahl : null, mitGesamtzahl ? gesamtzahl : null, kosten);
    }
}
