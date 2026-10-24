// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Persistence.Terminwuensche;

namespace AndiGenerator.Presentation.Tests;

/// <summary>
/// Die erfundene Kompaktklasse <c>testdaten/kompakt</c>, vorbereitet wie nach der Einrichtung: Kompaktspieltage bestätigt,
/// Bericht „Terminwünsche“ übernommen (je Verein eine Halle an einem echten Ort), QTTR-Schnitte für Spitzen- und
/// Abstiegsspieltag. Auch für die Bildschirmfotos.
/// </summary>
internal static class Kompaktklasse
{
    /// <summary>Kopiert die Klasse nach <paramref name="ordner"/>/kompakt und bereitet sie vor.</summary>
    /// <param name="ordner">Temporärer Ordner des Tests.</param>
    /// <returns>Pfad der Staffeldatei.</returns>
    public static string Vorbereiten(string ordner)
    {
        string daten = Path.Combine(ordner, "kompakt");
        Directory.CreateDirectory(daten);
        string datei = Path.Combine(daten, "Jugend 19 Testliga (HR).xml");
        File.Copy(Path.Combine(Testdaten.Referenz, "..", "kompakt", "Jugend_19_Testliga_(HR).xml"), datei);
        Plansitzung sitzung = Plansitzung.Oeffnen(datei, Path.Combine(ordner, "basis-kompakt"));
        Datenbearbeitung b = sitzung.DatenBearbeiten();
        b.KompaktspieltageSetzen(Kompaktmodus.Bestaetigt);
        b.TerminwuenscheUebernehmen(b.TerminwuenscheAbgleichen(Meldungen(sitzung.Staffel.Mannschaften)), "Terminmeldungen.pdf");
        b.MannschaftsstaerkenSpeichern(sitzung.Staffel.Mannschaften.Select((m, i) => new Mannschaftsstaerke(m.Name, 1450 + ((i * 173) % 500), 4)));
        sitzung.DatenUebernehmen(b);
        return datei;
    }

    /// <summary>
    /// Bericht „Terminwünsche“ zur erfundenen Kompaktklasse: je Verein eine Halle an einem echten Ort (für Karte und
    /// Entfernungen), die gemeldeten Heimtermine und Sperren, bei einer Mannschaft „Nur Heim“ an ihrem ersten Heimtermin.
    /// </summary>
    /// <param name="mannschaften">Die Mannschaften der Klasse.</param>
    /// <returns>Die Meldungen je Verein.</returns>
    public static List<Vereinsmeldung> Meldungen(IReadOnlyList<Mannschaft> mannschaften)
    {
        ArgumentNullException.ThrowIfNull(mannschaften);
        (string Plz, string Ort)[] orte =
        [
            ("55116", "Mainz"), ("56068", "Koblenz"), ("54290", "Trier"), ("67655", "Kaiserslautern"), ("55543", "Bad Kreuznach"),
            ("56564", "Neuwied"), ("67547", "Worms"), ("56410", "Montabaur"), ("55411", "Bingen am Rhein"),
        ];
        var meldungen = new List<Vereinsmeldung>();
        for (int i = 0; i < mannschaften.Count; i++)
        {
            Mannschaft m = mannschaften[i];
            (string plz, string ort) = orte[i % orte.Length];
            string halle = m.Heimspieltermine.Select(t => t.Halle).FirstOrDefault(h => h.Length > 0) ?? ("Sporthalle " + ort);
            List<GemeldeterHeimtermin> termine = m.Heimspieltermine.Select(t => new GemeldeterHeimtermin(t.Zeitpunkt, halle, "2", "-")).ToList();
            string bemerkung = i == 2 && termine.Count > 0
                ? "Nur Heim: " + termine[0].Zeitpunkt.ToString("dd.MM.", CultureInfo.InvariantCulture)
                : string.Empty;
            meldungen.Add(new Vereinsmeldung(
                m.Name,
                int.Parse(m.VereinsId, CultureInfo.InvariantCulture),
                "Jugend 19",
                new Dictionary<int, Spiellokaladresse>
                {
                    [1] = new(halle, "Schulstraße " + (i + 3).ToString(CultureInfo.InvariantCulture), plz, ort),
                },
                bemerkung,
                termine,
                m.Sperrtermine));
        }

        return meldungen;
    }
}
