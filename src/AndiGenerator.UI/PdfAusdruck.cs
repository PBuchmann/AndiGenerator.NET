// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Rendering;
using SkiaSharp;

namespace AndiGenerator.UI;

/// <summary>Schreibt einen Ausdruck als PDF-Datei (Vektorgrafik, Schriften eingebettet).</summary>
internal static class PdfAusdruck
{
    /// <summary>Umbricht den Ausdruck mit den Maßen der Skia-Schrift und schreibt die Seiten.</summary>
    /// <param name="pfad">Pfad der PDF-Datei.</param>
    /// <param name="dokument">Der Ausdruck.</param>
    /// <param name="format">Das Seitenformat.</param>
    public static void Schreiben(string pfad, Druckdokument dokument, Seitenformat format)
    {
        IReadOnlyList<Druckseite> seiten;
        using (var messung = new SkiaZeichenflaeche(null))
        {
            seiten = Seitenumbruch.Umbrechen(dokument, format, messung);
        }

        using FileStream datei = File.Create(pfad);
        using SKDocument pdf = SKDocument.CreatePdf(datei);
        for (int i = 0; i < seiten.Count; i++)
        {
            SKCanvas leinwand = pdf.BeginPage((float)format.Breite, (float)format.Hoehe);
            using (var flaeche = new SkiaZeichenflaeche(leinwand))
            {
                seiten[i].Zeichnen(flaeche, dokument, format, i + 1, seiten.Count);
            }

            pdf.EndPage();
        }

        pdf.Close();
    }
}
