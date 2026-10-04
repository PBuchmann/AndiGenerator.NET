// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Application;

/// <summary>
/// Planqualität als Zahl der Verstöße je Kriterium in der Rangfolge aus MIGRATIONSPLAN E14: zuerst die harten Fehler (A1),
/// dann die Kriterien in der Einteilung des Staffelleiters (Stufen A, B, C, darin in seiner Reihenfolge nummeriert).
/// Ergänzt die gewichtete Kostensumme, die je nach Gewichtung schwer zu deuten ist.
/// </summary>
public static class Planqualitaet
{
    /// <summary>Kosten je hartem Fehler im Original (10 × <c>cMaxKostenNoHardError</c>).</summary>
    private const double KostenJeHartemFehler = 10.0 * 1_000_000_000.0;

    /// <summary>Ermittelt die Kriterien aus einer Bewertung.</summary>
    /// <param name="bewertung">Bewertung des Plans.</param>
    /// <param name="einteilung">Einteilung der Kriterien; <c>null</c> = <see cref="Stufeneinteilung.Standard"/>.</param>
    /// <returns>Die Kriterien: A1 (harte Fehler), dann A2 …, B1 …, C1 … in der Reihenfolge der Einteilung.</returns>
    public static IReadOnlyList<Qualitaetskriterium> Kriterien(Planbewertung bewertung, Stufeneinteilung? einteilung = null)
    {
        ArgumentNullException.ThrowIfNull(bewertung);
        Stufeneinteilung e = (einteilung ?? Stufeneinteilung.Standard).Vervollstaendigt();
        var ergebnis = new List<Qualitaetskriterium>
        {
            HarterFehler("Spiele ohne Termin", bewertung.NichtTerminiert),
            HarterFehler("Spiele mit ungültigem Termin", bewertung.UngueltigeSpiele),
            HarterFehler("Spiele an spielfreien Tagen", bewertung.SpieleAnSpielfreienTagen),
        };
        ergebnis.AddRange(Stufe(bewertung, "A", e.A, 2));
        ergebnis.AddRange(Stufe(bewertung, "B", e.B, 1));
        ergebnis.AddRange(Stufe(bewertung, "C", e.C, 1));
        return ergebnis;
    }

    /// <summary>Anzeigename eines Kriteriums, wie in der Qualitätsansicht.</summary>
    /// <param name="k">Das Kriterium.</param>
    /// <returns>Der Name, z. B. „Sperrtermine“ oder „Länge Spieltage“.</returns>
    public static string Name(Kostenkriterium k) => k switch
    {
        Kostenkriterium.VereinsinterneSpieleAmAnfang => "Vereinsinterne Spiele am Anfang",
        Kostenkriterium.Spieltaglaenge => "Länge Spieltage",
        Kostenkriterium.Spieltagueberlappung => "Überlappung Spieltage",
        Kostenkriterium.LetzterSpieltagLaenge => "Länge letzter Spieltag",
        Kostenkriterium.LetzterSpieltagUeberlappung => "Überlappung letzter Spieltag",
        _ => Gewichtungsanzeige.Name((MannschaftsKostenart)(int)k),
    };

    private static IEnumerable<Qualitaetskriterium> Stufe(Planbewertung bewertung, string stufe, IReadOnlyList<Kostenkriterium> kriterien, int erste)
    {
        int nummer = erste;
        foreach (Kostenkriterium k in kriterien)
        {
            // Kostenarten, die in dieser Staffel keine Rolle spielen (z. B. Setzliste ohne Setzliste), zeigt die Ansicht in C nicht.
            if (stufe == "C" && (int)k < (int)Kostenkriterium.VereinsinterneSpieleAmAnfang && !bewertung.SichtbareKostenarten.Contains((MannschaftsKostenart)(int)k))
            {
                continue;
            }

            yield return Kriterium(bewertung, string.Create(CultureInfo.InvariantCulture, $"{stufe}{nummer++}"), k);
        }
    }

    private static Qualitaetskriterium Kriterium(Planbewertung bewertung, string stufe, Kostenkriterium k) => k switch
    {
        Kostenkriterium.VereinsinterneSpieleAmAnfang => new(stufe, Name(k), null, null, bewertung.VereinsinterneSpieleAmAnfang, k),
        Kostenkriterium.Spieltaglaenge => new(stufe, Name(k), null, null, bewertung.LaengeSpieltage, k),
        Kostenkriterium.Spieltagueberlappung => new(stufe, Name(k), null, null, bewertung.UeberlappungSpieltage, k),
        Kostenkriterium.LetzterSpieltagLaenge => new(stufe, Name(k), null, null, bewertung.LaengeLetzterSpieltag, k),
        Kostenkriterium.LetzterSpieltagUeberlappung => new(stufe, Name(k), null, null, bewertung.UeberlappungLetzterSpieltag, k),
        _ => Kostenart(bewertung, stufe, (MannschaftsKostenart)(int)k) with { Kriterium = k },
    };

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
