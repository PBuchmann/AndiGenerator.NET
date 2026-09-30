// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Application;

/// <summary>
/// Beschreibung der Abweichungen gespeicherter Optionen vom Standard (Original <c>getDiffToDefault</c>), angezeigt beim
/// Öffnen einer Staffel mit der Frage, ob diese Benutzereinstellungen wieder verwendet werden sollen.
/// </summary>
public static class Optionsabweichungen
{
    /// <summary>Ermittelt die Meldungen.</summary>
    /// <param name="optionen">Die Optionen.</param>
    /// <param name="staffel">Die Staffel (für die Mannschaftsnamen).</param>
    /// <returns>Die Meldungen in der Reihenfolge des Originals; leer, wenn alles Standard ist.</returns>
    public static IReadOnlyList<string> Meldungen(Berechnungsoptionen optionen, Staffel staffel)
    {
        ArgumentNullException.ThrowIfNull(optionen);
        ArgumentNullException.ThrowIfNull(staffel);
        var ergebnis = new List<string>();
        List<string> gewichte = Gewichte(optionen, staffel);
        if (gewichte.Count > 0)
        {
            ergebnis.Add("Gewichtung");
            ergebnis.AddRange(gewichte);
            ergebnis.Add(string.Empty);
        }

        if (!optionen.FreitagZaehltZumWochenende)
        {
            ergebnis.Add("Freitag ist bei der 60km-Regel nicht erlaubt");
            ergebnis.Add(string.Empty);
        }

        string? runde = optionen.Rundenplanung switch
        {
            Rundenplanung.Halbrunde => "Halbrunde wird generiert",
            Rundenplanung.NurVorrunde => "Nur Vorrunde wird generiert",
            Rundenplanung.NurRueckrunde => "Nur Rückrunde wird generiert",
            Rundenplanung.Corona => "Nur die \"Corona\"-Rückrunde wird generiert",
            _ => null,
        };
        if (runde is not null)
        {
            ergebnis.Add(runde);
            ergebnis.Add(string.Empty);
        }

        if (optionen.Doppelrunde)
        {
            ergebnis.Add("Doppelrunde ist aktiv");
            ergebnis.Add(string.Empty);
        }

        return ergebnis;
    }

    private static List<string> Gewichte(Berechnungsoptionen o, Staffel staffel)
    {
        var zeilen = new List<string>();
        void Pruefen(string name, Gewichtung wert)
        {
            if (wert != Gewichtung.Normal)
            {
                zeilen.Add("  " + name + ": " + Gewichtungsanzeige.Name(wert));
            }
        }

        Pruefen("Überlappung der Spieltage", o.SpieltagUeberlappung);
        Pruefen("Länge der Spieltage", o.Spieltag);
        Pruefen("Vereinsinterne Spiele am Anfang", o.VereinsinterneSpieleAmAnfang);
        Pruefen("Überlappung letzter Spieltag", o.LetzterSpieltagUeberlappung);
        Pruefen("Länge letzter Spieltag", o.LetzterSpieltag);
        foreach (MannschaftsKostenart art in Enum.GetValues<MannschaftsKostenart>())
        {
            Pruefen(Gewichtungsanzeige.Name(art), o.Fuer(art));
        }

        foreach (Mannschaft mannschaft in staffel.Mannschaften)
        {
            Pruefen(mannschaft.Name, new MannschaftsGewichtungsziel(mannschaft.Id, mannschaft.Name).Lesen(o));
            foreach (MannschaftsKostenart art in Enum.GetValues<MannschaftsKostenart>())
            {
                Pruefen(mannschaft.Name + " " + Gewichtungsanzeige.Name(art), new MannschaftsKostenartGewichtungsziel(mannschaft.Id, mannschaft.Name, art).Lesen(o));
            }
        }

        return zeilen;
    }
}
