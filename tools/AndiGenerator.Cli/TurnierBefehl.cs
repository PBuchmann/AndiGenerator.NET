// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Domain.Turniere;
using AndiGenerator.Engine.Turniere;
using AndiGenerator.Persistence.Turniere;

namespace AndiGenerator.Cli;

/// <summary>
/// <c>andigen turnier pruefen</c>: erkennt in einem Plan aus click-TT die Turniertage (3er und 4er) und prüft sie gegen
/// die Meldungen der Mannschaften aus einer Umfrage (CSV).
/// </summary>
internal static class TurnierBefehl
{
    public const string Hilfe = """
        Aufruf: andigen turnier pruefen <click-TT-Exportdatei.xml> <Meldungen.csv> [Optionen]
          Meldungen: CSV einer Umfrage (z. B. Google Formulare) mit Spalten Mannschaft, je Sonntag „TT.MM.JJJJ“
                     (möglicher Heimspieltag / möglicher Auswärtsspieltag / können nicht), Halle, parallel, Bemerkungen
          --ausgabe DATEI       Bildschirmausgabe zusätzlich in diese Datei schreiben
        Geprüft werden: Form der Turniere, jede Paarung genau einmal, ein Turnier je Mannschaft und Tag, Ausrichter kann
        zuhause (beim 4er mit zwei Spielen parallel), die übrigen können auswärts, jede Mannschaft richtet aus
        (hat Vorrang vor den Meldungen). Es zählen nur die Meldungen, nicht die Sperrtermine aus click-TT.
        Für Turniere mit Befund werden passende Ausweichtage vorgeschlagen.
        Einzelbegegnungen (bei gerader Mannschaftszahl) werden auf Heim/Auswärts geprüft, höchstens eine je Mannschaft.
        """;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    public static async Task<int> Ausfuehren(IReadOnlyList<string> args)
    {
        if (args.Count < 3 || args[0] != "pruefen")
        {
            Console.WriteLine(Hilfe);
            return 1;
        }

        string? ausgabeDatei = null;
        int i = 3;
        while (i < args.Count)
        {
            if (args[i] != "--ausgabe" || i + 1 >= args.Count)
            {
                await Console.Error.WriteLineAsync($"Unbekannte Angabe: {args[i]}");
                Console.WriteLine(Hilfe);
                return 1;
            }

            ausgabeDatei = args[i + 1];
            i += 2;
        }

        TextWriter konsole = Console.Out;
        await using StreamWriter? ausgabe = ausgabeDatei is null ? null : new StreamWriter(ausgabeDatei, append: false);
        if (ausgabe is not null)
        {
            Console.SetOut(new DoppelSchreiber(konsole, ausgabe));
        }

        try
        {
            return Pruefen(args[1], args[2]);
        }
        finally
        {
            Console.SetOut(konsole);
        }
    }

    private static int Pruefen(string xml, string csv)
    {
        Staffel staffel = OptimierenBefehl.StaffelLaden(xml, leer: false);
        IReadOnlyList<Turniermeldung> meldungen = TurniermeldungenCsv.Laden(csv);
        List<string> mannschaften = staffel.Mannschaften.Select(m => m.Name).ToList();
        IReadOnlyList<Turnier> turniere = Turniererkennung.Erkennen(staffel.BestehenderSpielplan);
        Turnierpruefungsergebnis ergebnis = Turnierpruefung.Pruefen(turniere, mannschaften, meldungen);

        Console.WriteLine($"Staffel:      {staffel.Name} ({mannschaften.Count} Mannschaften)");
        Console.WriteLine($"Meldungen:    {meldungen.Count} aus {csv}");
        Console.WriteLine($"Turniere:     {turniere.Count(t => t.Form == Turnierform.Dreier)} × 3er, {turniere.Count(t => t.Form == Turnierform.Vierer)} × 4er, "
            + $"{turniere.Count(t => t.Form == Turnierform.Einzel)} Einzelbegegnungen"
            + $", {turniere.Count(t => t.Form == Turnierform.Unbekannt)} ohne bekannte Form; {turniere.Sum(t => t.Spiele.Count)} Spiele");
        Console.WriteLine();

        foreach (Turnierbewertung bewertung in ergebnis.Turniere)
        {
            Turnier t = bewertung.Turnier;
            string halle = bewertung.Ausrichter is { } a && Turnierpruefung.Meldung(a, meldungen) is { Halle.Length: > 0 } m ? $" ({m.Halle})" : string.Empty;
            string rolle = t.Form == Turnierform.Einzel ? "Heim" : "Ausrichter";
            Console.WriteLine($"{t.Tag.ToString("ddd dd.MM.yyyy", Deutsch)}  {Formname(t.Form),-6}  {rolle}: {bewertung.Ausrichter ?? "–"}{halle}");
            foreach (Spiel spiel in t.Spiele)
            {
                Console.WriteLine($"    {spiel.Zeitpunkt!.Value.ToString("HH:mm", CultureInfo.InvariantCulture)}  {spiel.Heim} – {spiel.Gast}");
            }

            foreach (string hinweis in bewertung.Hinweise)
            {
                Console.WriteLine($"  ! {hinweis}");
            }

            if (bewertung.Ausweichtage.Count > 0)
            {
                Console.WriteLine($"  → passende Ausweichtage: {string.Join(", ", bewertung.Ausweichtage.Select(d => d.ToString("ddd dd.MM.", Deutsch)))}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Ausgerichtet:");
        foreach (string name in mannschaften)
        {
            Console.WriteLine($"  {ergebnis.Ausgerichtet[name]}×  {name}");
        }

        Bemerkungen(mannschaften, meldungen);
        Console.WriteLine();
        if (ergebnis.Befunde.Count == 0)
        {
            Console.WriteLine("Keine Befunde: Der Plan passt zu allen Meldungen.");
            return 0;
        }

        Console.WriteLine($"Befunde ({ergebnis.Befunde.Count}):");
        foreach (string befund in ergebnis.Befunde)
        {
            Console.WriteLine($"  - {befund}");
        }

        return 3;
    }

    /// <summary>Freitext der Meldungen (z. B. Zeitwünsche), den die Prüfung nicht auswerten kann.</summary>
    private static void Bemerkungen(List<string> mannschaften, IReadOnlyList<Turniermeldung> meldungen)
    {
        var mitBemerkung = mannschaften.Select(n => (Name: n, Meldung: Turnierpruefung.Meldung(n, meldungen)))
            .Where(x => x.Meldung is { Bemerkung.Length: > 0 })
            .ToList();
        if (mitBemerkung.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Bemerkungen (bitte selbst prüfen):");
        foreach ((string name, Turniermeldung? meldung) in mitBemerkung)
        {
            Console.WriteLine($"  {name}: {meldung!.Bemerkung.ReplaceLineEndings(" ")}");
        }
    }

    private static string Formname(Turnierform form) => form switch
    {
        Turnierform.Dreier => "3er",
        Turnierform.Vierer => "4er",
        Turnierform.Einzel => "Einzel",
        _ => "?",
    };
}
