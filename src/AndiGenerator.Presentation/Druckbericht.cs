// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;
using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation;

/// <summary>
/// Baut die Abschnitte des Ausdrucks (Original <c>TFormPrintSelection</c>/<c>PlanPanel</c>) aus denselben Zeilen wie die
/// Ansichten. Überschriften mit den Tippfehlern des Originals („letzer“, „Nachbarmanschaften“) sind korrigiert.
/// </summary>
internal static class Druckbericht
{
    /// <summary>Schriftgröße für Text und Tabellen.</summary>
    internal const double Groesse = 9;

    private const double Einzug = 16;
    private const double Kachelbreite = 150;
    private const double Kachelhoehe = 40;
    private const double Kachelabstand = 8;
    private const int KachelnJeZeile = 4;

    /// <summary>
    /// Abschnitt „Terminwünsche“: das Raster der Wünsche mit Legende, dann die Hinweise und je Mannschaft ihre Wünsche
    /// und die Auswertung.
    /// </summary>
    /// <param name="uebersicht">Die Terminwünsche oder <c>null</c>.</param>
    /// <returns>Der Abschnitt.</returns>
    internal static Abschnitt Terminwuensche(Terminwunschuebersicht? uebersicht)
    {
        var bausteine = new List<Baustein>();
        if (uebersicht is Terminwunschuebersicht u && new Terminwunschzeichner(u, null, mitLegende: true).Zeichnen() is { Breite: > 0 } raster)
        {
            bausteine.Add(new Grafik(raster.Breite, raster.Hoehe, f => new Terminwunschzeichner(u, f, mitLegende: true).Zeichnen()));
        }

        bausteine.AddRange(Terminwunschzeile.Bilden(uebersicht)
            .Select(z => new Textzeile(z.Text, Groesse, Wunschfarbe(z.Farbe), z.Ebene < 2, z.Ebene * Einzug, z.Ebene == 0 ? Groesse : 0)));
        return new Abschnitt("Terminwünsche", OderHinweis(bausteine));
    }

    /// <summary>Abschnitt mit Terminzeilen (Spielplan, Mannschaftspläne, Termine der Nachbarmannschaften).</summary>
    /// <param name="titel">Der Titel.</param>
    /// <param name="zeilen">Die Zeilen der Ansicht.</param>
    /// <returns>Der Abschnitt.</returns>
    internal static Abschnitt Termine(string titel, IReadOnlyList<Terminzeile> zeilen)
    {
        List<Baustein> bausteine = zeilen.Count == 0 ? [] : [new Tabelle(null, zeilen.Select(Zeile).ToList(), Groesse, LetzteSpalteUmbrechen: true)];
        return new Abschnitt(titel, OderHinweis(bausteine));
    }

    /// <summary>
    /// Abschnitt „Kosten“: Kennzahlen als Kacheln, Kostentabelle je Mannschaft als Matrix (eingefärbt wie in der
    /// Ansicht) und die Meldungen der Mannschaften.
    /// </summary>
    /// <param name="kennzahlen">Die Kennzahlen.</param>
    /// <param name="tabelle">Die Kostentabelle.</param>
    /// <returns>Der Abschnitt.</returns>
    internal static Abschnitt Kosten(IReadOnlyList<Kennzahl> kennzahlen, Kostentabelle tabelle)
    {
        var bausteine = new List<Baustein>();
        if (kennzahlen.Count > 0)
        {
            int reihen = (kennzahlen.Count + KachelnJeZeile - 1) / KachelnJeZeile;
            double breite = (Math.Min(kennzahlen.Count, KachelnJeZeile) * (Kachelbreite + Kachelabstand)) - Kachelabstand;
            double hoehe = (reihen * (Kachelhoehe + Kachelabstand)) - Kachelabstand;
            bausteine.Add(new Grafik(breite, hoehe, f => Kacheln(f, kennzahlen)));
            bausteine.Add(new Abstand(Groesse * 2));
        }

        var kopf = new List<string> { "Mannschaft" };
        kopf.AddRange(tabelle.Spalten.Select(s => s.Text));
        kopf.Add("Gesamt");
        var zeilen = new List<Tabellenzeile>();
        foreach (Kostenzeile z in tabelle.Zeilen)
        {
            var zellen = new List<Tabellenzelle> { new(z.Mannschaft.Text) };
            zellen.AddRange(z.Zellen.Select(Zelle));
            zellen.Add(Zelle(z.Gesamt));
            zeilen.Add(new Tabellenzeile(zellen, Farbe.Tinte));
        }

        var summe = new List<Tabellenzelle> { new("Summe") };
        summe.AddRange(tabelle.Summen.Select(Zelle));
        summe.Add(Zelle(tabelle.Gesamtsumme));
        zeilen.Add(new Tabellenzeile(summe, Farbe.Tinte, Fett: true));
        bausteine.Add(new Tabelle(kopf, zeilen, Groesse, Matrix: true));

        foreach (Kostenzeile z in tabelle.Zeilen.Where(z => z.Meldungen.Length > 0))
        {
            bausteine.Add(new Textzeile(z.Mannschaft.Text, Groesse, Farbe.Tinte, Fett: true, AbstandDavor: Groesse));
            bausteine.AddRange(z.Meldungen.Split(Environment.NewLine).Select(m => new Textzeile(m, Groesse, Farbe.Tinte2, Einzug: Einzug)));
        }

        return new Abschnitt("Kosten", bausteine);
    }

    /// <summary>Abschnitt „Diagramme“: jedes Diagramm des Originals als eigene, bei Bedarf verkleinerte Grafik.</summary>
    /// <param name="daten">Die Diagrammdaten.</param>
    /// <returns>Der Abschnitt.</returns>
    internal static Abschnitt Diagramme(Diagrammdaten daten)
    {
        var bausteine = new List<Baustein>();
        foreach (Diagrammteil teil in Diagrammzeichner.Teile(daten))
        {
            Zeichengroesse groesse = new Diagrammzeichner(daten, null).Zeichnen(teil, 0);
            double abstand = bausteine.Count == 0 ? 0 : Groesse * 2;
            bausteine.Add(new Grafik(groesse.Breite, groesse.Hoehe, f => new Diagrammzeichner(daten, f).Zeichnen(teil, 0), abstand));
        }

        return new Abschnitt("Diagramme", bausteine.Count > 0 ? bausteine : [new Textzeile("Keine Spiele terminiert.", Groesse, Farbe.Grau)]);
    }

    /// <summary>
    /// Hinterlegung eines Kostenfelds wie in der Kostenansicht: von Blassrosa zu Ziegelrot, bereits ab 20 % der
    /// Gesamtkosten voll (Wurzel, damit auch kleine Anteile sichtbar werden).
    /// </summary>
    /// <param name="anteil">Anteil an den Gesamtkosten (0 … 1).</param>
    /// <returns>Die Farbe.</returns>
    internal static Farbe Hinterlegung(double anteil)
    {
        double f = Staerke(anteil);
        return new Farbe(Mischen(0xFD, 0xC9, f), Mischen(0xF1, 0x4A, f), Mischen(0xEF, 0x3C, f));
    }

    /// <summary>Schriftfarbe auf der <see cref="Hinterlegung(double)"/>: weiß auf kräftigem Rot, sonst Tinte.</summary>
    /// <param name="anteil">Anteil an den Gesamtkosten (0 … 1).</param>
    /// <returns>Die Farbe.</returns>
    internal static Farbe Hinterlegungsschrift(double anteil) => Staerke(anteil) > 0.55 ? Farbe.Weiss : Farbe.Tinte;

    private static double Staerke(double anteil) => Math.Min(1, Math.Sqrt(Math.Clamp(anteil, 0, 1) / 0.2));

    private static byte Mischen(byte von, byte bis, double f) => (byte)Math.Round(von + ((bis - von) * f));

    private static void Kacheln(IZeichenflaeche f, IReadOnlyList<Kennzahl> kennzahlen)
    {
        for (int i = 0; i < kennzahlen.Count; i++)
        {
            double x = (i % KachelnJeZeile) * (Kachelbreite + Kachelabstand);
            double y = (i / KachelnJeZeile) * (Kachelhoehe + Kachelabstand);
            Kennzahl k = kennzahlen[i];
            f.Rechteck(x, y, Kachelbreite, Kachelhoehe, Farbe.Flaeche);
            f.Rechteck(x, y, 2, Kachelhoehe, i == 0 ? Farbe.Akzent : Farbe.Rand);
            f.Text(k.Name, x + 9, y + 6, 7.5, Farbe.Grau, false);
            string wert = k.Wert.Markierung.Length > 0 ? k.Wert.Text + " " + k.Wert.Markierung : k.Wert.Text;
            f.Text(wert, x + 9, y + 18, 12, Farbe.Tinte, true);
        }
    }

    private static List<Baustein> OderHinweis(List<Baustein> bausteine) =>
        bausteine.Count > 0 ? bausteine : [new Textzeile("Keine Einträge.", Groesse, Farbe.Grau)];

    private static Tabellenzelle Zelle(Kostenfeld feld) => new(
        feld.Markierung.Length > 0 ? feld.Text + " " + feld.Markierung : feld.Text,
        feld.Anteil > 0 ? Hinterlegung(feld.Anteil) : null,
        feld.Anteil > 0 ? Hinterlegungsschrift(feld.Anteil) : null);

    private static Tabellenzeile Zeile(Terminzeile z) => z.Art switch
    {
        Planzeilenart.Abstand => Tabellenzeile.Leer,
        Planzeilenart.Ueberschrift => Tabellenzeile.Ueberschrift(z.Heim),
        _ => new Tabellenzeile([new(z.Datum), new(z.Zeit), new(z.Heim), new(z.Trenner), new(z.Gast), new(z.Hinweis, Farbe: Hinweisfarbe(z))], Zeilenfarbe(z.Art)),
    };

    /// <summary>Hinweise wie die Chips der Ansicht: rot bei Verstößen, ocker bei zu Beachtendem, sonst grau.</summary>
    private static Farbe? Hinweisfarbe(Terminzeile z)
    {
        if (z.Hinweis.Length == 0 || z.Art != Planzeilenart.Spiel)
        {
            return null;
        }

        IReadOnlyList<Hinweischip> chips = Hinweischip.Aus(z.Hinweis);
        if (chips.Any(c => c.Art == Hinweisart.Problem))
        {
            return Farbe.Schlecht;
        }

        return chips.Any(c => c.Art == Hinweisart.Warnung) ? Farbe.WarnungDunkel : Farbe.Grau;
    }

    private static Farbe Zeilenfarbe(Planzeilenart art) => art switch
    {
        Planzeilenart.Nachbarspiel => Farbe.Grau,
        Planzeilenart.EchtesNachbarspiel => Farbe.Akzent,
        _ => Farbe.Tinte,
    };

    private static Farbe Wunschfarbe(Terminwunschfarbe farbe) => farbe switch
    {
        Terminwunschfarbe.Gelb => Farbe.WarnungDunkel,
        Terminwunschfarbe.Rot => Farbe.Schlecht,
        Terminwunschfarbe.Gruen => Farbe.Gut,
        _ => Farbe.Tinte,
    };
}
