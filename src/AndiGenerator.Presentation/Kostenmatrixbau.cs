// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Presentation;

/// <summary>
/// Baut Kostenmatrix und Details der Kostenansicht (neu gegenüber dem Original, das eine breite Tabelle mit allen Werten
/// je Zelle und je Klick einen Gewichtungsdialog zeigt): Spalten und Zeilen nach Kosten sortiert, Kostenarten ohne Kosten
/// ausblendbar, je Zelle wahlweise Kosten oder Verstöße.
/// </summary>
internal static class Kostenmatrixbau
{
    private const string Eingehalten = "·";

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Baut die Matrix.</summary>
    /// <param name="b">Die Bewertung.</param>
    /// <param name="optionen">Die Optionen (Gewichtungsmarken).</param>
    /// <param name="staffel">Die Staffel (IDs der Mannschaften).</param>
    /// <param name="leereZeigen">Auch Kostenarten ohne Kosten zeigen.</param>
    /// <param name="verstoesse">Verstöße statt Kosten in den Zellen.</param>
    /// <param name="auswahl">Die Auswahl (für die Hervorhebung) oder <c>null</c>.</param>
    /// <returns>Die Matrix.</returns>
    internal static Kostenmatrix Bauen(Planbewertung b, Berechnungsoptionen optionen, Staffel staffel, bool leereZeigen, bool verstoesse, Kostenauswahl? auswahl)
    {
        double gesamt = b.Gesamtkosten;
        Dictionary<string, string> ids = Ids(staffel);
        Dictionary<MannschaftsKostenart, double> summen = b.SichtbareKostenarten.ToDictionary(art => art, art => b.Mannschaften.Sum(m => m.JeKostenart[(int)art].Kosten));
        List<MannschaftsKostenart> arten = b.SichtbareKostenarten
            .Where(art => leereZeigen || summen[art] > 0)
            .OrderByDescending(art => summen[art])
            .ToList();
        double hoechsteSpalte = summen.Values.DefaultIfEmpty(0).Max();
        double hoechsteZeile = b.Mannschaften.Select(m => m.Gesamt).DefaultIfEmpty(0).Max();

        List<Matrixspalte> spalten = arten.Select(art => new Matrixspalte(
            art,
            Gewichtungsanzeige.Name(art),
            Gewichtungsanzeige.Markierung(Gewichtungsanzeige.Wert(optionen.Fuer(art))),
            Verhaeltnis(summen[art], hoechsteSpalte),
            summen[art] > 0,
            auswahl is { Art: Kostenauswahlart.Kostenart } && auswahl.Kostenart == art)).ToList();

        List<Matrixzeile> zeilen = b.Mannschaften
            .OrderByDescending(m => m.Gesamt)
            .Select(m =>
            {
                string id = ids.GetValueOrDefault(m.Name, m.Name);
                Gewichtung mannschaft = new MannschaftsGewichtungsziel(id, m.Name).Lesen(optionen);
                List<Matrixzelle> zellen = arten.Select(art =>
                {
                    Kostenzelle zelle = m.JeKostenart[(int)art];
                    bool geaendert = new MannschaftsKostenartGewichtungsziel(id, m.Name, art).Lesen(optionen) != Gewichtung.Normal;
                    bool gewaehlt = auswahl is { Art: Kostenauswahlart.Zelle } && auswahl.Kostenart == art && auswahl.Mannschaft == m.Name;
                    string hinweis = $"{m.Name} · {Gewichtungsanzeige.Name(art)}: {zelle.Anzeige()}";
                    return new Matrixzelle(m.Name, art, Zelltext(zelle, verstoesse), Anteil(zelle.Kosten, gesamt), geaendert, gewaehlt, hinweis);
                }).ToList();
                bool zeileGewaehlt = auswahl is { Art: Kostenauswahlart.Mannschaft } && auswahl.Mannschaft == m.Name;
                string marke = Gewichtungsanzeige.Markierung(Gewichtungsanzeige.Wert(mannschaft));
                return new Matrixzeile(m.Name, marke, Kostenanzeige.Kurz(m.Gesamt), Verhaeltnis(m.Gesamt, hoechsteZeile), zeileGewaehlt, zellen);
            }).ToList();

        double alle = summen.Values.Sum();
        List<Kostenanteil> verteilung = summen
            .Where(p => p.Value > 0)
            .OrderByDescending(p => p.Value)
            .Select(p => new Kostenanteil(p.Key, Gewichtungsanzeige.Name(p.Key), p.Value / alle, Prozent(p.Value, alle)))
            .ToList();
        int ohneKosten = summen.Count(p => p.Value <= 0);
        return new Kostenmatrix(spalten, zeilen, verteilung, Kostenanzeige.Kurz(alle), leereZeigen ? 0 : ohneKosten, ohneKosten);
    }

    /// <summary>Baut die Details zur Auswahl.</summary>
    /// <param name="b">Die Bewertung.</param>
    /// <param name="optionen">Die Optionen.</param>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="kennzahlen">Die Kennzahlen des Plans.</param>
    /// <param name="auswahl">Die Auswahl.</param>
    /// <param name="meldungen">Liefert die Meldungen einer Mannschaft zu einer Kostenart.</param>
    /// <returns>Die Details oder <c>null</c>, wenn die Auswahl im Plan nicht (mehr) vorkommt.</returns>
    internal static Kostendetail? Detail(
        Planbewertung b, Berechnungsoptionen optionen, Staffel staffel, IReadOnlyList<Kennzahl> kennzahlen, Kostenauswahl auswahl, Func<string, MannschaftsKostenart, IReadOnlyList<string>> meldungen)
    {
        Mannschaftsbewertung? m = b.Mannschaften.FirstOrDefault(x => x.Name == auswahl.Mannschaft);
        string id = m is null ? string.Empty : Ids(staffel).GetValueOrDefault(m.Name, m.Name);
        return auswahl.Art switch
        {
            Kostenauswahlart.Zelle when m is not null => Zellendetail(b, optionen, m, id, auswahl.Kostenart, meldungen),
            Kostenauswahlart.Kostenart => Kostenartdetail(b, optionen, auswahl.Kostenart),
            Kostenauswahlart.Mannschaft when m is not null => Mannschaftsdetail(b, optionen, m, id),
            Kostenauswahlart.Kennzahl when kennzahlen.FirstOrDefault(k => k.Name == auswahl.Kennzahl) is Kennzahl k => Kennzahldetail(b, optionen, k),
            _ => null,
        };
    }

    /// <summary>Die Tasten der Gewichtung.</summary>
    /// <param name="aktuell">Die eingestellte Stufe.</param>
    /// <returns>Die sieben Stufen.</returns>
    internal static List<Gewichtungsstufe> Stufen(Gewichtung aktuell) =>
        Gewichtungsanzeige.AlleStufen
            .Select(s => new Gewichtungsstufe(s, s == Gewichtung.NichtBeruecksichtigen ? "aus" : Gewichtungsanzeige.Name(s), s == aktuell))
            .ToList();

    private static Kostendetail Zellendetail(
        Planbewertung b, Berechnungsoptionen optionen, Mannschaftsbewertung m, string id, MannschaftsKostenart art, Func<string, MannschaftsKostenart, IReadOnlyList<string>> meldungen)
    {
        Kostenzelle zelle = m.JeKostenart[(int)art];
        var ziel = new MannschaftsKostenartGewichtungsziel(id, m.Name, art);
        return new Kostendetail(
            "ZELLE",
            Gewichtungsanzeige.Name(art),
            m.Name,
            Verstoesse(zelle),
            Kostenanzeige.Kurz(zelle.Kosten),
            Prozent(zelle.Kosten, b.Gesamtkosten),
            "nur diese Mannschaft und Kostenart",
            ziel,
            Stufen(ziel.Lesen(optionen)),
            meldungen(m.Name, art),
            "Meldungen",
            IstZelle: true);
    }

    private static Kostendetail Kostenartdetail(Planbewertung b, Berechnungsoptionen optionen, MannschaftsKostenart art)
    {
        double summe = b.Mannschaften.Sum(x => x.JeKostenart[(int)art].Kosten);
        int anzahl = b.Mannschaften.Sum(x => Math.Max(0, x.JeKostenart[(int)art].Anzahl));
        bool gezaehlt = b.Mannschaften.Any(x => x.JeKostenart[(int)art].Anzahl >= 0);
        var ziel = new KostenartGewichtungsziel(art);
        List<string> aufteilung = b.Mannschaften
            .Where(x => x.JeKostenart[(int)art].Kosten > 0)
            .OrderByDescending(x => x.JeKostenart[(int)art].Kosten)
            .Select(x => $"{x.Name}: {x.JeKostenart[(int)art].Anzeige()}")
            .ToList();
        return new Kostendetail(
            "KOSTENART",
            Gewichtungsanzeige.Name(art),
            "alle Mannschaften",
            gezaehlt ? anzahl.ToString(Deutsch) : "–",
            Kostenanzeige.Kurz(summe),
            Prozent(summe, b.Gesamtkosten),
            "diese Kostenart bei allen Mannschaften",
            ziel,
            Stufen(ziel.Lesen(optionen)),
            aufteilung,
            "Kosten je Mannschaft",
            IstZelle: false);
    }

    private static Kostendetail Mannschaftsdetail(Planbewertung b, Berechnungsoptionen optionen, Mannschaftsbewertung m, string id)
    {
        var ziel = new MannschaftsGewichtungsziel(id, m.Name);
        int anzahl = b.SichtbareKostenarten.Sum(art => Math.Max(0, m.JeKostenart[(int)art].Anzahl));
        List<string> aufteilung = b.SichtbareKostenarten
            .Where(art => m.JeKostenart[(int)art].Kosten > 0)
            .OrderByDescending(art => m.JeKostenart[(int)art].Kosten)
            .Select(art => $"{Gewichtungsanzeige.Name(art)}: {m.JeKostenart[(int)art].Anzeige()}")
            .ToList();
        return new Kostendetail(
            "MANNSCHAFT",
            m.Name,
            "alle Kostenarten",
            anzahl.ToString(Deutsch),
            Kostenanzeige.Kurz(m.Gesamt),
            Prozent(m.Gesamt, b.Gesamtkosten),
            "alle Kosten dieser Mannschaft",
            ziel,
            Stufen(ziel.Lesen(optionen)),
            aufteilung,
            "Kosten je Kostenart",
            IstZelle: false);
    }

    private static Kostendetail Kennzahldetail(Planbewertung b, Berechnungsoptionen optionen, Kennzahl k) =>
        new(
            "KENNZAHL DES PLANS",
            k.Name,
            "ganzer Plan",
            "–",
            k.Wert.Text,
            Prozent(k.Wert.Anteil * b.Gesamtkosten, b.Gesamtkosten),
            "diese Kennzahl",
            k.Wert.Ziel,
            k.Wert.Ziel is Gewichtungsziel ziel ? Stufen(ziel.Lesen(optionen)) : [],
            [],
            "Meldungen",
            IstZelle: false);

    private static string Zelltext(Kostenzelle zelle, bool verstoesse)
    {
        if (zelle.Kosten <= 0 && zelle.Anzahl <= 0)
        {
            return Eingehalten;
        }

        if (!verstoesse)
        {
            return Kostenanzeige.Kurz(zelle.Kosten);
        }

        if (zelle.Anzahl < 0)
        {
            return "–";
        }

        return zelle.Gesamtzahl >= 0 ? $"{zelle.Anzahl}/{zelle.Gesamtzahl}" : zelle.Anzahl.ToString(Deutsch);
    }

    private static string Verstoesse(Kostenzelle zelle)
    {
        if (zelle.Anzahl < 0)
        {
            return "–";
        }

        return zelle.Gesamtzahl >= 0 ? $"{zelle.Anzahl} von {zelle.Gesamtzahl}" : zelle.Anzahl.ToString(Deutsch);
    }

    private static Dictionary<string, string> Ids(Staffel staffel) =>
        staffel.Mannschaften.GroupBy(m => m.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

    private static double Anteil(double kosten, double gesamt) => gesamt > 0 ? Math.Clamp(kosten / gesamt, 0, 1) : 0;

    private static double Verhaeltnis(double wert, double hoechster) => hoechster > 0 ? Math.Clamp(wert / hoechster, 0, 1) : 0;

    private static string Prozent(double kosten, double gesamt)
    {
        double prozent = Anteil(kosten, gesamt) * 100;
        return prozent is > 0 and < 1 ? "< 1 %" : string.Create(Deutsch, $"{prozent:0} %");
    }
}
