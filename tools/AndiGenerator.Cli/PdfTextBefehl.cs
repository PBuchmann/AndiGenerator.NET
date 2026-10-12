// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Import;
using AndiGenerator.Persistence.Terminwuensche;

namespace AndiGenerator.Cli;

/// <summary><c>andigen pdftext</c>: zeigt die Textzeilen eines PDFs, wie der Import sie sieht (zur Fehlersuche).</summary>
internal static class PdfTextBefehl
{
    public static int Ausfuehren(string pdf, string? ausgabe)
    {
        IReadOnlyList<Textzeile> zeilen = TerminwunschPdf.Textzeilen(pdf);
        var text = new System.Text.StringBuilder();
        foreach (Textzeile zeile in zeilen)
        {
            text.Append(CultureInfo.InvariantCulture, $"{zeile.Seite};{zeile.Oben:0.0};");
            text.AppendJoin(" | ", zeile.Woerter.Select(w => string.Create(CultureInfo.InvariantCulture, $"{w.Links:0.0}:{w.Text}")));
            text.AppendLine();
        }

        if (ausgabe is null)
        {
            Console.Write(text.ToString());
        }
        else
        {
            File.WriteAllText(ausgabe, text.ToString());
            Console.WriteLine($"{zeilen.Count} Zeilen nach {ausgabe} geschrieben.");
        }

        return 0;
    }
}
