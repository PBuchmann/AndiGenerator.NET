// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Schranke;
using AndiGenerator.Import;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Cli;

/// <summary>
/// <c>andigen highsvergleich</c>: vergleicht Verfahren mit HiGHS – wie schnell findet welches einen Plan mit höchstens so vielen
/// A-Verstößen wie vorgegeben und möglichst wenigen B-Verstößen? Ohne Inseln, nur HiGHS.
/// </summary>
internal static class HighsvergleichBefehl
{
    public const string Hilfe = """
        Aufruf: andigen highsvergleich <click-TT-Exportdatei.xml> [Optionen]
          --sekunden N       Zeit je Verfahren (Standard 300)
          --ziel-a N|auto    höchstens so viele A-Verstöße (Standard auto: was HiGHS vorher in Stufe A findet, bis 2 min)
          --stufen TEXT      Einteilung statt der gespeicherten, z. B. "A:Hallenbelegung,ParalleleSpiele;B:Sperrtermine"
          --laeufe N         Läufe von HiGHS gleichzeitig (Standard: logische Prozessoren - 1)
          --verfahren LISTE  z. B. "kette,direkt,direkt:0.3,portfolio,teilneuplanung,kombination"
                             (Standard: alle; ":H" = Aufwand der Heuristiken von HiGHS, Standard 0.05)
                             kette          wie heute: A, dann B, je mind. 20 s, weiter solange es vorangeht (max. 3 min)
                             kettegeduldig  wie kette, aber Stufe A mind. 60 s, Ende erst nach 30 s ohne Fortschritt
                             kettelang      wie kettegeduldig, aber B rechnet bis zum Ende
                             direkt         A nach A1 gleich auf das Ziel begrenzt, ein Lauf für B bis zum Ende
                             portfolio      wie direkt, alle Läufe gleichzeitig (lang, ohne Neustart), verschiedene
                                            Startwerte und Heuristik 0.05 / 0.2 / 0.5 im Wechsel
                             teilneuplanung Stufe A als Start, dann Teilneuplanung auf allen Läufen
                             kombination    ein langer direkt-Lauf (meldet jede bessere Lösung sofort), gleichzeitig
                                            Stufe A und danach Teilneuplanung auf den übrigen Läufen
                             mehrfach       mehrere lange direkt-Läufe (Linien), gleichzeitig Stufe A; die übrigen
                                            Läufe verfeinern je eine Linie mit Teilneuplanung
          --intervall N      Zwischenstand alle N Sekunden (Standard 5)
          --optionen DATEI   Optionsdatei (.options); "auto" = Staffelordner des Originals; Standard: Standardoptionen
          --rundenplanung R  beide | halbrunde | vorrunde | rueckrunde (Standard: beide = die ganze Runde)
          --ausgabe DATEI    Bildschirmausgabe zusätzlich in diese Datei schreiben
          --plaene ORDNER    besten Plan je Verfahren als gemerkten Plan speichern (zum Prüfen mit "bewerten")
        Die Einteilung der Kriterien kommt aus der .modifications-Datei neben der XML-Datei (wie gespeichert).
        """;

    private static readonly int[] Marken = [10, 20, 30, 60, 120, 180, 300, 600, 900, 1200, 1800];

    private static readonly double[] Heuristiken = [0.05, 0.2, 0.5];

    public static int Ausfuehren(IReadOnlyList<string> args)
    {
        string? datei = null;
        string? optionenDatei = null;
        string? ausgabeDatei = null;
        string? planOrdner = null;
        Rundenplanung runde = Rundenplanung.Beide;
        int sekunden = 300;
        int? zielA = null;
        Stufeneinteilung? stufen = null;
        int intervall = 5;
        int laeufe = Math.Max(1, Environment.ProcessorCount - 1);
        string verfahrenText = "kette,direkt,portfolio,teilneuplanung,kombination,mehrfach";
        var werte = new Queue<string>(args);
        while (werte.Count > 0)
        {
            string arg = werte.Dequeue();
            string? wert = werte.Count > 0 ? werte.Peek() : null;
            bool zahl = int.TryParse(wert, CultureInfo.InvariantCulture, out int n) && n > 0;
            switch (arg)
            {
                case "--sekunden" when zahl:
                    sekunden = n;
                    break;
                case "--ziel-a" when wert is not null && int.TryParse(wert, CultureInfo.InvariantCulture, out int a) && a >= 0:
                    zielA = a;
                    break;
                case "--ziel-a" when wert == "auto":
                    zielA = null;
                    break;
                case "--stufen" when wert is not null && Stufeneinteilung.Lesen(wert) is Stufeneinteilung e:
                    stufen = e;
                    break;
                case "--laeufe" when zahl:
                    laeufe = n;
                    break;
                case "--intervall" when zahl:
                    intervall = n;
                    break;
                case "--verfahren" when wert is not null:
                    verfahrenText = wert;
                    break;
                case "--optionen" when wert is not null:
                    optionenDatei = wert;
                    break;
                case "--ausgabe" when wert is not null:
                    ausgabeDatei = wert;
                    break;
                case "--plaene" when wert is not null:
                    planOrdner = wert;
                    break;
                case "--rundenplanung" when wert is not null && Rundenwahl.Lesen(wert) is Rundenplanung r:
                    runde = r;
                    break;
                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal) || datei is not null)
                    {
                        Console.WriteLine(Hilfe);
                        return 1;
                    }

                    datei = arg;
                    continue;
            }

            werte.Dequeue();
        }

        List<(string Name, Highsverfahren Verfahren, double? Heuristik)>? liste = Verfahren(verfahrenText);
        if (datei is null || liste is null)
        {
            Console.WriteLine(Hilfe);
            return 1;
        }

        if (!HighsLoeser.Verfuegbar)
        {
            Console.Error.WriteLine("HiGHS ist auf diesem Rechner nicht verfügbar.");
            return 2;
        }

        TextWriter konsole = Console.Out;
        using StreamWriter? ausgabe = ausgabeDatei is null ? null : new StreamWriter(ausgabeDatei, append: false);
        if (ausgabe is not null)
        {
            Console.SetOut(new DoppelSchreiber(konsole, ausgabe));
        }

        try
        {
            Staffel staffel = OptimierenBefehl.StaffelLaden(datei, leer: true);
            (Berechnungsoptionen optionen, string herkunft) = OptimierenBefehl.OptionenLaden(optionenDatei, staffel);
            optionen = optionen with { Rundenplanung = runde };
            Stufeneinteilung einteilung = stufen ?? Stufeneinteilung.AusText(staffel.Kriterienstufen);
            Console.WriteLine($"Staffel:      {staffel.Name} ({staffel.Mannschaften.Count} Mannschaften)");
            Console.WriteLine($"Optionen:     {herkunft}, Rundenplanung {runde}");
            Console.WriteLine($"Einteilung:   {einteilung.Text()}" + (stufen is null ? " (gespeichert)" : " (vorgegeben)"));
            int ziel = zielA ?? ZielBestimmen(staffel, optionen, einteilung);
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Ziel:         A ≤ {ziel}{(zielA is null ? " (von HiGHS vorher bestimmt)" : string.Empty)}, dann möglichst wenige B-Verstöße; je Verfahren {sekunden} s, {laeufe} Läufe gleichzeitig ({Environment.ProcessorCount} logische Prozessoren)"));
            var ergebnisse = new List<Ergebnis>();
            foreach ((string name, Highsverfahren verfahren, double? heuristik) in liste)
            {
                ergebnisse.Add(Rechnen(staffel, optionen, einteilung, name, verfahren, heuristik, ziel, sekunden, laeufe, intervall, planOrdner));
            }

            Zusammenfassen(ergebnisse, ziel, sekunden);
            if (ergebnisse.Exists(e => e.Haengt))
            {
                // Hängende Läufe von HiGHS beenden.
                Console.Out.Flush();
                Environment.Exit(0);
            }

            return 0;
        }
        finally
        {
            Console.SetOut(konsole);
        }
    }

    /// <summary>Das Ziel für A: was HiGHS in Stufe A findet (wie „kettegeduldig“, höchstens 2 Minuten; ohne Zeitwertung).</summary>
    private static int ZielBestimmen(Staffel staffel, Berechnungsoptionen optionen, Stufeneinteilung einteilung)
    {
        Console.WriteLine("Ziel für A wird bestimmt (Stufe A mit HiGHS, höchstens 2 Minuten) …");
        int bester = int.MaxValue;
        var bestimmt = new CancellationTokenSource();
        Ligaschranke.Vergleichen(
            staffel,
            optionen,
            einteilung,
            Highsverfahren.KetteGeduldig,
            0,
            TimeSpan.FromMinutes(2),
            1,
            _ => new SchrankeBefehl.Highsschranke(),
            (_, w, herkunft) =>
            {
                if (herkunft == "Stufe A" && w.Harte == 0)
                {
                    bester = Math.Min(bester, w.A);
                    bestimmt.Cancel();
                }
            },
            _ => { },
            bestimmt.Token);
        bestimmt.Dispose();
        return bester == int.MaxValue ? 0 : bester;
    }

    private static List<(string Name, Highsverfahren Verfahren, double? Heuristik)>? Verfahren(string text)
    {
        var liste = new List<(string, Highsverfahren, double?)>();
        foreach (string teil in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] stuecke = teil.Split(':');
            Highsverfahren? v = stuecke[0].ToUpperInvariant() switch
            {
                "KETTE" => Highsverfahren.Kette,
                "KETTEGEDULDIG" => Highsverfahren.KetteGeduldig,
                "KETTELANG" => Highsverfahren.KetteLang,
                "DIREKT" => Highsverfahren.Direkt,
                "PORTFOLIO" => Highsverfahren.Portfolio,
                "TEILNEUPLANUNG" => Highsverfahren.Teilneuplanung,
                "KOMBINATION" => Highsverfahren.Kombination,
                "MEHRFACH" => Highsverfahren.Mehrfach,
                _ => null,
            };
            double? heuristik = null;
            if (stuecke.Length > 1 && double.TryParse(stuecke[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double h))
            {
                heuristik = h;
            }

            if (v is null || (stuecke.Length > 1 && heuristik is null))
            {
                return null;
            }

            liste.Add((teil, v.Value, heuristik));
        }

        return liste.Count > 0 ? liste : null;
    }

    private static Ergebnis Rechnen(Staffel staffel, Berechnungsoptionen optionen, Stufeneinteilung einteilung, string name, Highsverfahren verfahren, double? heuristik, int zielA, int sekunden, int laeufe, int intervall, string? planOrdner)
    {
        Console.WriteLine();
        Console.WriteLine($"================================ {name} ================================");
        var ergebnis = new Ergebnis(name);
        var sperre = new Lock();
        var modell = new Dictionary<int, (double Primal, double Dual)>();
        var uhr = Stopwatch.StartNew();
        IReadOnlyList<Spiel>? besterPlan = null;

        Highsvorgabe Vorgabe(int lauf)
        {
            void Merken(double primal, double dual)
            {
                lock (sperre)
                {
                    modell[lauf] = (primal, dual);
                }
            }

            double? h = heuristik ?? (verfahren is Highsverfahren.Portfolio or Highsverfahren.Kombination or Highsverfahren.Mehrfach ? Heuristiken[lauf % Heuristiken.Length] : null);
            return new Highsvorgabe(lauf + 1, h, lauf >= Ligaschranke.TeilneuplanungsLauf ? null : (_, p, d) => Merken(p, d), OhneNeustart: true);
        }

        void Plan(IReadOnlyList<Spiel> p, Stufenwert w, string herkunft)
        {
            double t = uhr.Elapsed.TotalSeconds;
            lock (sperre)
            {
                ergebnis.Verlauf.Add((t, w));
                besterPlan = p;
            }

            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {t,6:0.0} s  neuer bester Plan A {w.A} · B {w.B} · C {w.CAnzahl}{(w.Harte > 0 ? $" · harte Fehler {w.Harte}" : string.Empty)}  ({herkunft})"));
        }

        using var abbruch = new CancellationTokenSource();
        Task arbeit = Task.Run(() => Ligaschranke.Vergleichen(
            staffel,
            optionen,
            einteilung,
            verfahren,
            zielA,
            TimeSpan.FromSeconds(sekunden),
            laeufe,
            lauf => new SchrankeBefehl.Highsschranke(Vorgabe(lauf)),
            Plan,
            text => Console.WriteLine("  " + text),
            abbruch.Token));
        while (!arbeit.Wait(TimeSpan.FromSeconds(intervall)))
        {
            Zwischenstand(uhr.Elapsed.TotalSeconds, ergebnis, sperre, modell);
            if (uhr.Elapsed.TotalSeconds > sekunden + 60)
            {
                // HiGHS reagiert manchmal nicht auf das Ende: nicht warten, sondern das nächste Verfahren beginnen (der hängende
                // Lauf rechnet im Hintergrund weiter; am Ende beendet sich das Programm hart).
                abbruch.Cancel();
                Console.WriteLine("  HiGHS reagiert nicht auf das Ende – Verfahren abgebrochen, das Ergebnis gilt bis hier.");
                ergebnis.Haengt = true;
                break;
            }
        }

        Zwischenstand(uhr.Elapsed.TotalSeconds, ergebnis, sperre, modell);
        if (planOrdner is not null && besterPlan is not null)
        {
            Directory.CreateDirectory(planOrdner);
            string pfad = Path.Combine(planOrdner, $"highsvergleich-{name.Replace(':', '-')}.xml");
            GespeicherterPlanDatei.Speichern(pfad, besterPlan.Select(s => new GespeichertesSpiel(s.Zeitpunkt, s.Heim, s.Gast)));
            Console.WriteLine($"  Plan gespeichert: {pfad}");
        }

        return ergebnis;
    }

    private static void Zwischenstand(double t, Ergebnis ergebnis, Lock sperre, Dictionary<int, (double Primal, double Dual)> modell)
    {
        string text;
        lock (sperre)
        {
            string bester = ergebnis.Verlauf.Count == 0 ? "noch kein Plan" : Kurz(ergebnis.Verlauf[^1].Wert);
            string highs = modell.Count == 0
                ? string.Empty
                : string.Create(CultureInfo.InvariantCulture, $" | HiGHS-Modell ({modell.Count} Läufe): bestes Ziel {Zahl(modell.Values.Min(m => m.Primal))}, Grenze {Zahl(modell.Values.Max(m => m.Dual))}");
            text = string.Create(CultureInfo.InvariantCulture, $"  {t,6:0} s  bester {bester}{highs}");
        }

        Console.WriteLine(text);
    }

    private static void Zusammenfassen(List<Ergebnis> ergebnisse, int zielA, int sekunden)
    {
        int[] marken = [.. Marken.Where(m => m <= sekunden), sekunden];
        marken = [.. marken.Distinct()];
        Console.WriteLine();
        Console.WriteLine("================================ Zusammenfassung ================================");
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"B-Verstöße des besten Plans mit A ≤ {zielA} nach … Sekunden (– = noch keiner; am Ende A · B · C):"));
        Console.WriteLine($"{"Verfahren",-18} {"A≤Ziel nach",11} " + string.Join(" ", marken.Select(m => $"{m,5}")) + "   Ende (die letzte Spalte zählt auch, was HiGHS beim Aufhören noch lieferte)");
        foreach (Ergebnis e in ergebnisse)
        {
            bool Ziel((double Zeit, Stufenwert Wert) x) => x.Wert.Harte == 0 && x.Wert.A <= zielA;
            int erster = e.Verlauf.FindIndex(x => Ziel(x));
            string zeiten = string.Join(" ", marken.Select(m =>
            {
                int i = e.Verlauf.FindLastIndex(x => (x.Zeit <= m || m == sekunden) && Ziel(x));
                return i < 0 ? $"{"–",5}" : $"{e.Verlauf[i].Wert.B,5}";
            }));
            string ende = (e.Verlauf.Count == 0 ? "–" : Kurz(e.Verlauf[^1].Wert)) + (e.Haengt ? " (HiGHS hing)" : string.Empty);
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{e.Name,-18} {(erster >= 0 ? $"{e.Verlauf[erster].Zeit:0} s" : "–"),11} {zeiten}   {ende}"));
        }
    }

    private static string Kurz(Stufenwert w) => string.Create(CultureInfo.InvariantCulture, $"A {w.A} · B {w.B} · C {w.CAnzahl}{(w.Harte > 0 ? $" · H {w.Harte}" : string.Empty)}");

    private static string Zahl(double x) => double.IsFinite(x) ? x.ToString("0", CultureInfo.InvariantCulture) : "–";

    private sealed class Ergebnis(string name)
    {
        public string Name => name;

        public List<(double Zeit, Stufenwert Wert)> Verlauf { get; } = [];

        /// <summary>Holt oder setzt einen Wert, der angibt, ob HiGHS nicht auf das Ende reagierte.</summary>
        public bool Haengt { get; set; }
    }
}
