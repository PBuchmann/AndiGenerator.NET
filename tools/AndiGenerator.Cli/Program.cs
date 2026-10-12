// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Cli;
using AndiGenerator.Persistence.ClickTt;
using AndiGenerator.Persistence.Plandaten;

// Kommandozeile für Tests, Benchmarks und Läufe ohne Oberfläche (MIGRATIONSPLAN E5).
Console.OutputEncoding = System.Text.Encoding.UTF8;
try
{
    if (args.Length >= 2 && args[0] == "optimieren")
    {
        return await OptimierenBefehl.Ausfuehren(args[1..]);
    }

    if (args.Length >= 1 && args[0] == "bewerten")
    {
        return await BewertenBefehl.Ausfuehren(args[1..]);
    }

    if (args.Length >= 1 && args[0] == "turnier")
    {
        return await TurnierBefehl.Ausfuehren(args[1..]);
    }

    if (args.Length is 2 or 3 && args[0] == "pdftext")
    {
        return PdfTextBefehl.Ausfuehren(args[1], args.Length == 3 ? args[2] : null);
    }

    if (args.Length == 2 && args[0] == "info")
    {
        return InfoBefehl.Ausfuehren(args[1]);
    }
}
catch (Exception ex) when (ex is ClickTtFormatException or PlanDatenFormatException or FormatException or IOException or UnauthorizedAccessException)
{
    await Console.Error.WriteLineAsync("Fehler: " + ex.Message);
    return 2;
}

Console.WriteLine("Aufruf: andigen info <click-TT-Exportdatei.xml>");
Console.WriteLine("        andigen optimieren <click-TT-Exportdatei.xml> [Optionen]   (Hilfe: andigen optimieren --hilfe)");
Console.WriteLine("        andigen bewerten <click-TT-Exportdatei.xml> <Plan> [<Plan> ...] [Optionen]   (Hilfe: andigen bewerten --hilfe)");
Console.WriteLine("        andigen turnier pruefen <click-TT-Exportdatei.xml> <Meldungen.csv> [--ausgabe DATEI]");
return 1;
