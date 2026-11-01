// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Einfach;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Cli;

/// <summary>
/// <c>andigen einfachreihe</c>: der einfache Modus vollautomatisch über mehrere Klassen, so wie ein Klassenleiter ihn nutzt
/// (Peter, 28.10.2026: ein bis zwei Minuten müssen reichen, gern mit allen Kernen). Je Klasse ein eigener Prozess (<c>andigen einfach</c>, auf
/// Wunsch zum Vergleich der Automodus mit HiGHS gleich lang); danach werden alle Pläne gleich bewertet – Pflicht, Wünsche und
/// die Güte nach dem Standard-Maßstab (je Mannschaft, damit beide Verfahren vergleichbar sind), auch die Zwischenstände.
/// </summary>
internal static partial class EinfachreiheBefehl
{
    public const string Hilfe = """
        Aufruf: andigen einfachreihe <Ordner oder click-TT-Dateien …> [Optionen]
          --sekunden N      Laufzeit je Klasse (Standard 120)
          --kerne N         Rechen-Threads je Klasse (Standard: Prozessorkerne - 1)
          --zwischen S,…    Zwischenstände des einfachen Modus (Standard 60)
          --nur P,P,…       aus einem Ordner nur die Klassen, deren Name mit P_ beginnt (z. B. R06,R15,R21)
          --vergleich       zusätzlich der Automodus mit HiGHS, gleich lang (doppelte Laufzeit)
          --gleichzeitig N  so viele Klassen gleichzeitig (Standard 1; nur mit genug Kernen, z. B. 2 × 4 auf 12)
          --optionen DATEI  Optionsdatei (.options); "auto" = Staffelordner des Originals; Standard: Standardoptionen
          --ziel ORDNER     Pläne, Protokolle und Tabelle (Standard: Unterordner „einfachreihe“ neben den Eingaben)
          --ausgabe DATEI   Bildschirmausgabe zusätzlich in diese Datei schreiben
        Eine .modifications-Datei neben der XML-Datei wird berücksichtigt. Ergebnis: einfachreihe.csv im Zielordner.
        """;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    public static async Task<int> Ausfuehren(IReadOnlyList<string> args)
    {
        var dateien = new List<string>();
        var werte = new Dictionary<string, string>(StringComparer.Ordinal);
        int i = 0;
        while (i < args.Count)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                dateien.AddRange(Directory.Exists(args[i])
                    ? Directory.GetFiles(args[i], "*.xml").Order(StringComparer.OrdinalIgnoreCase)
                    : [args[i]]);
                i++;
                continue;
            }

            bool mitWert = i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            werte[args[i]] = mitWert ? args[i + 1] : "ja";
            i += mitWert ? 2 : 1;
        }

        if (werte.TryGetValue("--nur", out string? nur))
        {
            string[] praefixe = nur.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            dateien = [.. dateien.Where(d => Array.Exists(praefixe, p => Path.GetFileName(d).StartsWith(p + "_", StringComparison.OrdinalIgnoreCase)))];
        }

        if (dateien.Count == 0 || werte.ContainsKey("--hilfe"))
        {
            Console.WriteLine(Hilfe);
            return 1;
        }

        TextWriter konsole = Console.Out;
        await using StreamWriter? ausgabe = werte.TryGetValue("--ausgabe", out string? aus) ? new StreamWriter(aus, append: false) : null;
        if (ausgabe is not null)
        {
            Console.SetOut(new DoppelSchreiber(konsole, ausgabe));
        }

        try
        {
            return await Reihe(dateien, werte);
        }
        finally
        {
            Console.SetOut(konsole);
        }
    }

    private static async Task<int> Reihe(List<string> dateien, Dictionary<string, string> werte)
    {
        int Zahl(string name, int standard) => werte.TryGetValue(name, out string? text) ? int.Parse(text, CultureInfo.InvariantCulture) : standard;
        int sekunden = Zahl("--sekunden", 120);
        int kerne = Zahl("--kerne", Math.Max(1, Environment.ProcessorCount - 1));
        int gleichzeitig = Zahl("--gleichzeitig", 1);
        bool vergleich = werte.ContainsKey("--vergleich");
        int[] zwischen = [.. werte.GetValueOrDefault("--zwischen", "60").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => int.Parse(t, CultureInfo.InvariantCulture)).Where(t => t < sekunden)];
        string ziel = werte.GetValueOrDefault("--ziel") ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dateien[0])) ?? ".", "einfachreihe");
        Directory.CreateDirectory(ziel);
        string? optionen = werte.GetValueOrDefault("--optionen");

        Console.WriteLine($"Einfacher Modus über {dateien.Count} Klassen: je {sekunden} s, {kerne} Kerne{(vergleich ? ", dazu der Automodus mit HiGHS" : string.Empty)}, {gleichzeitig} gleichzeitig");
        Console.WriteLine($"Ziel: {ziel}");
        Console.WriteLine($"Voraussichtlich fertig nach gut {Math.Ceiling(dateien.Count * sekunden * (vergleich ? 2 : 1) / (60.0 * gleichzeitig)):0} min.");
        Console.WriteLine();

        var zeilen = new List<Ergebniszeile>();
        var sperre = new Lock();
        var uhr = Stopwatch.StartNew();
        int fertig = 0;
        await Parallel.ForEachAsync(dateien, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, gleichzeitig) }, async (datei, abbruch) =>
        {
            string name = Path.GetFileNameWithoutExtension(datei);
            List<Ergebniszeile> klasse = await KlasseRechnen(datei, name, ziel, sekunden, kerne, zwischen, vergleich, optionen, abbruch);
            lock (sperre)
            {
                zeilen.AddRange(klasse);
                fertig++;
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"[{uhr.Elapsed.TotalMinutes,5:0.0} min] {fertig}/{dateien.Count} {name}"));
                foreach (Ergebniszeile z in klasse)
                {
                    Console.WriteLine($"    {z.Verfahren,-10} {z.Sekunden,4} s  {z.Kurz()}");
                }
            }
        });

        List<Ergebniszeile> sortiert = [.. zeilen.OrderBy(z => z.Klasse, StringComparer.OrdinalIgnoreCase).ThenBy(z => z.Verfahren, StringComparer.Ordinal).ThenBy(z => z.Sekunden)];
        string csv = Path.Combine(ziel, "einfachreihe.csv");
        await File.WriteAllLinesAsync(csv, [Ergebniszeile.Kopf, .. sortiert.Select(z => z.Csv())], new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        Console.WriteLine();
        Zusammenfassen(sortiert, sekunden, zwischen, vergleich);
        Console.WriteLine();
        Console.WriteLine($"Tabelle: {csv}");
        return 0;
    }

    /// <summary>Eine Klasse: der einfache Modus (und auf Wunsch der Automodus) als eigene Prozesse, dann alle Pläne bewerten.</summary>
    private static async Task<List<Ergebniszeile>> KlasseRechnen(string datei, string name, string ziel, int sekunden, int kerne, int[] zwischen, bool vergleich, string? optionen, CancellationToken abbruch)
    {
        string einfachPlan = Path.Combine(ziel, name + " einfach.xml");
        string einfachLog = Path.Combine(ziel, name + " einfach.log");
        List<string> einfach = ["einfach", datei, "--sekunden", Text(sekunden), "--kerne", Text(kerne), "--plan", einfachPlan, "--ausgabe", einfachLog];
        if (zwischen.Length > 0)
        {
            einfach.AddRange(["--zwischen", string.Join(",", zwischen.Select(Text))]);
        }

        string autoPlan = Path.Combine(ziel, name + " automodus.xml");
        List<string> auto = ["optimieren", datei, "--leer", "--highs", "--nebenher", "--neuplanen", "--sekunden", Text(sekunden), "--kerne", Text(kerne), "--plan", autoPlan, "--ausgabe", Path.Combine(ziel, name + " automodus.log")];
        if (optionen is not null)
        {
            einfach.AddRange(["--optionen", optionen]);
            auto.AddRange(["--optionen", optionen]);
        }

        foreach (string alt in new[] { einfachPlan, autoPlan }.Concat(zwischen.Select(z => EinfachBefehl.Zwischendatei(einfachPlan, z))))
        {
            File.Delete(alt);
        }

        await Starten(einfach, sekunden, abbruch);
        if (vergleich)
        {
            await Starten(auto, sekunden, abbruch);
        }

        // Alle Pläne gleich bewerten.
        Staffel staffel = OptimierenBefehl.StaffelLaden(datei, leer: true);
        (Berechnungsoptionen opt, _) = OptimierenBefehl.OptionenLaden(optionen, staffel);
        double? abFertig = File.Exists(einfachLog) && AbFertigMuster().Match(await File.ReadAllTextAsync(einfachLog, abbruch)) is { Success: true } m
            ? double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)
            : null;
        var zeilen = new List<Ergebniszeile>();
        void Bewerten(string verfahren, int nach, string plan)
        {
            if (!File.Exists(plan) || BewertenBefehl.PlanLaden(staffel, plan) is not { } spiele)
            {
                return;
            }

            Planguete g = Planguete.Bewerten(Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = spiele }, opt));
            zeilen.Add(new Ergebniszeile(name, staffel.Mannschaften.Count, verfahren, nach, g, verfahren == "einfach" ? abFertig : null, Serien(spiele)));
        }

        foreach (int z in zwischen)
        {
            Bewerten("einfach", z, EinfachBefehl.Zwischendatei(einfachPlan, z));
        }

        Bewerten("einfach", sekunden, einfachPlan);
        Bewerten("automodus", sekunden, autoPlan);
        return zeilen;
    }

    /// <summary>Ein Lauf als eigener Prozess (dasselbe Programm), Ausgabe verworfen (sie steht im Protokoll).</summary>
    private static async Task Starten(List<string> argumente, int sekunden, CancellationToken abbruch)
    {
        string programm = Environment.ProcessPath ?? "andigen";
        var start = new ProcessStartInfo(programm)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };

        // Unter „dotnet andigen.dll“ (Linux, Tests) läuft dotnet selbst – dann die DLL voranstellen.
        if (Path.GetFileNameWithoutExtension(programm).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "andigen.dll"));
        }

        foreach (string a in argumente)
        {
            start.ArgumentList.Add(a);
        }

        using Process prozess = Process.Start(start) ?? throw new IOException("andigen ließ sich nicht starten.");
        prozess.OutputDataReceived += (_, _) => { };
        prozess.ErrorDataReceived += (_, _) => { };
        prozess.BeginOutputReadLine();
        prozess.BeginErrorReadLine();
        using var zeit = CancellationTokenSource.CreateLinkedTokenSource(abbruch);
        zeit.CancelAfter(TimeSpan.FromSeconds(sekunden + 600));
        try
        {
            await prozess.WaitForExitAsync(zeit.Token);
        }
        catch (OperationCanceledException)
        {
            prozess.Kill(entireProcessTree: true);
            Console.WriteLine($"Abgebrochen (zu lange): {string.Join(" ", argumente.Take(2))}");
        }
    }

    /// <summary>Je Klasse eine Zeile: Wünsche und Güte des einfachen Modus nach jeder Zeit, daneben der Automodus.</summary>
    private static void Zusammenfassen(List<Ergebniszeile> zeilen, int sekunden, int[] zwischen, bool vergleich)
    {
        int[] zeiten = [.. zwischen, sekunden];
        var kopf = new StringBuilder($"{"Klasse",-34} {"A/B fertig",10}");
        foreach (int t in zeiten)
        {
            kopf.Append(CultureInfo.InvariantCulture, $" {"nach " + Text(t) + " s",-18}");
        }

        if (vergleich)
        {
            kopf.Append(" Automodus");
        }

        Console.WriteLine("Je Zelle: Pflicht · Wünsche · Güte (Standard-Maßstab, kleiner ist besser)");
        Console.WriteLine(kopf.ToString());
        foreach (IGrouping<string, Ergebniszeile> klasse in zeilen.GroupBy(z => z.Klasse))
        {
            Ergebniszeile? Finden(string verfahren, int t) => klasse.FirstOrDefault(z => z.Verfahren == verfahren && z.Sekunden == t);
            double? ab = klasse.FirstOrDefault(z => z.AbFertig is not null)?.AbFertig;
            var zeile = new StringBuilder(string.Create(CultureInfo.InvariantCulture, $"{Kuerzen(klasse.Key),-34} {(ab is double a ? $"{a:0} s" : "–"),10}"));
            foreach (int t in zeiten)
            {
                zeile.Append(CultureInfo.InvariantCulture, $" {Finden("einfach", t)?.Zelle() ?? "–",-18}");
            }

            if (vergleich)
            {
                zeile.Append(' ').Append(Finden("automodus", sekunden)?.Zelle() ?? "–");
            }

            Console.WriteLine(zeile.ToString());
        }

        if (vergleich)
        {
            var paare = zeilen.GroupBy(z => z.Klasse)
                .Select(k => (E: k.FirstOrDefault(z => z.Verfahren == "einfach" && z.Sekunden == sekunden), A: k.FirstOrDefault(z => z.Verfahren == "automodus")))
                .Where(p => p.E is not null && p.A is not null)
                .ToList();
            int besser = paare.Count(p => p.E!.Guete.IstBesserAls(p.A!.Guete));
            int schlechter = paare.Count(p => p.A!.Guete.IstBesserAls(p.E!.Guete));
            Console.WriteLine();
            Console.WriteLine($"Einfacher Modus gegen Automodus (Pflicht, dann Wünsche, dann Güte): {besser} besser, {schlechter} schlechter, {paare.Count - besser - schlechter} gleich.");
        }
    }

    private static string Kuerzen(string name) => name.Length <= 34 ? name : name[..33] + "…";

    private static string Text(int zahl) => zahl.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"Pflicht und Wünsche fertig \(HiGHS\): ([0-9.]+) s")]
    private static partial Regex AbFertigMuster();

    /// <summary>Ein bewerteter Plan der Reihe.</summary>
    /// <summary>
    /// Heim- und Auswärtsserien je Mannschaft, gleich gemessen für jeden Plan: die Spiele nach Datum, mehrere Spiele derselben Art
    /// an einem Tag (Koppeltermin) als eines. Doppel = genau zwei in Folge, Serien = drei oder mehr, dazu die längste.
    /// </summary>
    private static (int Doppel, int SerienAb3, int Laengste) Serien(IReadOnlyList<Spiel> spiele)
    {
        int doppel = 0;
        int serien = 0;
        int laengste = 0;
        foreach (string mannschaft in spiele.SelectMany(s => new[] { s.Heim, s.Gast }).Distinct(StringComparer.Ordinal))
        {
            List<bool> folge = [.. spiele
                .Where(s => s.Zeitpunkt is not null && (s.Heim == mannschaft || s.Gast == mannschaft))
                .Select(s => (Tag: s.Zeitpunkt!.Value.Date, Heim: s.Heim == mannschaft))
                .Distinct()
                .OrderBy(s => s.Tag)
                .Select(s => s.Heim)];
            int i = 0;
            while (i < folge.Count)
            {
                int j = i;
                while (j + 1 < folge.Count && folge[j + 1] == folge[i])
                {
                    j++;
                }

                int laenge = j - i + 1;
                doppel += laenge == 2 ? 1 : 0;
                serien += laenge >= 3 ? 1 : 0;
                laengste = Math.Max(laengste, laenge);
                i = j + 1;
            }
        }

        return (doppel, serien, laengste);
    }

    private sealed record Ergebniszeile(string Klasse, int Mannschaften, string Verfahren, int Sekunden, Planguete Guete, double? AbFertig, (int Doppel, int SerienAb3, int Laengste) Folgen)
    {
        public static string Kopf => "Klasse;Mannschaften;Verfahren;Sekunden;Harte Fehler;Pflicht;Wünsche;Güte;"
            + string.Join(";", Massstab.Gruppen.Select(g => g.Name)) + ";A und B fertig (s);"
            + string.Join(";", Massstab.Gruppen.SelectMany(g => g.Kriterien).Select(Massstab.Kriteriumsname))
            + ";Doppel (2 in Folge);Serien ab 3;Längste Serie";

        public string Zelle() => string.Create(CultureInfo.InvariantCulture, $"{(Guete.Harte > 0 ? "H" + Text(Guete.Harte) + " " : string.Empty)}{Guete.A}·{Guete.Wuensche}·{Guete.Guete:0.00}");

        public string Kurz() => string.Create(
            CultureInfo.InvariantCulture,
            $"{Guete} │ {string.Join(" ", Enumerable.Range(0, Massstab.Gruppen.Count).Where(g => Guete.Gruppe(g) > 0).Select(g => $"{Massstab.Gruppen[g].Name} {Guete.Gruppe(g):0.00}"))}");

        public string Csv() => string.Join(
            ";",
            [
                Klasse,
                Text(Mannschaften),
                Verfahren,
                Text(Sekunden),
                Text(Guete.Harte),
                Text(Guete.A),
                Text(Guete.Wuensche),
                Guete.Guete.ToString("0.000", Deutsch),
                .. Enumerable.Range(0, Massstab.Gruppen.Count).Select(g => Guete.Gruppe(g).ToString("0.000", Deutsch)),
                AbFertig is double a ? a.ToString("0.0", Deutsch) : string.Empty,
                .. Massstab.Gruppen.SelectMany(g => g.Kriterien).Select(k => Text(Guete.Anzahl[(int)k])),
                Text(Folgen.Doppel),
                Text(Folgen.SerienAb3),
                Text(Folgen.Laengste),
            ]);
    }
}
