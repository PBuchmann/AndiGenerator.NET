// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Presentation;

/// <summary>Kennzahlen und Kostentabelle wie im Tab „Kosten“ des Originals; für die Ansicht und den Ausdruck.</summary>
internal static class Kostendarstellung
{
    /// <summary>
    /// Gesamtkosten und Plan-Kostenarten wie im Original (<c>PaintKosten</c>): nicht terminierte und ungültige Spiele,
    /// Spiele an spielfreien Tagen und vereinsinterne Spiele am Anfang nur, wenn sie Kosten verursachen; die vier
    /// Spieltag-Kostenarten immer.
    /// </summary>
    /// <param name="b">Die Bewertung.</param>
    /// <param name="optionen">Die Optionen (Gewichtungsmarken).</param>
    /// <returns>Die Kennzahlen.</returns>
    internal static List<Kennzahl> Kennzahlen(Planbewertung b, Berechnungsoptionen optionen)
    {
        double gesamt = b.Gesamtkosten;
        var liste = new List<Kennzahl> { new("Gesamtkosten", Kostenfeld.Nur(Kostenanzeige.Kurz(gesamt))) };
        WennKosten(liste, "Spiele nicht terminiert", b.NichtTerminiert, gesamt);
        WennKosten(liste, "Ungültige Spiele", b.UngueltigeSpiele, gesamt);
        WennKosten(liste, "Spiele an spielfreien Tagen", b.SpieleAnSpielfreienTagen, gesamt);
        liste.Add(PlanKennzahl(PlanKostenart.UeberlappungSpieltage, b.UeberlappungSpieltage, gesamt, optionen));
        liste.Add(PlanKennzahl(PlanKostenart.LaengeSpieltage, b.LaengeSpieltage, gesamt, optionen));
        liste.Add(PlanKennzahl(PlanKostenart.UeberlappungLetzterSpieltag, b.UeberlappungLetzterSpieltag, gesamt, optionen));
        liste.Add(PlanKennzahl(PlanKostenart.LaengeLetzterSpieltag, b.LaengeLetzterSpieltag, gesamt, optionen));
        if (b.VereinsinterneSpieleAmAnfang > 0)
        {
            liste.Add(PlanKennzahl(PlanKostenart.VereinsinterneSpieleAmAnfang, b.VereinsinterneSpieleAmAnfang, gesamt, optionen));
        }

        return liste;
    }

    /// <summary>Die Kostentabelle je Mannschaft und Kostenart mit Summen und Gewichtungsmarken.</summary>
    /// <param name="b">Die Bewertung.</param>
    /// <param name="optionen">Die Optionen (Gewichtungsmarken).</param>
    /// <param name="staffel">Die Staffel (IDs der Mannschaften).</param>
    /// <returns>Die Tabelle.</returns>
    internal static Kostentabelle Tabelle(Planbewertung b, Berechnungsoptionen optionen, Staffel staffel)
    {
        double gesamt = b.Gesamtkosten;
        IReadOnlyList<MannschaftsKostenart> arten = b.SichtbareKostenarten;
        var ids = staffel.Mannschaften.GroupBy(m => m.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

        var zeilen = new List<Kostenzeile>();
        foreach (Mannschaftsbewertung m in b.Mannschaften)
        {
            string id = ids.GetValueOrDefault(m.Name, m.Name);
            var mannschaft = new MannschaftsGewichtungsziel(id, m.Name);
            Gewichtung mannschaftGesamt = mannschaft.Lesen(optionen);
            List<Kostenfeld> zellen = arten.Select(art =>
            {
                var ziel = new MannschaftsKostenartGewichtungsziel(id, m.Name, art);
                Kostenzelle zelle = m.JeKostenart[(int)art];
                int wert = Gewichtungsanzeige.Wert(optionen.Fuer(art), mannschaftGesamt, ziel.Lesen(optionen));
                return new Kostenfeld(zelle.Anzeige(), Gewichtungsanzeige.Markierung(wert), Anteil(zelle.Kosten, gesamt), ziel);
            }).ToList();
            zeilen.Add(new Kostenzeile(
                new Kostenfeld(m.Name, string.Empty, 0, mannschaft),
                zellen,
                new Kostenfeld(Kostenanzeige.Kurz(m.Gesamt), Gewichtungsanzeige.Markierung(Gewichtungsanzeige.Wert(mannschaftGesamt)), Anteil(m.Gesamt, gesamt), mannschaft),
                string.Join(Environment.NewLine, m.Meldungen)));
        }

        List<Kostenfeld> spalten = arten.Select(art => new Kostenfeld(Gewichtungsanzeige.Name(art), string.Empty, 0, new KostenartGewichtungsziel(art))).ToList();
        List<Kostenfeld> summen = arten.Select(art =>
        {
            double summe = b.Mannschaften.Sum(m => m.JeKostenart[(int)art].Kosten);
            string marke = Gewichtungsanzeige.Markierung(Gewichtungsanzeige.Wert(optionen.Fuer(art)));
            return new Kostenfeld(Kostenanzeige.Kurz(summe), marke, Anteil(summe, gesamt), new KostenartGewichtungsziel(art));
        }).ToList();
        double alle = b.Mannschaften.Sum(m => m.Gesamt);
        return new Kostentabelle(spalten, zeilen, summen, Feld(alle, gesamt));
    }

    private static void WennKosten(List<Kennzahl> liste, string name, double kosten, double gesamt)
    {
        if (kosten > 0)
        {
            liste.Add(new Kennzahl(name, Feld(kosten, gesamt)));
        }
    }

    private static Kostenfeld Feld(double kosten, double gesamt) =>
        new(Kostenanzeige.Kurz(kosten), string.Empty, Anteil(kosten, gesamt), null);

    private static double Anteil(double kosten, double gesamt) => gesamt > 0 ? Math.Clamp(kosten / gesamt, 0, 1) : 0;

    private static Kennzahl PlanKennzahl(PlanKostenart art, double kosten, double gesamt, Berechnungsoptionen optionen)
    {
        var ziel = new PlanGewichtungsziel(art);
        string marke = Gewichtungsanzeige.Markierung(Gewichtungsanzeige.Wert(ziel.Lesen(optionen)));
        return new Kennzahl(Gewichtungsanzeige.Name(art), new Kostenfeld(Kostenanzeige.Kurz(kosten), marke, Anteil(kosten, gesamt), ziel));
    }
}
