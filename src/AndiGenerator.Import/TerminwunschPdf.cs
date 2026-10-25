// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Terminwuensche;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace AndiGenerator.Import;

/// <summary>
/// Liest den Bericht „Terminwünsche“ aus click-TT als PDF: PdfPig liefert die Wörter mit Lage, daraus entstehen Zeilen,
/// die <see cref="TerminwunschBericht"/> auswertet. Einziger Ort, an dem die PDF-Bibliothek verwendet wird.
/// </summary>
public static class TerminwunschPdf
{
    /// <summary>Wörter, deren Grundlinie weniger als dieser Abstand trennt, gehören zu einer Zeile.</summary>
    private const double Zeilentoleranz = 2.0;

    /// <summary>Liest die Meldungen aus einer PDF-Datei.</summary>
    /// <param name="pfad">Pfad der PDF-Datei.</param>
    /// <returns>Die Meldungen.</returns>
    /// <exception cref="FormatException">Die Datei ist kein lesbares PDF oder kein Bericht „Terminwünsche“.</exception>
    /// <exception cref="IOException">Die Datei kann nicht gelesen werden.</exception>
    public static IReadOnlyList<Vereinsmeldung> Laden(string pfad) => TerminwunschBericht.Lesen(Textzeilen(pfad));

    /// <summary>Liest die Textzeilen aller Seiten (oben nach unten, links nach rechts).</summary>
    /// <param name="pfad">Pfad der PDF-Datei.</param>
    /// <returns>Die Zeilen.</returns>
    /// <exception cref="FormatException">Die Datei ist kein lesbares PDF.</exception>
    /// <exception cref="FileNotFoundException">Die Datei gibt es nicht (PdfPig meldet sonst einen ungeprüften Fehler).</exception>
    public static IReadOnlyList<Textzeile> Textzeilen(string pfad)
    {
        if (!File.Exists(pfad))
        {
            throw new FileNotFoundException("Die Datei „" + pfad + "“ gibt es nicht.", pfad);
        }

        try
        {
            using PdfDocument dokument = PdfDocument.Open(pfad);
            var zeilen = new List<Textzeile>();
            int nummer = 0;
            foreach (Page seite in dokument.GetPages())
            {
                zeilen.AddRange(Seitenzeilen(nummer, seite));
                nummer++;
            }

            return zeilen;
        }
        catch (PdfDocumentFormatException ex)
        {
            throw new FormatException("Die Datei ist kein lesbares PDF: " + ex.Message, ex);
        }
    }

    private static List<Textzeile> Seitenzeilen(int nummer, Page seite)
    {
        // PdfPig misst von unten. Zeilen ergeben sich aus der Grundlinie der Buchstaben – die Oberkante hängt vom Zeichen ab
        // („-“ und „,“ liegen tiefer). Manche Berichte zeichnen Text doppelt (fett), solche Wörter zählen einmal.
        var woerter = seite.GetWords()
            .Where(w => !string.IsNullOrWhiteSpace(w.Text) && w.Letters.Count > 0)
            .Select(w => (Oben: seite.Height - w.Letters[0].StartBaseLine.Y, Links: w.BoundingBox.Left, w.Text))
            .DistinctBy(w => (Math.Round(w.Oben), Math.Round(w.Links), w.Text))
            .OrderBy(w => w.Oben)
            .ThenBy(w => w.Links)
            .ToList();

        var zeilen = new List<Textzeile>();
        var aktuell = new List<(double Oben, double Links, string Text)>();
        foreach (var wort in woerter)
        {
            if (aktuell.Count > 0 && wort.Oben - aktuell[0].Oben > Zeilentoleranz)
            {
                zeilen.Add(Zeile(nummer, aktuell));
                aktuell = [];
            }

            aktuell.Add(wort);
        }

        if (aktuell.Count > 0)
        {
            zeilen.Add(Zeile(nummer, aktuell));
        }

        return zeilen;
    }

    private static Textzeile Zeile(int seite, List<(double Oben, double Links, string Text)> woerter) =>
        new(seite, woerter[0].Oben, woerter.OrderBy(w => w.Links).Select(w => new Textwort(w.Links, w.Text)).ToList());
}
