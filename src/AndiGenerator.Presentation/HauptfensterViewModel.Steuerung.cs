// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Presentation;

/// <summary>
/// Steuerung von außen (MCP-Server): Gewichtungen lesen und ändern, Spielplandaten lesen und ändern – mit derselben
/// Übernahme wie die Oberfläche (speichern, laufende Generierung mit den neuen Daten fortsetzen, Ansichten neu bewerten).
/// </summary>
public sealed partial class HauptfensterViewModel
{
    /// <summary>
    /// Holt die Gewichtungen: alle Plan-Kostenarten, alle Mannschafts-Kostenarten, je Mannschaft ihre Gewichtung und die
    /// abweichenden Gewichtungen einzelner Kostenarten bei einer Mannschaft.
    /// </summary>
    /// <returns>Die Gewichtungen (leer ohne Staffel).</returns>
    public IReadOnlyList<Gewichtungsangabe> Gewichtungen()
    {
        if (sitzung is not Plansitzung s)
        {
            return [];
        }

        Berechnungsoptionen o = s.Optionen;
        var liste = new List<Gewichtungsangabe>();
        liste.AddRange(Enum.GetValues<PlanKostenart>().Select(a => Eintrag("Plan", Gewichtungsanzeige.Name(a), string.Empty, new PlanGewichtungsziel(a), o)));
        liste.AddRange(Enum.GetValues<MannschaftsKostenart>().Select(a => Eintrag("Kostenart", Gewichtungsanzeige.Name(a), string.Empty, new KostenartGewichtungsziel(a), o)));
        foreach (var m in s.Staffel.Mannschaften.GroupBy(m => m.Name, StringComparer.Ordinal).Select(g => g.First()))
        {
            liste.Add(Eintrag("Mannschaft", string.Empty, m.Name, new MannschaftsGewichtungsziel(m.Id, m.Name), o));
            liste.AddRange(Enum.GetValues<MannschaftsKostenart>()
                .Select(a => Eintrag("Mannschaft und Kostenart", Gewichtungsanzeige.Name(a), m.Name, new MannschaftsKostenartGewichtungsziel(m.Id, m.Name, a), o))
                .Where(e => e.Wert != Gewichtung.Normal));
        }

        return liste;
    }

    /// <summary>
    /// Sucht das Ziel einer Gewichtung nach Namen: Kostenart allein (Plan- oder Mannschafts-Kostenart), Mannschaft allein, oder
    /// Kostenart bei einer Mannschaft.
    /// </summary>
    /// <param name="kostenart">Name der Kostenart oder leer.</param>
    /// <param name="mannschaft">Name der Mannschaft oder leer.</param>
    /// <returns>Das Ziel oder <c>null</c>, wenn es die Kostenart bzw. Mannschaft nicht gibt.</returns>
    public Gewichtungsziel? GewichtungszielSuchen(string kostenart, string mannschaft)
    {
        if (sitzung is not Plansitzung s)
        {
            return null;
        }

        static bool Gleich(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        var team = s.Staffel.Mannschaften.FirstOrDefault(m => Gleich(m.Name, mannschaft));
        MannschaftsKostenart? art = Enum.GetValues<MannschaftsKostenart>().Select(a => (MannschaftsKostenart?)a).FirstOrDefault(a => Gleich(Gewichtungsanzeige.Name(a!.Value), kostenart));
        PlanKostenart? planart = Enum.GetValues<PlanKostenart>().Select(a => (PlanKostenart?)a).FirstOrDefault(a => Gleich(Gewichtungsanzeige.Name(a!.Value), kostenart));
        return (kostenart.Trim().Length > 0, mannschaft.Trim().Length > 0) switch
        {
            (true, false) when art is { } a => new KostenartGewichtungsziel(a),
            (true, false) when planart is { } p => new PlanGewichtungsziel(p),
            (false, true) when team is not null => new MannschaftsGewichtungsziel(team.Id, team.Name),
            (true, true) when team is not null && art is { } a => new MannschaftsKostenartGewichtungsziel(team.Id, team.Name, a),
            _ => null,
        };
    }

    /// <summary>Ändert eine Gewichtung wie im Detailfeld der Kostenansicht (gilt sofort, auch für die laufende Generierung).</summary>
    /// <param name="ziel">Die Gewichtung.</param>
    /// <param name="wert">Die neue Stufe.</param>
    /// <returns><c>true</c>, wenn eine Staffel offen ist.</returns>
    public bool GewichtungAendern(Gewichtungsziel ziel, Gewichtung wert)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        if (sitzung is null)
        {
            return false;
        }

        GewichtungSetzen(ziel, wert);
        return true;
    }

    /// <summary>Liest aus den Spielplandaten (Arbeitskopie der aktuellen Daten).</summary>
    /// <typeparam name="T">Art des Ergebnisses.</typeparam>
    /// <param name="lesen">Was gelesen wird.</param>
    /// <returns>Das Ergebnis oder <c>default</c> ohne Staffel.</returns>
    public T? SpielplandatenLesen<T>(Func<Datenbearbeitung, T> lesen)
    {
        ArgumentNullException.ThrowIfNull(lesen);
        return sitzung is Plansitzung s ? lesen(s.DatenBearbeiten()) : default;
    }

    /// <summary>
    /// Ändert die Spielplandaten und übernimmt sie wie nach dem Dialog „Spielplandaten bearbeiten“: speichern, laufende
    /// Generierung mit den neuen Daten fortsetzen, Pläne neu bewerten, Ansichten aktualisieren.
    /// </summary>
    /// <param name="aenderung">Die Änderung; liefert einen Fehlertext, wenn sie nicht möglich ist (dann wird nichts gespeichert).</param>
    /// <returns><c>null</c> bei Erfolg, sonst der Grund.</returns>
    public async Task<string?> SpielplandatenAendernAsync(Func<Datenbearbeitung, string?> aenderung)
    {
        ArgumentNullException.ThrowIfNull(aenderung);
        if (sitzung is not Plansitzung s)
        {
            return "Es ist keine Staffel geöffnet.";
        }

        using (dienst.Anhalten())
        {
            Datenbearbeitung daten = s.DatenBearbeiten();
            try
            {
                if (aenderung(daten) is string fehler)
                {
                    return fehler;
                }
            }
            catch (ArgumentException fehler)
            {
                return fehler.Message;
            }

            if (!await UebernehmenAsync(s, daten))
            {
                return "Die Änderungen konnten nicht gespeichert werden.";
            }

            KopfAktualisieren(s);
            NachAenderung(s);
            optionenAnsicht?.Laden();
            return null;
        }
    }

    private static Gewichtungsangabe Eintrag(string ebene, string kostenart, string mannschaft, Gewichtungsziel ziel, Berechnungsoptionen o) =>
        new(ebene, kostenart, mannschaft, ziel.Lesen(o), ziel);
}
