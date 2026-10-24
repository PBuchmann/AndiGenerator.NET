// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Kern;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Turniere;
using Nachbarspiel = AndiGenerator.Engine.Kern.Nachbarspiel;

namespace AndiGenerator.Engine.Schranke;

/// <summary>
/// Untere Schranke für die Stufe A (MIGRATIONSPLAN O7, Schritt 1; nicht im Original): Ein ganzzahliges Modell, in dem jedes
/// Spiel einen Wunschtermin seiner Heimmannschaft bekommt, wird nacheinander nach den Kriterien der Stufe A (Rangfolge E14)
/// minimiert. Das Modell ist eine <b>Lockerung</b> des echten Problems: Es lässt alles zu, was ein gültiger Plan darf (und etwas
/// mehr, z. B. Zweitzeiten von Koppelterminen ohne Partner), und zählt von jedem Verstoß höchstens so viel, wie die
/// Kostenfunktion sicher zählt (z. B. bei parallelen Spielen nur die direkten Fälle, nicht die weitergereichten). Darum hat
/// jeder gültige Plan mindestens die berechnete Zahl an Verstößen – ist sie 0, sagt die Schranke nichts.
/// </summary>
public sealed partial class Ligaschranke
{
    /// <summary>Zeitgrenze je Stufe der Kette in Oberfläche und Kommandozeile (auf den Referenzstaffeln reichen 0,1–10 s).</summary>
    public static readonly TimeSpan StandardZeitJeStufe = TimeSpan.FromSeconds(20);

    /// <summary>Ab so vielen logischen Prozessoren rechnet die Summe B früh mit (sie braucht einen Kern mehr).</summary>
    private const int FruehAb = 8;

    /// <summary>Höchstens so oft geht ein besserer Plan mitten in einem Schritt hinaus.</summary>
    private static readonly TimeSpan Meldeabstand = TimeSpan.FromSeconds(2);

    private static readonly double Hallenfenster = DelphiDatum.EncodeTime(1, 29);
    private static readonly double Parallelfenster = DelphiDatum.EncodeTime(3, 59);

    private readonly KernDefinition d;
    private readonly Ganzzahlmodell modell = new();

    /// <summary>Spalte, Spiel und Wunschtermin je Termin-Entscheidung („Spiel s am Wunschtermin w“).</summary>
    private readonly List<(int Spalte, int Spiel, int Wunsch)> termine = [];

    /// <summary>Termin-Entscheidungen (Index in <see cref="termine"/>) je Mannschaft, als Heim- oder Gastmannschaft.</summary>
    private readonly List<int>[] jeMannschaft;

    /// <summary>Termin-Entscheidungen je Wunschtermin.</summary>
    private readonly Dictionary<int, List<int>> jeWunsch = [];

    /// <summary>
    /// Spalte „ohne Termin“ mit dem Spiel, für das sie steht: je Spiel, bei einer Halbrunde je Paarung (die Richtung ist
    /// dort frei, es findet nur eines der beiden Spiele statt).
    /// </summary>
    private readonly List<(int Spalte, int Spiel)> ohneTermin = [];

    private readonly List<string> hinweise = [];

    /// <summary>Gekoppelte Paare je Mannschaft (siehe <see cref="KoppelPaare"/>).</summary>
    private readonly Dictionary<int, List<(int Spalte, int Tag1, int Tag2)>> koppelPaare = [];

    /// <summary>Feste Spiele außerhalb der geplanten Runde (nur Rückrunde: die Vorrunde aus dem bestehenden Plan).</summary>
    private readonly FesterTermin[] feste;

    /// <summary>Bricht die Rechnung ab (auch mitten in einem Lauf von HiGHS), oder <c>null</c>.</summary>
    private Func<bool>? aufhoeren;

    /// <summary>
    /// Die genauen Formen (Spieltage, Abstand Heim/Auswärts, Setzliste) zählen nur Spiele mit Termin: Für eine Schranke gelten sie
    /// erst, wenn die Spiele ohne Termin (A1) auf 0 festgehalten sind.
    /// </summary>
    private bool genauMoeglich = true;

    /// <summary>Liefert den besten Plan der Generierung als Startlösung (vor jeder Stufe neu gefragt), oder <c>null</c>.</summary>
    private Func<IReadOnlyList<Spiel>?>? startplan;

    /// <summary>Zeitgrenze nach Fortschritt für die Schritte von A und B (statt der festen je Stufe), oder <c>null</c>.</summary>
    private Laufzeitregel? laufzeit;

    private Ligaschranke(KernDefinition d)
    {
        this.d = d;
        feste = d.Rundenplanung == Rundenplanung.NurRueckrunde
            ? d.Bestehend.Where(f => f.Datum != 0.0 && !d.IstInZuPlanenderRunde(f.Datum)).ToArray()
            : [];
        jeMannschaft = Enumerable.Range(0, d.N).Select(_ => new List<int>()).ToArray();
        Grundmodell();
    }

    /// <summary>
    /// Berechnet die untere Schranke für eine Staffel: A1, die Summe der Stufe A, die der Stufe B, je Kriterium der Wert allein und
    /// – bei bewiesener Kette – die Stufe C mit dem Feinschliff; dazu den besten Plan des Modells.
    /// </summary>
    /// <param name="staffel">Die Staffel (ein bestehender Plan wird ignoriert).</param>
    /// <param name="optionen">Die Optionen.</param>
    /// <param name="loeser">Der Löser (HiGHS).</param>
    /// <param name="zeitJeStufe">
    /// Zeitgrenze je Stufe der Kette A → B (ist eine Stufe bewiesen, geht es sofort weiter) und der Werte „allein“.
    /// </param>
    /// <param name="einteilung">
    /// Rangfolge der Kriterien (Stufe A in dieser Reihenfolge); <c>null</c> = <see cref="Stufeneinteilung.Standard"/>. Kriterien
    /// der Stufe A, die das Modell nicht kennt, werden übersprungen (die Schranke der übrigen bleibt gültig).
    /// </param>
    /// <param name="fortschritt">Meldet, woran gerade gerechnet wird, mit dem bisher besten Plan; oder <c>null</c>.</param>
    /// <param name="zeitStufeC">
    /// Zeitgrenze für die Stufe C und den Feinschliff (sie nutzen sie meist ganz aus); <c>null</c> = <paramref name="zeitJeStufe"/>.
    /// </param>
    /// <param name="aufhoeren">Bricht die Rechnung ab (auch mitten in einem Lauf von HiGHS), oder <c>null</c>.</param>
    /// <param name="startplan">
    /// Liefert den besten Plan der Generierung (vor jeder Stufe neu gefragt), oder <c>null</c>. Hält er die festgehaltenen Stufen
    /// ein, beginnt HiGHS mit ihm: Das Ergebnis ist dann nie schlechter, und erreicht er die Schranke, ist die Stufe sofort
    /// bewiesen.
    /// </param>
    /// <param name="laufzeit">
    /// Zeitgrenze nach Fortschritt für die Schritte von A und B (A1, die Kriterien einzeln, die Summen, die B-Varianten, A und B
    /// zusammen): mindestens, solange es vorangeht, höchstens; <c>null</c> = fest <paramref name="zeitJeStufe"/>. Stufe C und
    /// Feinschliff bleiben bei ihrer festen Zeit.
    /// </param>
    /// <returns>Der Bericht.</returns>
    public static Schrankenbericht Berechnen(Staffel staffel, Berechnungsoptionen optionen, ISchrankenloeser loeser, TimeSpan zeitJeStufe, Stufeneinteilung? einteilung = null, Action<Schrankenfortschritt>? fortschritt = null, TimeSpan? zeitStufeC = null, Func<bool>? aufhoeren = null, Func<IReadOnlyList<Spiel>?>? startplan = null, Laufzeitregel? laufzeit = null)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        ArgumentNullException.ThrowIfNull(loeser);

        // Nur Rückrunde: Die Vorrunde des bestehenden Plans bleibt fest und bestimmt die Richtung der Rückspiele.
        RefPlan plan = RefPlan.Laden(optionen.Rundenplanung == Rundenplanung.NurRueckrunde ? staffel : staffel with { BestehenderSpielplan = [] }, optionen);
        var d = new KernDefinition(plan);
        if (Grund(d) is { } grund)
        {
            return new Schrankenbericht(0, 0, [], [grund], []);
        }

        return new Ligaschranke(d) { aufhoeren = aufhoeren, startplan = startplan, laufzeit = laufzeit }.Rechnen(loeser, zeitJeStufe, zeitStufeC ?? zeitJeStufe, (einteilung ?? Stufeneinteilung.Standard).Vervollstaendigt(), fortschritt ?? (_ => { }));
    }

    /// <summary>
    /// Die Kriterien mit einer Anzahl von Verstößen, die das Modell für diese Staffel nicht kennt – sie fehlen in den Grenzen und
    /// Varianten aus HiGHS, und der Automodus kann sie nicht nach A oder B schieben. <c>null</c>, wenn es für die Staffel kein
    /// Modell gibt (dann fehlt HiGHS ganz, und die Einteilung ist frei).
    /// </summary>
    /// <param name="staffel">Die Staffel (bei „nur Rückrunde“ mit der Vorrunde als bestehendem Plan).</param>
    /// <param name="optionen">Die Optionen.</param>
    /// <returns>Die unbekannten Kriterien (meist leer), oder <c>null</c>.</returns>
    public static IReadOnlyList<Kostenkriterium>? Unbekannt(Staffel staffel, Berechnungsoptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        RefPlan plan = RefPlan.Laden(optionen.Rundenplanung == Rundenplanung.NurRueckrunde ? staffel : staffel with { BestehenderSpielplan = [] }, optionen);
        var d = new KernDefinition(plan);
        return Grund(d) is null ? new Ligaschranke(d).Unbekannt() : null;
    }

    /// <summary>Wie <c>KernPlan.GueltigesAuswaertsKoppelDatum</c> für eine Art (ohne Prüfung der Gegner).</summary>
    private static bool KoppelDatumPasst(AuswaertsKoppelTyp art, double datum1, double datum2)
    {
        int abstand = Math.Abs(DelphiDatum.Trunc(datum1) - DelphiDatum.Trunc(datum2));
        return art switch
        {
            AuswaertsKoppelTyp.Keiner => false,
            AuswaertsKoppelTyp.GleicherTag => abstand == 0 && Math.Abs(datum1 - datum2) >= DelphiDatum.MinGameDistance,
            AuswaertsKoppelTyp.AndererTag => abstand == 1,
            _ => abstand <= 1 && Math.Abs(datum1 - datum2) >= DelphiDatum.MinGameDistance,
        };
    }

    /// <summary>Minimiert die Zielfunktion im Modell (mit den dort festgehaltenen Stufen).</summary>
    private static (int Schranke, int? Gefunden, double[]? Werte, bool Unloesbar) Minimieren(Ganzzahlmodell m, List<(int Spalte, double Faktor)> ziel, ISchrankenloeser loeser, TimeSpan zeit, double[]? start = null, Func<bool>? aufhoeren = null, Laufzeitregel? regel = null, Action<double[]>? zwischen = null)
    {
        if (ziel.Count == 0)
        {
            return (0, 0, null, false);
        }

        for (int c = 0; c < m.Spalten; c++)
        {
            m.Preis(c, 0);
        }

        foreach ((int c, double f) in ziel)
        {
            m.Preis(c, f);
        }

        Highsauslastung.Beginnt();
        Schrankenloesung l;
        try
        {
            l = loeser.Loesen(m, zeit, start, aufhoeren, regel, zwischen);
        }
        finally
        {
            Highsauslastung.Endet();
        }

        int schranke = double.IsFinite(l.Schranke) ? Math.Max(0, (int)Math.Ceiling(l.Schranke - 1e-6)) : 0;
        int? gefunden = l.Werte is null ? null : (int)Math.Round(l.Wert);
        return (schranke, gefunden, l.Werte, l.Unloesbar);
    }

    /// <summary>Die Laufzeitregel für Stufe A: mindestens 60 s, Ende erst nach 30 s ohne Fortschritt (höchstens wie die Regel).</summary>
    private static Laufzeitregel Geduldig(Laufzeitregel regel) => regel with
    {
        Mindestens = regel.Mindestens < TimeSpan.FromSeconds(60) ? TimeSpan.FromSeconds(60) : regel.Mindestens,
        Stillstand = regel.Stillstand < TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : regel.Stillstand,
    };

    /// <summary>Kriterien, deren genaue Form das Modell für HiGHS schwer macht (Stufe B rechnet erst ohne sie).</summary>
    private static bool Schwer(Kostenkriterium k) => k is Kostenkriterium.AbstandHeimAuswaerts or Kostenkriterium.Setzliste;

    /// <summary>Der Name eines Kriteriums in Bericht und Varianten.</summary>
    private static string Name(Kostenkriterium k) => k switch
    {
        Kostenkriterium.VereinsinterneSpieleAmAnfang => "Vereinsinterne Spiele am Anfang",
        Kostenkriterium.ParalleleSpiele => "Parallele Spiele",
        Kostenkriterium.Auswaertskoppel => "Auswärtskoppel",
        _ => k.ToString(),
    };

    /// <summary>Löst mehrere Ziele gleichzeitig, jedes auf seiner Kopie des Modells (HiGHS rechnet je Lauf auf einem Kern).</summary>
    private static (int Schranke, int? Gefunden, double[]? Werte, bool Unloesbar)[] Parallel(Ganzzahlmodell[] kopien, List<List<(int Spalte, double Faktor)>> ziele, ISchrankenloeser loeser, TimeSpan zeit, Func<bool>? aufhoeren = null, double[]? start = null, Laufzeitregel? regel = null)
    {
        var ergebnis = new (int Schranke, int? Gefunden, double[]? Werte, bool Unloesbar)[ziele.Count];
        var optionen = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
        System.Threading.Tasks.Parallel.For(0, ziele.Count, optionen, i => ergebnis[i] = Minimieren(kopien[i], ziele[i], loeser, zeit, start, aufhoeren, regel));
        return ergebnis;
    }

    private static List<(int Spalte, double Faktor)> Zusammenfassen(IEnumerable<(int Spalte, double Faktor)> eintraege) =>
        eintraege.GroupBy(e => e.Spalte).Select(g => (g.Key, g.Sum(e => e.Faktor))).Where(e => e.Item2 != 0).ToList();

    /// <summary>Die Kriterien mit Anzahl, die das Modell nicht kennt (legt dabei ihre Hilfsspalten an).</summary>
    private List<Kostenkriterium> Unbekannt() =>
        Enum.GetValues<Kostenkriterium>().Where(k => Stufeneinteilung.HatAnzahl(k) && Ziel(k) is null).ToList();

    private Schrankenbericht Rechnen(ISchrankenloeser loeser, TimeSpan zeit, TimeSpan zeitC, Stufeneinteilung einteilung, Action<Schrankenfortschritt> fortschritt)
    {
        List<(int Spalte, double Faktor)> a1 = ohneTermin.Select(u => (u.Spalte, 1.0)).ToList();

        // Die Kette läuft nacheinander, jede Stufe wird für die nächste festgehalten: A1 (Spiele ohne Termin), dann die Summe
        // aller A-Verstöße, dann die Summe aller B-Verstöße – wie der Automodus vergleicht (innerhalb einer Stufe zählt jeder
        // Verstoß gleich). Nach A1 rechnet jedes Kriterium allein auf eigenen Kopien des Modells parallel dazu (die Spalte
        // „Bestmöglich“ je Kriterium), nach der Kette die C-Werte und der Feinschliff. Die Ziele der Kriterien entstehen erst
        // nach A1: Die genauen Formen gelten nur, wenn A1 auf 0 festgehalten ist.
        var einzelne = new List<(string Stufe, string Name, Kostenkriterium? Kriterium, List<(int Spalte, double Faktor)> Ziel)>();
        var unbekannt = new List<Kostenkriterium>();
        var kettenziele = new List<(string Stufe, string Name, List<(int Spalte, double Faktor)> Ziel)>
        {
            ("A1", "Spiele ohne Termin", Zusammenfassen(a1)),
        };

        // Abstand H/A und Setzliste (genau) machen das Modell für HiGHS schwer: Ihre Zeilen werden zuletzt angelegt und gemerkt –
        // die Stufe B rechnet erst ohne sie („leicht“, dort findet HiGHS schnell gute Pläne) und dann mit ihnen von diesen Plänen aus.
        var schwereZeilen = new List<(int Von, int Bis)>();
        void ZieleAnlegen()
        {
            var reihenfolge = new List<(string Stufe, Kostenkriterium Kriterium)>();
            foreach ((string buchstabe, IReadOnlyList<Kostenkriterium> liste, int erste) in new[] { ("A", einteilung.A, 2), ("B", einteilung.B, 1) })
            {
                reihenfolge.AddRange(liste.Select((k, i) => (buchstabe + (i + erste).ToString(CultureInfo.InvariantCulture), k)));
            }

            foreach ((string stufe, Kostenkriterium k) in reihenfolge.OrderBy(e => Schwer(e.Kriterium)))
            {
                int zeilen = modell.Zeilen;
                if (Ziel(k) is not { } ziel)
                {
                    hinweise.Add($"{stufe}: {Name(k)} kennt das Modell nicht – fehlt in Grenze und Varianten der Stufe.");
                    unbekannt.Add(k);
                    continue;
                }

                if (Schwer(k) && modell.Zeilen > zeilen)
                {
                    schwereZeilen.Add((zeilen, modell.Zeilen));
                }

                einzelne.Add((stufe, Name(k), k, ziel));
            }

            einzelne.Sort((x, y) => string.CompareOrdinal(x.Stufe[..1], y.Stufe[..1]) != 0
                ? string.CompareOrdinal(x.Stufe[..1], y.Stufe[..1])
                : int.Parse(x.Stufe[1..], CultureInfo.InvariantCulture).CompareTo(int.Parse(y.Stufe[1..], CultureInfo.InvariantCulture)));
            kettenziele.Add(("A", "Stufe A gesamt", Zusammenfassen(einzelne.Where(z => z.Stufe[0] == 'A').SelectMany(z => z.Ziel))));
            kettenziele.Add(("B", "Stufe B gesamt", Zusammenfassen(einzelne.Where(z => z.Stufe[0] == 'B').SelectMany(z => z.Ziel))));
        }

        // Das leichte Modell: eine Kopie, in der die schweren Zeilen nicht gelten (ihre Hilfsspalten sind dann frei und fallen
        // beim Lösen weg); null, wenn es nichts Schweres gibt.
        Ganzzahlmodell? Leicht()
        {
            if (schwereZeilen.Count == 0)
            {
                return null;
            }

            Ganzzahlmodell kopie = modell.Kopie();
            foreach ((int von, int bis) in schwereZeilen)
            {
                for (int r = von; r < bis; r++)
                {
                    kopie.ZeileGrenzen(r, double.NegativeInfinity, double.PositiveInfinity);
                }
            }

            return kopie;
        }

        // Ein Plan des leichten Modells im vollen: Ansetzungen festlegen, Hilfsspalten (auch die schweren) ergänzen.
        double[]? Ergaenzen(double[] leicht)
        {
            List<(int Spalte, double Faktor)> alles = Zusammenfassen(a1.Concat(einzelne.SelectMany(z => z.Ziel)));
            Ganzzahlmodell kopie = modell.Kopie();
            foreach (int spalte in termine.Select(t => t.Spalte).Concat(ohneTermin.Select(u => u.Spalte)))
            {
                double wert = Math.Round(leicht[spalte]);
                kopie.Zeile([spalte], wert, wert);
            }

            return Minimieren(kopie, alles, loeser, zeit, aufhoeren: aufhoeren).Werte;
        }

        var kette = new List<(int Schranke, int? Gefunden, bool Bewiesen, double Sekunden)>();
        double[]? beste = null;
        int[] einzeln = [];
        int?[] alleinGefunden = [];

        // Gedächtnis: Für dasselbe Modell (Fingerabdruck vor der Kette, festgehaltene Stufen) und Ziel gilt eine einmal bewiesene
        // Schranke auch jetzt – die Grenze sinkt bei einer neuen Rechnung nicht wieder.
        string grundmodell = modell.Fingerabdruck();
        var festgehalten = new System.Text.StringBuilder();
        string Schluessel(List<(int Spalte, double Faktor)> ziel) =>
            grundmodell + "|" + festgehalten + "|" + string.Join(";", ziel.OrderBy(e => e.Spalte).Select(e => string.Create(CultureInfo.InvariantCulture, $"{e.Spalte}:{e.Faktor}")));

        // Der beste Plan der Generierung im Modell (mit Hilfsspalten): Startlösung jeder Stufe, solange er die festgehaltenen
        // Stufen einhält (sonst null).
        double[]? VomPlan()
        {
            List<(int Spalte, double Faktor)> alles = Zusammenfassen(a1.Concat(einzelne.SelectMany(z => z.Ziel)));
            if (startplan?.Invoke() is not { Count: > 0 } p || aufhoeren?.Invoke() == true)
            {
                return null;
            }

            double[] x = Abbilden(p, out _);
            Ganzzahlmodell kopie = modell.Kopie();
            foreach (int spalte in termine.Select(t => t.Spalte).Concat(ohneTermin.Select(u => u.Spalte)))
            {
                kopie.Zeile([spalte], x[spalte], x[spalte]);
            }

            return Minimieren(kopie, alles, loeser, zeit, aufhoeren: aufhoeren).Werte;
        }

        // Für die Alternativen in B: das Modell mit festgehaltenem A (vor den Bedingungen der Summe B) und je B-Kriterium sein
        // Ergebnis allein.
        Ganzzahlmodell? mitA = null;
        var alleinB = new Dictionary<int, (int? Gefunden, double[]? Werte)>();
        List<(int Index, double[] Werte)> alternativenB = [];
        double[]? summenplanB = null;

        // Für den Plan „A und B zusammen“: das Modell nach A1 (ohne Spiele ohne Termin), A noch frei.
        Ganzzahlmodell? nachA1 = null;

        // Bessere Pläne mitten in einem Schritt gehen gleich hinaus (höchstens alle Meldeabstand): Der Automodus soll früh einen
        // guten Plan haben, nicht erst am Ende des Schritts.
        long letzteMeldung = 0;
        Action<double[]> Zwischen(string text) => werte =>
        {
            long jetzt = Stopwatch.GetTimestamp();
            long alt = Interlocked.Read(ref letzteMeldung);
            if (alt != 0 && Stopwatch.GetElapsedTime(alt, jetzt) < Meldeabstand)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref letzteMeldung, jetzt, alt) == alt)
            {
                fortschritt(new Schrankenfortschritt(text, 0, 0, Plan(werte), NurPlan: true));
            }
        };

        // Die Summe B früh: gleich nach Stufe A neben den B-Kriterien einzeln und den Varianten (die vor allem Grenzen und Pläne zum
        // Wählen liefern). Ihre Pläne gehen sofort hinaus; die eigentliche Summe B beginnt dann bei ihrem besten.
        var hauptBeginnt = new System.Runtime.CompilerServices.StrongBox<bool>();
        Task<double[]?>? summeFrueh = null;
        const int Stufen = 3;
        for (int n = 0; n < Stufen && n < kettenziele.Count; n++)
        {
            (string stufe, string name, List<(int Spalte, double Faktor)> ziel) = kettenziele[n];

            // Startlösung: der bessere aus dem Plan der Generierung und dem Plan der vorigen Stufe. Der Plan der Generierung fehlt,
            // wenn er die festgehaltenen Stufen nicht einhält (etwa noch Spiele ohne Termin hat) – dann begann HiGHS ohne Startlösung
            // und fand in der Zeit oft nur schlechte Pläne (Verbandsoberliga: A 72 statt 10, nicht bewiesen).
            double[]? vomPlan = VomPlan();
            double[]? stufenstart = new[] { vomPlan, beste }.OfType<double[]>().Where(w => w.Length == modell.Spalten).MinBy(w => Wert(ziel, w));
            if (stufe == "B" && ziel.Count > 0 && Environment.ProcessorCount >= FruehAb && aufhoeren?.Invoke() != true)
            {
                Ganzzahlmodell frueh = modell.Kopie();
                List<(int Spalte, double Faktor)> zielB = ziel;
                double[]? fruehStart = stufenstart;

                // Sie läuft, bis die eigentliche Summe B beginnt (mit viel Geduld: Bis dahin rechnet HiGHS sonst nur Grenzen).
                Laufzeitregel? fruehRegel = laufzeit is null ? null : laufzeit with { Hoechstens = TimeSpan.FromMinutes(10), Stillstand = TimeSpan.FromMinutes(2) };
                summeFrueh = Task.Run(() => Minimieren(frueh, zielB, loeser, zeit, fruehStart, () => Volatile.Read(ref hauptBeginnt.Value) || aufhoeren?.Invoke() == true, fruehRegel, Zwischen("Summe B früh")).Werte);
            }

            if (n > 0)
            {
                // Vor der Summe einer Stufe ihre Kriterien einzeln (auf Kopien, gleichzeitig): Ihre Summe ist eine untere
                // Schranke der Summe; als Bedingung im Modell beweist HiGHS die Summe damit oft sofort. Bei A gelten die Werte für
                // jeden Plan ohne Spiele ohne Termin (die Spalte „Bestmöglich“ je Kriterium), bei B bei bestmöglichem A.
                int[] index = Enumerable.Range(0, einzelne.Count).Where(i => einzelne[i].Stufe[0] == stufe[0]).ToArray();
                List<List<(int Spalte, double Faktor)>> alleine = index.Select(i => Zusammenfassen(einzelne[i].Ziel)).ToList();
                Ganzzahlmodell? leicht = stufe[0] == 'B' ? Leicht() : null;
                Ganzzahlmodell[] kopien = index.Select(i => leicht is not null && !Schwer(einzelne[i].Kriterium!.Value) ? leicht.Kopie() : modell.Kopie()).ToArray();
                if (stufe[0] == 'B')
                {
                    mitA = modell.Kopie();
                }

                (int Schranke, int? Gefunden, double[]? Werte, bool Unloesbar)[] ergebnisse = Parallel(kopien, alleine, loeser, zeit, aufhoeren, stufenstart, laufzeit);
                int[] einzelwerte = ergebnisse.Select((r, i) => Schrankengedaechtnis.Merken(Schluessel(alleine[i]), r.Schranke)).ToArray();
                for (int i = 0; i < index.Length; i++)
                {
                    einzeln[index[i]] = einzelwerte[i];
                    alleinGefunden[index[i]] = ergebnisse[i].Gefunden;
                    if (stufe[0] == 'B')
                    {
                        alleinB[index[i]] = (ergebnisse[i].Gefunden, ergebnisse[i].Werte);
                    }
                }

                if (einzelwerte.Sum() > 0 && ziel.Count > 0)
                {
                    modell.Zeile(ziel, einzelwerte.Sum(), double.PositiveInfinity);
                }
            }

            // Vor der Summe B die Alternativen (je B-Kriterium bestmöglich, gleichzeitig): Die beste nach der Summe ist der
            // Startplan der Summe B – ohne ihn findet HiGHS dort in der Zeit oft nur schlechte Pläne.
            // Startlösung: die beste aus dem Plan der Generierung und den Alternativen.
            double[]? start = stufenstart;
            if (stufe == "B" && mitA is not null && aufhoeren?.Invoke() != true)
            {
                fortschritt(new Schrankenfortschritt("Varianten in Stufe B", n, Stufen + 1, beste is null ? null : Plan(beste)));
                Dictionary<int, double[]> starts = [];
                if (Leicht() is { } mitALeicht)
                {
                    // Erst leicht (ohne Abstand H/A und Setzliste): Varianten und Summe der übrigen B-Kriterien, dann die Pläne im
                    // vollen Modell ergänzt als Startlösungen – von dort aus senkt HiGHS auch Abstand und Setzliste, so weit es kommt.
                    int[] leichte = Enumerable.Range(0, einzelne.Count).Where(i => einzelne[i].Stufe[0] == 'B' && !Schwer(einzelne[i].Kriterium!.Value)).ToArray();
                    List<(int Spalte, double Faktor)> summeLeicht = Zusammenfassen(leichte.SelectMany(i => einzelne[i].Ziel));
                    List<(int Index, double[] Werte)> leichtAlternativen = Alternativen(einzelne, alleinB, mitALeicht, summeLeicht, loeser, zeit, leichte);
                    double[]? leichtStart = leichtAlternativen.Select(e => e.Werte).Append(stufenstart).OfType<double[]>().MinBy(w => Wert(summeLeicht, w));
                    fortschritt(new Schrankenfortschritt("Summe B ohne Abstand H/A und Setzliste", n, Stufen + 1, beste is null ? null : Plan(beste)));
                    double[]? leichtSumme = Minimieren(mitALeicht, summeLeicht, loeser, zeit, leichtStart, aufhoeren, laufzeit).Werte ?? leichtStart;
                    List<double[]> leichtePlaene = [.. leichtAlternativen.Select(e => e.Werte), .. leichtSumme is null ? Array.Empty<double[]>() : [leichtSumme]];
                    var ergaenzt = new double[]?[leichtePlaene.Count];
                    var optionen = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
                    System.Threading.Tasks.Parallel.For(0, ergaenzt.Length, optionen, j => ergaenzt[j] = Ergaenzen(leichtePlaene[j]));
                    for (int j = 0; j < leichtAlternativen.Count; j++)
                    {
                        if (ergaenzt[j] is { } w)
                        {
                            starts[leichtAlternativen[j].Index] = w;
                        }
                    }

                    if (leichtSumme is not null && ergaenzt[^1] is { } s && (stufenstart is null || Wert(ziel, s) < Wert(ziel, stufenstart)))
                    {
                        start = s;
                    }

                    fortschritt(new Schrankenfortschritt("Varianten in Stufe B mit Abstand H/A und Setzliste", n, Stufen + 1, beste is null ? null : Plan(beste)));
                }

                alternativenB = Alternativen(einzelne, alleinB, mitA, ziel, loeser, zeit, starts: starts);
                start = alternativenB.Select(e => e.Werte).Append(start).OfType<double[]>().MinBy(w => Wert(ziel, w));
            }

            if (summeFrueh is not null)
            {
                // Die frühe Summe B endet hier; ihr Plan ist Startlösung, wenn er besser ist.
                Volatile.Write(ref hauptBeginnt.Value, true);
                if (summeFrueh.GetAwaiter().GetResult() is { } f && (start is null || Wert(ziel, f) < Wert(ziel, start)))
                {
                    start = f;
                }

                summeFrueh = null;
            }

            fortschritt(new Schrankenfortschritt(n == 0 ? $"{stufe} {name}" : name, n, Stufen + 1, beste is null ? null : Plan(beste)));
            var uhr = Stopwatch.StartNew();

            // Stufe A mit mehr Geduld: Mit der Regel für die übrigen Schritte brach sie nach 27 s mit A 66 ab, wenige Sekunden bevor
            // HiGHS A 10 gefunden und bewiesen hätte (Verbandsoberliga).
            Laufzeitregel? regel = n == 1 && laufzeit is not null ? Geduldig(laufzeit) : laufzeit;
            (int schranke, int? gefunden, double[]? werte, bool unloesbar) = Minimieren(modell, ziel, loeser, zeit, start, aufhoeren, regel, Zwischen(n == 0 ? $"{stufe} {name}" : name));
            schranke = Schrankengedaechtnis.Merken(Schluessel(ziel), schranke);
            if (stufe == "B")
            {
                summenplanB = werte ?? start;
            }

            if (aufhoeren?.Invoke() == true)
            {
                hinweise.Add("Abgebrochen.");
                break;
            }

            if (unloesbar)
            {
                hinweise.Add($"{name}: Das Modell hat keine Lösung (Fehler im Modell?).");
                break;
            }

            bool bewiesen = gefunden == schranke;
            kette.Add((schranke, gefunden, bewiesen, uhr.Elapsed.TotalSeconds));
            beste = werte ?? beste;
            if (!bewiesen && gefunden is null)
            {
                hinweise.Add($"{name}: HiGHS hat in der Zeit keinen Plan gefunden – die weiteren Stufen hängen davon ab und fehlen.");
                break;
            }

            // Weiter nur mit einem Plan, der mindestens so gut ist wie der der Generierung – sonst gälten die weiteren Stufen (B,
            // Varianten, A+B, C) für Pläne, die schon in dieser Stufe schlechter sind, und wären unbrauchbar.
            if (!bewiesen && vomPlan is not null && gefunden > Wert(ziel, vomPlan) + 0.5)
            {
                hinweise.Add($"{name} in der Zeit nicht bewiesen, und HiGHS fand nur {gefunden} (der Plan der Generierung hat {Wert(ziel, vomPlan):0}) – die weiteren Stufen fehlen.");
                break;
            }

            // Nicht bewiesen: Die Stufe bleibt beim besten gefundenen Wert, und es geht weiter – die Grenzen danach gelten dann für
            // Pläne, die in dieser Stufe so gut sind wie der gefundene (der Klassenleiter sieht trotzdem B, A+B und C).
            int halten = bewiesen ? schranke : gefunden!.Value;
            if (!bewiesen)
            {
                hinweise.Add($"{name} in der Zeit nicht bewiesen (Grenze {schranke}, bester Plan {halten}) – die weiteren Stufen gelten bei {stufe} ≤ {halten}.");
            }

            if (ziel.Count > 0)
            {
                // Höhere Stufe festhalten: Die nächste darf sie nicht verschlechtern.
                modell.Zeile(ziel, double.NegativeInfinity, halten);
            }

            festgehalten.Append(CultureInfo.InvariantCulture, $"{stufe}≤{halten};");
            if (n == 1)
            {
                // Die Schranke von A gleich weitergeben: Hat der beste Plan sie, ist bei A nichts mehr zu holen. Und die
                // Teilneuplanung darf rechnen (auch wenn A nicht bewiesen ist: A bleibt dann beim gefundenen Wert).
                fortschritt(new Schrankenfortschritt("Stufe B: Kriterien einzeln", 2, Stufen + 1, beste is null ? null : Plan(beste), SchrankeA: bewiesen ? halten : null, AFertig: true));
            }

            if (n == 0)
            {
                // Die genauen Formen (Spieltage, Abstand H/A, Setzliste) zählen nur Spiele mit Termin: nur, wenn es keine gibt.
                genauMoeglich = halten == 0;
                if (!genauMoeglich)
                {
                    hinweise.Add("Spiele ohne Termin sind unvermeidbar: Abstand H/A, Setzliste und Spieltage kennt das Modell dann nicht.");
                }

                ZieleAnlegen();
                einzeln = new int[einzelne.Count];
                alleinGefunden = new int?[einzelne.Count];
                nachA1 = modell.Kopie();

                // Der Plan aus A1 kennt die neuen Hilfsspalten noch nicht: ergänzen (sonst fehlen ihm die Werte der Kriterien).
                if (beste is not null && beste.Length < modell.Spalten)
                {
                    beste = Ergaenzen(beste) ?? beste;
                }
            }
        }

        // A und B zusammen: Jeder Verstoß in A und B zählt gleich – der Staffelleiter sieht, ob ein etwas schlechteres A deutlich
        // weniger B-Verstöße bringt. Start ist der Plan für die Summe B (er erfüllt das Modell nach A1).
        List<(int Spalte, double Faktor)> zielAB = kettenziele.Count > 2 ? Zusammenfassen(kettenziele[1].Ziel.Concat(kettenziele[2].Ziel)) : [];
        (double[]? Werte, bool Bewiesen)? planAB = null;
        if (nachA1 is not null && summenplanB is not null && zielAB.Count > 0 && aufhoeren?.Invoke() != true)
        {
            fortschritt(new Schrankenfortschritt("A und B zusammen", Stufen, Stufen + 1, beste is null ? null : Plan(beste)));
            (int schrankeAB, int? gefundenAB, double[]? werteAB, _) = Minimieren(nachA1, zielAB, loeser, zeit, summenplanB, aufhoeren, laufzeit, Zwischen("A und B zusammen"));
            planAB = (werteAB, werteAB is not null && gefundenAB == schrankeAB);
        }

        // Je Kriterium der Stufen A und B: Schranke ist der Wert allein (A: kein Plan ohne Spiele ohne Termin unterschreitet ihn;
        // B: bei bestmöglichem A), „Gefunden“ sein Wert im besten Plan; bewiesen, wenn die Summe seiner Stufe bewiesen ist.
        Schrankenstufe? Summe(int n) => n < kette.Count
            ? new Schrankenstufe(kettenziele[n].Stufe, kettenziele[n].Name, kette[n].Schranke, kette[n].Gefunden, kette[n].Bewiesen, kette[n].Sekunden)
            : null;
        Schrankenstufe? summeA = Summe(1);
        Schrankenstufe? summeB = Summe(2);
        Schrankenbericht Bericht(double[]? endplan, List<Schrankenstufe> c)
        {
            var stufen = einzelne.Select((z, i) =>
            {
                bool bewiesen = (z.Stufe[0] == 'A' ? summeA : summeB)?.Bewiesen == true;
                int? gefunden = endplan is null ? null : (int)Math.Round(Wert(Zusammenfassen(z.Ziel), endplan));
                return new Schrankenstufe(z.Stufe, z.Name, einzeln[i], gefunden, bewiesen, 0, z.Kriterium, einzeln[i]);
            }).Where(z => kette.Count >= (z.Stufe[0] == 'A' ? 2 : 3)).ToList();
            if (kette.Count > 0)
            {
                stufen.Insert(0, new Schrankenstufe("A1", "Spiele ohne Termin", kette[0].Schranke, kette[0].Gefunden, kette[0].Bewiesen, kette[0].Sekunden, null, kette[0].Schranke));
            }

            stufen.AddRange(c);

            // Varianten nur, wo sie sich vom Plan der Summe unterscheiden: Ihr Kriterium ist dort schlechter. Dazu immer der Plan
            // für die Summe in B selbst – auch ohne andere Varianten ist er zur Wahl. „Bewiesen“: der Wert des Kriteriums allein
            // bzw. die Summe B.
            double[]? vergleich = endplan ?? summenplanB;
            var alternativen = alternativenB
                .Where(e => vergleich is null || Wert(Zusammenfassen(einzelne[e.Index].Ziel), e.Werte) < Wert(Zusammenfassen(einzelne[e.Index].Ziel), vergleich) - 0.5)
                .Select(e => new Planalternative(einzelne[e.Index].Kriterium, einzelne[e.Index].Name, Plan(e.Werte), alleinGefunden[e.Index] == einzeln[e.Index]))
                .ToList();
            if (summenplanB is not null)
            {
                // Kennt das Modell nicht alle Kriterien der Stufe, gilt der Plan nur für die bekannten – und „optimal“ nur dort.
                bool optimal = summeB?.Bewiesen == true;
                string b = Stufenname('B');
                alternativen.Insert(0, new Planalternative(null, optimal ? $"Plan {b} optimal" : $"Bester Plan für {b} aus HiGHS", Plan(endplan ?? summenplanB), optimal));

                // A und B zusammen nur, wenn es dort weniger Verstöße gibt (dann mit mehr in A, weniger in B).
                if (planAB is { Werte: { } ab } && Wert(zielAB, ab) < Wert(zielAB, summenplanB) - 0.5)
                {
                    string ab2 = unbekannt.Count == 0 ? "A+B" : $"{Stufenname('A')} + {b}";
                    alternativen.Add(new Planalternative(null, planAB.Value.Bewiesen ? $"Plan {ab2} optimal" : $"Bester Plan für {ab2} aus HiGHS", Plan(ab), planAB.Value.Bewiesen, AUndB: true));
                }
            }

            return new Schrankenbericht(modell.Spalten, modell.Zeilen, stufen, hinweise, Plan(endplan ?? beste), summeA, summeB, alternativen, unbekannt);
        }

        // Name einer Stufe in den Varianten: „B“, wenn das Modell alle ihre Kriterien kennt, sonst die bekannten („B1–B7“).
        string Stufenname(char stufe)
        {
            if (!unbekannt.Any(k => einteilung.StufeVon(k) == (stufe == 'A' ? Stufe.A : Stufe.B)))
            {
                return stufe.ToString();
            }

            List<string> bekannt = einzelne.Where(z => z.Stufe[0] == stufe).Select(z => z.Stufe).ToList();
            bool fortlaufend = bekannt.Count > 1 && bekannt.Select(s => int.Parse(s[1..], CultureInfo.InvariantCulture)).Zip(bekannt.Skip(1).Select(s => int.Parse(s[1..], CultureInfo.InvariantCulture))).All(p => p.Second == p.First + 1);
            return bekannt.Count switch
            {
                0 => stufe + " (nichts)",
                1 => bekannt[0],
                _ when fortlaufend => $"{bekannt[0]}–{bekannt[^1]}",
                _ => string.Join(", ", bekannt),
            };
        }

        if (kette.Count == Stufen && beste is not null)
        {
            // A und B sind fertig: Bericht mit den Varianten schon jetzt (zum Wählen, während C rechnet).
            fortschritt(new Schrankenfortschritt("Stufe C und Feinschliff", Stufen, Stufen + 1, Plan(beste), Bericht(beste, [])));
            (List<Schrankenstufe> c, double[] plan) = StufeC(einteilung, beste, loeser, zeitC);
            return Bericht(plan, c);
        }

        return Bericht(beste, []);
    }

    /// <summary>
    /// Alternativen in B (bei festgehaltenem A): je B-Kriterium bleibt es bei seinem Wert allein, und die übrigen B-Verstöße
    /// werden zusammen so klein wie möglich (ab seinem Plan allein, auf eigenen Kopien gleichzeitig). So sieht der
    /// Staffelleiter, was er für ein Kriterium in den anderen bezahlt.
    /// </summary>
    private List<(int Index, double[] Werte)> Alternativen(
        List<(string Stufe, string Name, Kostenkriterium? Kriterium, List<(int Spalte, double Faktor)> Ziel)> einzelne,
        Dictionary<int, (int? Gefunden, double[]? Werte)> alleinB,
        Ganzzahlmodell mitA,
        List<(int Spalte, double Faktor)> summe,
        ISchrankenloeser loeser,
        TimeSpan zeit,
        int[]? nur = null,
        Dictionary<int, double[]>? starts = null)
    {
        int[] b = nur ?? Enumerable.Range(0, einzelne.Count).Where(i => einzelne[i].Stufe[0] == 'B').ToArray();
        if (b.Length < 2)
        {
            return [];
        }

        var kandidaten = new List<(int Index, double[] Start, Ganzzahlmodell Modell)>();
        foreach (int i in b)
        {
            if (alleinB.GetValueOrDefault(i) is { Gefunden: int wert, Werte: double[] allein })
            {
                Ganzzahlmodell kopie = mitA.Kopie();
                kopie.Zeile(Zusammenfassen(einzelne[i].Ziel), double.NegativeInfinity, wert);
                kandidaten.Add((i, starts?.GetValueOrDefault(i) ?? allein, kopie));
            }
        }

        var ergebnis = new (int Schranke, int? Gefunden, double[]? Werte, bool Unloesbar)[kandidaten.Count];
        var optionen = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
        System.Threading.Tasks.Parallel.For(0, kandidaten.Count, optionen, j =>
            ergebnis[j] = Minimieren(kandidaten[j].Modell, summe, loeser, zeit, kandidaten[j].Start, aufhoeren, laufzeit));
        return kandidaten.Select((k, j) => (k.Index, ergebnis[j].Werte ?? k.Start)).ToList();
    }

    /// <summary>
    /// Nach A und B (festgehalten): je C-Kriterium, das das Modell kennt, der bestmögliche Wert (gleichzeitig auf Kopien) und
    /// parallel dazu der Feinschliff des Startplans, der sie gewichtet in der Reihenfolge der Einteilung verbessert.
    /// </summary>
    private (List<Schrankenstufe> Stufen, double[] Plan) StufeC(Stufeneinteilung einteilung, double[] beste, ISchrankenloeser loeser, TimeSpan zeit)
    {
        // Wechsel H/A macht das Modell zu schwer (in 20 s keine Verbesserung) – das glättet der Automodus. Abstand H/A und
        // Setzliste (genau) ebenso: Ihre Grenzen werden berechnet, der Feinschliff lässt sie aus.
        var c = einteilung.C
            .Select((k, i) => (Kriterium: k, Stufe: "C" + (i + 1).ToString(CultureInfo.InvariantCulture), Ziel: k == Kostenkriterium.WechselHeimAuswaerts ? null : Ziel(k)))
            .Where(e => e.Ziel is { Count: > 0 })
            .Select(e => (e.Kriterium, e.Stufe, Ziel: Zusammenfassen(e.Ziel!)))
            .ToList();
        if (c.Count == 0)
        {
            return ([], beste);
        }

        var fein = new List<(int Spalte, double Faktor)>();
        double gewicht = 8;
        foreach ((Kostenkriterium k, _, List<(int Spalte, double Faktor)> ziel) in c)
        {
            if (k is Kostenkriterium.AbstandHeimAuswaerts or Kostenkriterium.Setzliste)
            {
                continue;
            }

            fein.AddRange(ziel.Select(e => (e.Spalte, e.Faktor * gewicht)));
            gewicht = Math.Max(1, gewicht / 2);
        }

        Ganzzahlmodell[] kopien = c.Select(_ => modell.Kopie()).ToArray();
        Ganzzahlmodell feinModell = modell.Kopie();
        var uhr = Stopwatch.StartNew();
        double[] plan = beste;
        (int Schranke, int? Gefunden, double[]? Werte, bool Unloesbar)[] werte = [];
        System.Threading.Tasks.Parallel.Invoke(
            () => werte = Parallel(kopien, c.Select(e => e.Ziel).ToList(), loeser, zeit, aufhoeren),
            () => plan = Feinschliff(feinModell, Zusammenfassen(fein), beste, loeser, zeit));
        hinweise.Add("Startplan zusätzlich verbessert in: " + string.Join(", ", c.Select(e => e.Kriterium).Where(k => k is not (Kostenkriterium.AbstandHeimAuswaerts or Kostenkriterium.Setzliste))) + ".");
        List<Schrankenstufe> stufen = c.Select((e, i) => new Schrankenstufe(
            e.Stufe, e.Kriterium.ToString(), werte[i].Schranke, werte[i].Gefunden, werte[i].Gefunden == werte[i].Schranke, uhr.Elapsed.TotalSeconds, e.Kriterium, -1)).ToList();
        return (stufen, plan);
    }

    /// <summary>Termine, ein Termin je Wunschtermin, ein Spiel je Mannschaft und Tag, Runden der Paarungen.</summary>
    private void Grundmodell()
    {
        var jeSpiel = new List<int>[d.SpielHeim.Length];
        for (int s = 0; s < d.SpielHeim.Length; s++)
        {
            int heim = d.SpielHeim[s];
            var eintraege = new List<int>();
            jeSpiel[s] = eintraege;
            for (int w = d.WunschStart[heim]; w < d.WunschStart[heim + 1]; w++)
            {
                if (!d.WunschInPlanung[w] || d.WunschSpielfrei[w])
                {
                    continue;
                }

                int i = termine.Count;
                termine.Add((modell.Spalte(0), s, w));
                jeMannschaft[heim].Add(i);
                jeMannschaft[d.SpielGast[s]].Add(i);
                if (!jeWunsch.TryGetValue(w, out List<int>? liste))
                {
                    jeWunsch[w] = liste = [];
                }

                liste.Add(i);
                eintraege.Add(termine[i].Spalte);
            }
        }

        if (d.Rundenplanung == Rundenplanung.Beide)
        {
            // Jedes Spiel bekommt einen Termin.
            for (int s = 0; s < jeSpiel.Length; s++)
            {
                SpielOderOhneTermin(s, jeSpiel[s]);
            }
        }
        else
        {
            // Halbrunde, nur Vor- oder Rückrunde: je Paarung ein Spiel. Die Richtung ist frei, außer bei der Rückrunde, wenn
            // die Vorrunde ein Spiel der Paarung festlegt: Dann ist das Rückspiel gesetzt.
            for (int h = 0; h < d.N; h++)
            {
                for (int g = h + 1; g < d.N; g++)
                {
                    int[] hin = d.Paarung[(h * d.N) + g];
                    int[] rueck = d.Paarung[(g * d.N) + h];
                    bool hinFest = Array.Exists(feste, f => f.Heim == h && f.Gast == g);
                    bool rueckFest = Array.Exists(feste, f => f.Heim == g && f.Gast == h);
                    int[] erlaubt = (hinFest, rueckFest) switch
                    {
                        (false, false) => [.. hin, .. rueck],
                        (true, false) => rueck,
                        (false, true) => hin,
                        _ => [],
                    };
                    foreach (int s in hin.Concat(rueck).Except(erlaubt).Where(s => jeSpiel[s].Count > 0))
                    {
                        modell.Zeile(jeSpiel[s], 0, 0);
                    }

                    if (erlaubt.Length > 0)
                    {
                        SpielOderOhneTermin(erlaubt[0], erlaubt.SelectMany(s => jeSpiel[s]).ToList());
                    }
                }
            }
        }

        // Ein Wunschtermin nimmt ein Spiel auf.
        foreach (List<int> liste in jeWunsch.Values.Where(l => l.Count > 1))
        {
            modell.Zeile(liste.Select(i => termine[i].Spalte), 0, 1);
        }

        // Eine Mannschaft spielt höchstens einmal am Tag – außer bei Koppelterminen nach den Regeln des Originals.
        bool koppel = d.HatKoppeltermine || d.HatAuswaertsKoppeltermine;
        for (int t = 0; t < d.N; t++)
        {
            foreach (IGrouping<int, int> tag in jeMannschaft[t].GroupBy(i => d.WunschTag[termine[i].Wunsch]).Where(g => g.Count() > 1))
            {
                if (koppel)
                {
                    KoppelTag(t, tag.ToList());
                }
                else
                {
                    modell.Zeile(tag.Select(i => termine[i].Spalte), 0, 1);
                }
            }
        }

        Paarungen();
    }

    /// <summary>Genau eine der Termin-Spalten oder „ohne Termin“ (steht für <paramref name="spiel"/>).</summary>
    private void SpielOderOhneTermin(int spiel, List<int> spalten)
    {
        int ohne = modell.Zahl(0, 1);
        ohneTermin.Add((ohne, spiel));
        modell.Zeile([.. spalten, ohne], 1, 1);
    }

    /// <summary>
    /// Wie <c>KernPlan.IstTerminFrei</c> für zwei Spiele einer Mannschaft am selben Tag: zwei Heimspiele auf Koppelterminen
    /// oder zwei Auswärtsspiele mit passendem Auswärtskoppel-Wunsch, jeweils mindestens 3:30 h auseinander; Heim und
    /// auswärts nie. Eine Zweitzeit gilt nur mit passendem Spiel. Gebündelt je Termin (Heim) bzw. Gastgeber-Termin (auswärts).
    /// </summary>
    private void KoppelTag(int t, List<int> amTag)
    {
        // Je Wunschtermin die Termin-Entscheidungen von t (heim bzw. auswärts beim Gastgeber dieses Termins).
        var heim = amTag.Where(i => d.SpielHeim[termine[i].Spiel] == t).GroupBy(i => termine[i].Wunsch).ToDictionary(g => g.Key, g => g.Select(i => termine[i].Spalte).ToList());
        var aus = amTag.Where(i => d.SpielGast[termine[i].Spiel] == t).GroupBy(i => termine[i].Wunsch).ToDictionary(g => g.Key, g => g.Select(i => termine[i].Spalte).ToList());
        List<int> hw = [.. heim.Keys];
        List<int> aw = [.. aus.Keys];

        bool HeimPasst(int w1, int w2) =>
            Math.Abs(d.WunschDatum[w1] - d.WunschDatum[w2]) >= DelphiDatum.MinGameDistance
            && Terminoptionen.IstKoppel(d.WunschOptionen[w1]) && Terminoptionen.IstKoppel(d.WunschOptionen[w2]);
        bool AusPasst(int w1, int w2) =>
            Math.Abs(d.WunschDatum[w1] - d.WunschDatum[w2]) >= DelphiDatum.MinGameDistance
            && Array.Exists(d.Auswaertskoppeln[t], k => KoppelDatumPasst(k.Art, d.WunschDatum[w1], d.WunschDatum[w2])
                && ((k.A == Gastgeber(w1) && k.B == Gastgeber(w2)) || (k.A == Gastgeber(w2) && k.B == Gastgeber(w1))));

        // Heim und auswärts schließen sich aus: „heute heim“ h ≥ jedes Heimspiel, h + jedes Auswärtsspiel ≤ 1.
        if (hw.Count > 0 && aw.Count > 0)
        {
            int h = modell.Spalte(0);
            foreach (int w in hw)
            {
                modell.Zeile([(h, 1.0), .. heim[w].Select(c => (c, -1.0))], 0, double.PositiveInfinity);
            }

            foreach (int w in aw)
            {
                modell.Zeile([(h, 1.0), .. aus[w].Select(c => (c, 1.0))], double.NegativeInfinity, 1);
            }
        }

        Paare(hw, heim, HeimPasst, Wunschterminoptionen.KoppelZweitzeit);
        Paare(aw, aus, AusPasst, Wunschterminoptionen.AuswaertsKoppelZweitzeit);
    }

    private int Gastgeber(int w) => Array.FindLastIndex(d.WunschStart, start => start <= w);

    /// <summary>Unverträgliche Termine schließen sich aus; eine Zweitzeit braucht ein verträgliches Spiel am selben Tag.</summary>
    private void Paare(List<int> termineAmTag, Dictionary<int, List<int>> spalten, Func<int, int, bool> passt, Wunschterminoptionen zweitzeit)
    {
        for (int a = 0; a < termineAmTag.Count; a++)
        {
            int w1 = termineAmTag[a];
            for (int b = a + 1; b < termineAmTag.Count; b++)
            {
                int w2 = termineAmTag[b];
                if (!passt(w1, w2))
                {
                    modell.Zeile([.. spalten[w1], .. spalten[w2]], 0, 1);
                }
            }

            if (Terminoptionen.Hat(d.WunschOptionen[w1], zweitzeit))
            {
                IEnumerable<int> partner = termineAmTag.Where(w2 => w2 != w1 && passt(w1, w2)).SelectMany(w2 => spalten[w2]);
                modell.Zeile([.. spalten[w1].Select(c => (c, 1.0)), .. partner.Select(c => (c, -1.0))], double.NegativeInfinity, 0);
            }
        }
    }

    /// <summary>Hin- und Rückspiel nicht in derselben Runde; bei Doppelrunde dieselbe Richtung nicht in derselben Hälfte.</summary>
    private void Paarungen()
    {
        var jeSpiel = termine.Select((t, i) => (t.Spiel, i)).ToLookup(e => e.Spiel, e => e.i);
        for (int h = 0; h < d.N; h++)
        {
            for (int g = 0; g < d.N; g++)
            {
                int[] hin = d.Paarung[(h * d.N) + g];
                if (h < g)
                {
                    IEnumerable<int> beide = hin.Concat(d.Paarung[(g * d.N) + h]).SelectMany(s => jeSpiel[s]);
                    Gruppen(beide, i => d.WunschRunde[termine[i].Wunsch]);
                }

                if (hin.Length > 1)
                {
                    Gruppen(hin.SelectMany(s => jeSpiel[s]), i => d.WunschRunde[termine[i].Wunsch] / 2);
                }
            }
        }
    }

    private void Gruppen(IEnumerable<int> termin, Func<int, int> schluessel)
    {
        foreach (IGrouping<int, int> gruppe in termin.GroupBy(schluessel).Where(g => g.Count() > 1))
        {
            modell.Zeile(gruppe.Select(i => termine[i].Spalte), 0, 1);
        }
    }

    /// <summary>
    /// Das Ziel eines Kriteriums im Modell (Zahl der Verstöße wie die Bewertung); <c>null</c>, wenn das Modell es nicht kennt.
    /// Für eine Schranke genau (Abstand H/A und Setzliste nach der Reihenfolge der eigenen Spiele, Spieltage); diese Formen
    /// zählen nur Spiele mit Termin und gelten erst, wenn A1 auf 0 festgehalten ist (<see cref="genauMoeglich"/>).
    /// </summary>
    /// <param name="k">Das Kriterium.</param>
    /// <param name="naeherung">
    /// Für die Teilneuplanung: Abstand H/A und Setzliste genähert (die genauen Formen machen die Ausschnitte für HiGHS zu
    /// schwer), es sei denn, sie sind ausdrücklich gewünscht.
    /// </param>
    private List<(int Spalte, double Faktor)>? Ziel(Kostenkriterium k, bool naeherung = false) => k switch
    {
        Kostenkriterium.AbstandHeimAuswaerts when naeherung => AbstandHeimAuswaerts(),
        Kostenkriterium.Setzliste when naeherung => Setzliste(),
        Kostenkriterium.AbstandHeimAuswaerts => genauMoeglich ? AbstandGenau() : null,
        Kostenkriterium.Setzliste => genauMoeglich ? SetzlisteGenau() : null,
        Kostenkriterium.Spielverteilung => Pausen(),
        Kostenkriterium.Spieltaglaenge when naeherung || genauMoeglich => Spieltage()?.Laenge,
        Kostenkriterium.Spieltagueberlappung when naeherung || genauMoeglich => Spieltage()?.Ueberlappung,
        Kostenkriterium.LetzterSpieltagLaenge when naeherung || genauMoeglich => Spieltage()?.LetzteLaenge,
        Kostenkriterium.LetzterSpieltagUeberlappung when naeherung || genauMoeglich => Spieltage()?.LetzteUeberlappung,
        Kostenkriterium.SechzigKilometerRegel => SechzigKilometer(),
        Kostenkriterium.Hallenbelegung => Hallenbelegung(),
        Kostenkriterium.ParalleleSpiele => ParalleleSpiele(),
        Kostenkriterium.Pflichtspieltage => Pflichtspieltage(),
        Kostenkriterium.VereinsinterneSpieleAmAnfang when d.Optionen.VereinsinterneSpieleAmAnfang == Gewichtung.NichtBeruecksichtigen => [],
        Kostenkriterium.VereinsinterneSpieleAmAnfang when !d.Doppelrunde => VereinsinterneAmAnfang(),
        Kostenkriterium.Sperrtermine => Sperrtermine(),
        Kostenkriterium.Ausweichtermine => Ausweichtermine(),
        Kostenkriterium.Heimkoppel => Heimkoppel(),
        Kostenkriterium.Auswaertskoppel => Auswaertskoppel(),
        Kostenkriterium.GleicheHeimtermine => GleicheHeimtermine(),
        Kostenkriterium.ZweiSpieleProWoche => ZweiSpieleProWoche(),
        Kostenkriterium.DreiTageAbstand => DreiTageAbstand(),
        Kostenkriterium.UngleichHeimAuswaerts => UngleichHeimAuswaerts(),
        Kostenkriterium.WechselHeimAuswaerts => WechselHeimAuswaerts(),
        _ => null,
    };

    /// <summary>
    /// Feinschliff ab dem besten Plan: erst dessen Ansetzungen festlegen und nur die neuen Hilfsspalten ergänzen (schnell),
    /// dann die Festlegung lösen und mit diesem vollständigen Startpunkt die C-Kriterien verbessern.
    /// </summary>
    private double[] Feinschliff(Ganzzahlmodell m, List<(int Spalte, double Faktor)> ziel, double[] beste, ISchrankenloeser loeser, TimeSpan zeit)
    {
        int erste = m.Zeilen;
        IEnumerable<int> ansetzungen = termine.Select(e => e.Spalte).Concat(ohneTermin.Select(u => u.Spalte));
        foreach (int c in ansetzungen)
        {
            double wert = Math.Round(beste[c]);
            m.Zeile([c], wert, wert);
        }

        (_, int? vorher, double[]? ergaenzt, _) = Minimieren(m, ziel, loeser, zeit, aufhoeren: aufhoeren);
        for (int r = erste; r < m.Zeilen; r++)
        {
            m.ZeileGrenzen(r, 0, 1);
        }

        if (ergaenzt is null)
        {
            hinweise.Add("Feinschliff: Ergänzen der Hilfsspalten ohne Ergebnis.");
            return beste;
        }

        (_, int? nachher, double[]? besser, _) = Minimieren(m, ziel, loeser, zeit, ergaenzt, aufhoeren);
        hinweise.Add($"Feinschliff (gewichtete C-Verstöße): {vorher} → {nachher?.ToString(CultureInfo.InvariantCulture) ?? "–"}.");
        return besser ?? ergaenzt;
    }

    /// <summary>Wie <c>KernPlan.GleicheHeimtermine</c>: je Heimspiel die gewünschten Nachbar-Heimspiele, die an dem Tag fehlen.</summary>
    private List<(int Spalte, double Faktor)> GleicheHeimtermine() =>
        termine
            .Select(e =>
            {
                int t = d.SpielHeim[e.Spiel];
                int tag = d.WunschTag[e.Wunsch];
                int[] teams = d.GleicheHeimtageTeams[t];
                if (!d.Nachbarspiele[t].TryGetValue(tag, out Nachbarspiel[]? liste))
                {
                    return (e.Spalte, (double)teams.Length);
                }

                int fehlend = teams.Count(team => !Array.Exists(liste, n => n.IstHeimspiel && n.ParalleleHeimspiele && n.Tag == tag && n.Schluessel == team));
                return (e.Spalte, (double)fehlend);
            })
            .Where(e => e.Item2 > 0)
            .ToList();

    /// <summary>
    /// Gekoppelte Paare von t (zwei Heimspiele auf Koppelterminen höchstens einen Tag auseinander, zwei Auswärtsspiele nach
    /// einem Auswärtskoppel-Wunsch): Spalte ≤ beide Spiele; mit den beiden Tagen. Je Mannschaft einmal angelegt.
    /// </summary>
    private List<(int Spalte, int Tag1, int Tag2)> KoppelPaare(int t)
    {
        if (koppelPaare.TryGetValue(t, out List<(int Spalte, int Tag1, int Tag2)>? fertig))
        {
            return fertig;
        }

        var paare = new List<(int Spalte, int Tag1, int Tag2)>();
        var heim = jeMannschaft[t].Where(i => d.SpielHeim[termine[i].Spiel] == t && Terminoptionen.IstKoppel(d.WunschOptionen[termine[i].Wunsch]))
            .GroupBy(i => termine[i].Wunsch).ToList();
        for (int a = 0; a < heim.Count; a++)
        {
            for (int b = a + 1; b < heim.Count; b++)
            {
                int w1 = heim[a].Key;
                int w2 = heim[b].Key;
                if (Math.Abs(d.WunschTag[w1] - d.WunschTag[w2]) <= 1 && Math.Abs(d.WunschDatum[w1] - d.WunschDatum[w2]) >= DelphiDatum.MinGameDistance)
                {
                    paare.Add(Paar(heim[a].Select(i => termine[i].Spalte), heim[b].Select(i => termine[i].Spalte), d.WunschTag[w1], d.WunschTag[w2]));
                }
            }
        }

        ILookup<int, int> auswaerts = jeMannschaft[t].Where(i => d.SpielGast[termine[i].Spiel] == t).ToLookup(i => d.SpielHeim[termine[i].Spiel]);
        foreach ((int a, int b, AuswaertsKoppelTyp art) in d.Auswaertskoppeln[t].Where(k => k.A >= 0 && k.B >= 0))
        {
            foreach (int i in auswaerts[a])
            {
                foreach (int j in auswaerts[b].Where(j => KoppelDatumPasst(art, d.WunschDatum[termine[i].Wunsch], d.WunschDatum[termine[j].Wunsch])))
                {
                    paare.Add(Paar([termine[i].Spalte], [termine[j].Spalte], d.WunschTag[termine[i].Wunsch], d.WunschTag[termine[j].Wunsch]));
                }
            }
        }

        koppelPaare[t] = paare;
        return paare;
    }

    private (int Spalte, int Tag1, int Tag2) Paar(IEnumerable<int> erstes, IEnumerable<int> zweites, int tag1, int tag2)
    {
        int paar = modell.Spalte(0);
        modell.Zeile([(paar, 1.0), .. erstes.Select(c => (c, -1.0))], double.NegativeInfinity, 0);
        modell.Zeile([(paar, 1.0), .. zweites.Select(c => (c, -1.0))], double.NegativeInfinity, 0);
        return (paar, Math.Min(tag1, tag2), Math.Max(tag1, tag2));
    }

    /// <summary>
    /// Wie <c>KernPlan.ZweiSpieleDieWoche</c>: je Woche aufeinanderfolgende Spiele, die nicht gekoppelt sind –
    /// mindestens Spiele der Woche − 1 − gekoppelte Paare der Woche.
    /// </summary>
    private List<(int Spalte, double Faktor)> ZweiSpieleProWoche()
    {
        var ziel = new List<(int Spalte, double Faktor)>();
        for (int t = 0; t < d.N; t++)
        {
            List<(int Spalte, int Tag1, int Tag2)> paare = KoppelPaare(t);
            foreach (IGrouping<int, int> woche in jeMannschaft[t].GroupBy(i => DelphiDatum.InterneWochennummer(d.WunschTag[termine[i].Wunsch])))
            {
                int w = woche.Key;
                int y = modell.Zahl(0, woche.Count());
                modell.Zeile(
                    [(y, 1.0), .. woche.Select(i => (termine[i].Spalte, -1.0)), .. paare.Where(p => DelphiDatum.InterneWochennummer(p.Tag1) == w && DelphiDatum.InterneWochennummer(p.Tag2) == w).Select(p => (p.Spalte, 1.0))],
                    -1,
                    double.PositiveInfinity);
                ziel.Add((y, 1));
            }
        }

        return ziel;
    }

    /// <summary>
    /// Wie <c>KernPlan.EngeTermine</c>: aufeinanderfolgende Spieltage einer Mannschaft höchstens drei Tage auseinander, die
    /// nicht gekoppelt sind. „Spielt am Tag“ q ≥ halbe Zahl der Spiele des Tages (höchstens zwei).
    /// </summary>
    private List<(int Spalte, double Faktor)> DreiTageAbstand()
    {
        var ziel = new List<(int Spalte, double Faktor)>();
        for (int t = 0; t < d.N; t++)
        {
            List<(int Spalte, int Tag1, int Tag2)> paare = KoppelPaare(t);
            var tage = jeMannschaft[t].GroupBy(i => d.WunschTag[termine[i].Wunsch]).OrderBy(g => g.Key).ToList();
            var q = new Dictionary<int, int>();
            foreach (IGrouping<int, int> tag in tage)
            {
                int spielt = modell.Spalte(0);
                q[tag.Key] = spielt;
                modell.Zeile([(spielt, 2.0), .. tag.Select(i => (termine[i].Spalte, -1.0))], 0, double.PositiveInfinity);
                modell.Zeile([(spielt, 1.0), .. tag.Select(i => (termine[i].Spalte, -1.0))], double.NegativeInfinity, 0);
            }

            for (int a = 0; a < tage.Count; a++)
            {
                for (int b = a + 1; b < tage.Count && tage[b].Key - tage[a].Key <= 3; b++)
                {
                    int d1 = tage[a].Key;
                    int d2 = tage[b].Key;

                    // z ≥ q(d1) + q(d2) − 1 − Spieltage dazwischen − gekoppelte Paare dieser beiden Tage
                    int z = modell.Spalte(0);
                    var zeile = new List<(int Spalte, double Faktor)> { (z, 1), (q[d1], -1), (q[d2], -1) };
                    zeile.AddRange(tage.Skip(a + 1).Take(b - a - 1).Select(g => (q[g.Key], 1.0)));
                    zeile.AddRange(paare.Where(p => p.Tag1 == d1 && p.Tag2 == d2).Select(p => (p.Spalte, 1.0)));
                    modell.Zeile(zeile, -1, double.PositiveInfinity);
                    ziel.Add((z, 1));
                }
            }
        }

        return ziel;
    }

    /// <summary>
    /// Wie <c>KernPlan.WechselHeimAuswaerts</c> (Anzahl): aufeinanderfolgende Spiele derselben Art (heim–heim, auswärts–auswärts),
    /// die nicht gekoppelt sind. Je Spieltag der Mannschaft: heim h, auswärts a, „schon gespielt“ G und „letztes Spiel heim“ L.
    /// </summary>
    private List<(int Spalte, double Faktor)> WechselHeimAuswaerts()
    {
        var ziel = new List<(int Spalte, double Faktor)>();
        for (int t = 0; t < d.N; t++)
        {
            List<(int Spalte, int Tag1, int Tag2)> paare = KoppelPaare(t);
            int vorG = -1;
            int vorL = -1;
            foreach (IGrouping<int, int> tag in jeMannschaft[t].GroupBy(i => d.WunschTag[termine[i].Wunsch]).OrderBy(g => g.Key))
            {
                int h = Tagesart(tag.Where(i => d.SpielHeim[termine[i].Spiel] == t));
                int a = Tagesart(tag.Where(i => d.SpielGast[termine[i].Spiel] == t));
                int g = modell.Spalte(0);
                int l = modell.Spalte(0);

                // G: schon gespielt; L: letztes Spiel war ein Heimspiel (bis einschließlich heute).
                modell.Zeile([(g, 1.0), (h, -1.0)], 0, double.PositiveInfinity);
                modell.Zeile([(g, 1.0), (a, -1.0)], 0, double.PositiveInfinity);
                modell.Zeile([(l, 1.0), (h, -1.0)], 0, double.PositiveInfinity);
                modell.Zeile([(l, 1.0), (a, 1.0)], double.NegativeInfinity, 1);
                if (vorG < 0)
                {
                    modell.Zeile([(g, 1.0), (h, -1.0), (a, -1.0)], double.NegativeInfinity, 0);
                    modell.Zeile([(l, 1.0), (h, -1.0)], double.NegativeInfinity, 0);
                }
                else
                {
                    modell.Zeile([(g, 1.0), (vorG, -1.0)], 0, double.PositiveInfinity);
                    modell.Zeile([(g, 1.0), (vorG, -1.0), (h, -1.0), (a, -1.0)], double.NegativeInfinity, 0);
                    modell.Zeile([(l, 1.0), (vorL, -1.0), (a, 1.0)], 0, double.PositiveInfinity);
                    modell.Zeile([(l, 1.0), (vorL, -1.0), (h, -1.0)], double.NegativeInfinity, 0);

                    // Gleiche Art wie das letzte Spiel, abzüglich gekoppelter Paare, die heute enden.
                    List<(int Spalte, double Faktor)> gekoppelt = paare.Where(p => p.Tag2 == tag.Key && p.Tag1 < p.Tag2).Select(p => (p.Spalte, 1.0)).ToList();
                    int sh = modell.Spalte(0);
                    modell.Zeile([(sh, 1.0), (h, -1.0), (vorL, -1.0), .. gekoppelt], -1, double.PositiveInfinity);
                    int sa = modell.Spalte(0);
                    modell.Zeile([(sa, 1.0), (a, -1.0), (vorG, -1.0), (vorL, 1.0), .. gekoppelt], -1, double.PositiveInfinity);
                    ziel.Add((sh, 1));
                    ziel.Add((sa, 1));
                }

                vorG = g;
                vorL = l;
            }
        }

        return ziel;
    }

    /// <summary>Spalte „spielt heute (in dieser Art)“: 1, sobald eines der Spiele stattfindet (höchstens zwei am Tag).</summary>
    private int Tagesart(IEnumerable<int> termineDerArt)
    {
        int[] spalten = termineDerArt.Select(i => termine[i].Spalte).ToArray();
        int q = modell.Spalte(0, spalten.Length > 0);
        if (spalten.Length > 0)
        {
            modell.Zeile([(q, 2.0), .. spalten.Select(c => (c, -1.0))], 0, double.PositiveInfinity);
            modell.Zeile([(q, 1.0), .. spalten.Select(c => (c, -1.0))], double.NegativeInfinity, 0);
        }

        return q;
    }

    /// <summary>
    /// Wie <c>KernPlan.ZahlHeimspiele</c>: je zu planende Runde |Heimspiele − Ziel| (abgerundet) plus Verstöße gegen
    /// Heimrecht-Vorgaben (je Spiel, für die Mannschaft mit der Vorgabe).
    /// </summary>
    private List<(int Spalte, double Faktor)> UngleichHeimAuswaerts()
    {
        var ziel = new List<(int Spalte, double Faktor)>();
        bool rundenIgnorieren = d.Rundenplanung == Rundenplanung.Halbrunde && !d.Doppelrunde;
        int rundenAnzahl = rundenIgnorieren ? 1 : d.Runden.Length;
        for (int t = 0; t < d.N; t++)
        {
            int spiele = Enumerable.Range(0, d.SpielHeim.Length).Count(s => d.SpielHeim[s] == t || d.SpielGast[s] == t);
            for (int r = 0; r < rundenAnzahl; r++)
            {
                if (!rundenIgnorieren && !d.RundeZuPlanen[r])
                {
                    continue;
                }

                double soll = rundenIgnorieren ? spiele * 0.25 : spiele * 0.5 / d.Runden.Length;
                if (!rundenIgnorieren && d.Rundenplanung == Rundenplanung.Halbrunde && d.Doppelrunde)
                {
                    soll = spiele * 0.25 / d.Runden.Length;
                }

                if (!rundenIgnorieren && d.HeimspieleRundeEins[t] >= 0)
                {
                    soll = r % 2 == 0 ? d.HeimspieleRundeEins[t] : d.N - d.HeimspieleRundeEins[t] - 1;
                }

                // Feste Heimspiele dieser Runde (nur Rückrunde) zählen mit.
                int fest = feste.Count(f => f.Heim == t && (rundenIgnorieren || d.RundeZuDatum(f.Datum) == r));
                List<int> heim = jeMannschaft[t].Where(i => d.SpielHeim[termine[i].Spiel] == t && (rundenIgnorieren || d.WunschRunde[termine[i].Wunsch] == r)).ToList();
                double rest = soll - Math.Floor(soll);
                int abw = modell.Zahl(0, heim.Count + fest + (int)Math.Ceiling(soll));

                // abw ≥ |h − soll| − Bruchteil (abgerundeter Betrag)
                modell.Zeile([(abw, 1.0), .. heim.Select(i => (termine[i].Spalte, -1.0))], fest - soll - rest, double.PositiveInfinity);
                modell.Zeile([(abw, 1.0), .. heim.Select(i => (termine[i].Spalte, 1.0))], soll - fest - rest, double.PositiveInfinity);
                ziel.Add((abw, 1));
            }
        }

        // Heimrecht: falsche Richtung je Spiel für die Mannschaft mit Vorgabe.
        foreach ((int spalte, int s, int w) in termine)
        {
            int h = d.SpielHeim[s];
            int g = d.SpielGast[s];
            int runde = rundenIgnorieren ? 0 : d.WunschRunde[w];
            int fehler = HeimrechtFehler(h, g, runde, istHeim: true) + HeimrechtFehler(g, h, runde, istHeim: false);
            if (fehler > 0)
            {
                ziel.Add((spalte, fehler));
            }
        }

        return ziel;
    }

    private int HeimrechtFehler(int t, int gegner, int runde, bool istHeim)
    {
        if (!d.HatHeimrechte[t])
        {
            return 0;
        }

        HeimrechtWert recht = d.HeimrechtGegen[(t * d.N) + gegner];
        if (recht == HeimrechtWert.Keins)
        {
            return 0;
        }

        bool zielHeim = runde % 2 == 0;
        if (recht == HeimrechtWert.Runde2)
        {
            zielHeim = !zielHeim;
        }

        return istHeim == zielHeim ? 0 : 1;
    }

    /// <summary>Wie <c>KernPlan.SperrtermineKosten</c>: je Mannschaft jedes Spiel (Heim oder Gast) an einem ihrer Sperrtage.</summary>
    private List<(int Spalte, double Faktor)> Sperrtermine() =>
        termine
            .Select(e =>
            {
                int tag = d.WunschTag[e.Wunsch];
                bool Gesperrt(int t) => d.IstInZuPlanenderRunde(tag) && Array.BinarySearch(d.Sperrtage[t], (double)tag) >= 0;
                return (e.Spalte, (double)((Gesperrt(d.SpielHeim[e.Spiel]) ? 1 : 0) + (Gesperrt(d.SpielGast[e.Spiel]) ? 1 : 0)));
            })
            .Where(e => e.Item2 > 0)
            .ToList();

    /// <summary>Wie <c>KernPlan.AusweichtermineKosten</c>: Heimspiele an einem Ausweichtag der Heimmannschaft.</summary>
    private List<(int Spalte, double Faktor)> Ausweichtermine() =>
        d.HatAusweichtermine
            ? termine
                .Where(e => Array.BinarySearch(d.Ausweichtage[d.SpielHeim[e.Spiel]], (double)d.WunschTag[e.Wunsch]) >= 0)
                .Select(e => (e.Spalte, 1.0))
                .ToList()
            : [];

    /// <summary>
    /// Wie <c>KernPlan.Koppeltermine</c>: je Mannschaft Soll (gemeldete Koppeltermine je Runde, halbiert und gedeckelt) minus
    /// gekoppelte Heimspiel-Paare, mindestens 0 (negative Zahlen zählt die Qualität nicht). Als Paar zählt jedes Paar belegter
    /// Koppel-Wunschtermine höchstens einen Tag auseinander (Lockerung: ohne Reihenfolge und Ausschlüsse).
    /// </summary>
    private List<(int Spalte, double Faktor)> Heimkoppel()
    {
        var ziel = new List<(int Spalte, double Faktor)>();
        if (!d.HatKoppeltermine)
        {
            return ziel;
        }

        int maxJeRunde = (d.N - 1) / 4;
        for (int t = 0; t < d.N; t++)
        {
            var hoch = new int[d.Runden.Length];
            var weich = new int[d.Runden.Length];
            var koppel = new List<int>();
            for (int w = d.WunschStart[t]; w < d.WunschStart[t + 1]; w++)
            {
                Wunschterminoptionen o = d.WunschOptionen[w];
                if (!Terminoptionen.IstKoppel(o) || !d.WunschInPlanung[w])
                {
                    continue;
                }

                koppel.Add(w);
                int runde = d.WunschRunde[w];
                if (runde < 0)
                {
                    continue;
                }

                hoch[runde] += Terminoptionen.IstKoppelHart(o) ? 1 : 0;
                weich[runde] += Terminoptionen.IstKoppelWeich(o) ? 1 : 0;
            }

            int soll = 0;
            for (int r = 0; r < d.Runden.Length; r++)
            {
                int h = hoch[r] / 2;
                int g = weich[r] / 2;
                if (h >= maxJeRunde)
                {
                    h = maxJeRunde;
                    g = maxJeRunde;
                }

                if (g + h > maxJeRunde)
                {
                    g = maxJeRunde - h;
                }

                soll += h + g;
            }

            if (soll <= 0)
            {
                continue;
            }

            // fehlend ≥ soll − Σ Paare; ein Paar nur, wenn beide Termine mit einem Heimspiel belegt sind.
            var zeile = new List<(int Spalte, double Faktor)>();
            int fehlend = modell.Zahl(0, soll);
            zeile.Add((fehlend, 1));
            List<int> belegt = koppel.Where(jeWunsch.ContainsKey).ToList();
            for (int i = 0; i < belegt.Count; i++)
            {
                for (int j = i + 1; j < belegt.Count; j++)
                {
                    int w1 = belegt[i];
                    int w2 = belegt[j];
                    if (Math.Abs(d.WunschTag[w1] - d.WunschTag[w2]) > 1 || Math.Abs(d.WunschDatum[w1] - d.WunschDatum[w2]) < DelphiDatum.MinGameDistance)
                    {
                        continue;
                    }

                    int paar = modell.Spalte(0);
                    zeile.Add((paar, 1));
                    modell.Zeile([(paar, 1.0), .. jeWunsch[w1].Select(x => (termine[x].Spalte, -1.0))], double.NegativeInfinity, 0);
                    modell.Zeile([(paar, 1.0), .. jeWunsch[w2].Select(x => (termine[x].Spalte, -1.0))], double.NegativeInfinity, 0);
                }
            }

            modell.Zeile(zeile, soll, double.PositiveInfinity);
            ziel.Add((fehlend, 1));
        }

        return ziel;
    }

    /// <summary>
    /// Wie <c>KernPlan.AuswaertsKoppeltermine</c>: Jeder Gegner aus den Auswärtskoppel-Wünschen einer Mannschaft hat einen
    /// Zähler (Runden / 2), den jede gekoppelte Auswärtsfahrt zu ihm um 1 senkt; Verstöße = Σ max(0, Zähler) / 2 (abgerundet).
    /// </summary>
    private List<(int Spalte, double Faktor)> Auswaertskoppel()
    {
        var ziel = new List<(int Spalte, double Faktor)>();
        if (!d.HatAuswaertsKoppeltermine)
        {
            return ziel;
        }

        int zielwert = d.Runden.Length / 2;
        for (int t = 0; t < d.N; t++)
        {
            (int A, int B, AuswaertsKoppelTyp Art)[] koppeln = d.Auswaertskoppeln[t].Where(k => k.A >= 0 && k.B >= 0).ToArray();
            int[] gegner = koppeln.SelectMany(k => new[] { k.A, k.B }).Distinct().ToArray();
            if (gegner.Length == 0 || zielwert <= 0)
            {
                continue;
            }

            // Auswärtsspiele von t je Gastgeber.
            ILookup<int, int> auswaerts = jeMannschaft[t].Where(i => d.SpielGast[termine[i].Spiel] == t).ToLookup(i => d.SpielHeim[termine[i].Spiel]);
            var fahrten = gegner.ToDictionary(g => g, _ => new List<int>());
            foreach ((int a, int b, AuswaertsKoppelTyp art) in koppeln)
            {
                foreach (int i in auswaerts[a])
                {
                    foreach (int j in auswaerts[b].Where(j => KoppelDatumPasst(art, d.WunschDatum[termine[i].Wunsch], d.WunschDatum[termine[j].Wunsch])))
                    {
                        int fahrt = modell.Spalte(0);
                        modell.Zeile([(fahrt, 1.0), (termine[i].Spalte, -1.0)], double.NegativeInfinity, 0);
                        modell.Zeile([(fahrt, 1.0), (termine[j].Spalte, -1.0)], double.NegativeInfinity, 0);
                        fahrten[a].Add(fahrt);
                        fahrten[b].Add(fahrt);
                    }
                }
            }

            // Feste gekoppelte Fahrten der Vorrunde (nur Rückrunde) senken die Zähler schon vorab (eher zu viele: Lockerung).
            var vorab = gegner.ToDictionary(g => g, _ => 0);
            FesterTermin[] festAuswaerts = feste.Where(f => f.Gast == t).ToArray();
            foreach ((int a, int b, AuswaertsKoppelTyp art) in koppeln)
            {
                int paare = festAuswaerts.Where(f => f.Heim == a).Sum(f1 => festAuswaerts.Count(f2 => f2.Heim == b && KoppelDatumPasst(art, f1.Datum, f2.Datum)));
                vorab[a] += paare;
                vorab[b] += paare;
            }

            // m_g ≥ zielwert − Fahrten zu g, m_g ≥ 0; 2·Verstöße ≥ Σ m_g − 1.
            int verstoesse = modell.Zahl(0, gegner.Length * zielwert);
            var summe = new List<(int Spalte, double Faktor)> { (verstoesse, 2) };
            foreach (int g in gegner)
            {
                int m = modell.Zahl(0, zielwert);
                modell.Zeile([(m, 1.0), .. fahrten[g].Select(f => (f, 1.0))], zielwert - vorab[g], double.PositiveInfinity);
                summe.Add((m, -1));
            }

            modell.Zeile(summe, -1, double.PositiveInfinity);
            ziel.Add((verstoesse, 1));
        }

        return ziel;
    }

    private bool GleicheHalle(int lokalA, int halleA, int lokalB, int halleB) =>
        d.Korrekturen.HallenNachNamen && halleA != 0 && halleB != 0 ? halleA == halleB : lokalA == lokalB;

    /// <summary>
    /// Wie <c>KernPlan.Hallenbelegung</c>: Je Heimspiel am Wunschtermin w mit höchstens m parallelen Spielen zählt
    /// max(0, Belegung − m + 1); Belegung = Heimspiele der Nachbarmannschaften (fest) und der Vereinsmannschaften im Plan in
    /// derselben Halle bis 1:29 h Abstand.
    /// </summary>
    private List<(int Spalte, double Faktor)> Hallenbelegung()
    {
        var ziel = new List<(int Spalte, double Faktor)>();
        for (int t = 0; t < d.N; t++)
        {
            if (d.Nachbarspiele[t].Count == 0 && d.Vereinsteams[t].Length == 0)
            {
                continue;
            }

            for (int w = d.WunschStart[t]; w < d.WunschStart[t + 1]; w++)
            {
                if (d.WunschParallele[w] <= 0 || !jeWunsch.TryGetValue(w, out List<int>? hier))
                {
                    continue;
                }

                int extern_ = ExterneHallenbelegung(t, w);
                List<int> intern = InterneHallenbelegung(t, w);
                int c = extern_ - d.WunschParallele[w] + 1;
                if (intern.Count == 0)
                {
                    if (c > 0)
                    {
                        ziel.AddRange(hier.Select(i => (termine[i].Spalte, (double)c)));
                    }

                    continue;
                }

                // p ≥ c·X + Σ intern − M·(1 − X) mit X = Termin w belegt, M = Zahl der internen Kandidaten.
                int m = intern.Count;
                int p = modell.Zahl(0, Math.Max(c, 0) + m);
                var zeile = new List<(int Spalte, double Faktor)> { (p, 1) };
                zeile.AddRange(hier.Select(i => (termine[i].Spalte, (double)-(c + m))));
                zeile.AddRange(intern.Select(i => (termine[i].Spalte, -1.0)));
                modell.Zeile(Zusammenfassen(zeile), -m, double.PositiveInfinity);
                ziel.Add((p, 1));
            }
        }

        return ziel;
    }

    private int ExterneHallenbelegung(int t, int w)
    {
        double datum = d.WunschDatum[w];
        return d.Nachbarspiele[t].TryGetValue(d.WunschTag[w], out Nachbarspiel[]? nachbarn)
            ? nachbarn.Count(n => n.IstHeimspiel && Math.Abs(n.Datum - datum) <= Hallenfenster && GleicheHalle(d.WunschLokal[w], d.WunschHalle[w], n.Lokal, n.Halle))
            : 0;
    }

    private List<int> InterneHallenbelegung(int t, int w)
    {
        double datum = d.WunschDatum[w];
        return d.Vereinsteams[t]
            .SelectMany(v => jeMannschaft[v].Where(i =>
            {
                (_, int s, int w2) = termine[i];
                return d.SpielHeim[s] == v && d.SpielGast[s] != t && Math.Abs(d.WunschDatum[w2] - datum) <= Hallenfenster
                    && GleicheHalle(d.WunschLokal[w2], d.WunschHalle[w2], d.WunschLokal[w], d.WunschHalle[w]);
            }))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Wie <c>KernPlan.ParalleleSpieleKosten</c>, aber nur sicher gezählte Fälle: alle festen Nachbarspiele (auch weitergereicht)
    /// und direkte Fälle zweier Vereinsmannschaften im Plan mit benachbarter Nummer (für beide Mannschaften je 1).
    /// </summary>
    private List<(int Spalte, double Faktor)> ParalleleSpiele()
    {
        var ziel = new List<(int Spalte, double Faktor)>();
        for (int t = 0; t < d.N; t++)
        {
            if (d.Nachbarspiele[t].Count == 0 && d.Vereinsteams[t].Length == 0)
            {
                continue;
            }

            foreach (int i in jeMannschaft[t])
            {
                int w = termine[i].Wunsch;
                int anzahl = ExternParallel(t, d.WunschDatum[w], d.WunschTag[w], d.Nummer[t], Suchrichtung.Beide);
                if (anzahl > 0)
                {
                    ziel.Add((termine[i].Spalte, anzahl));
                }
            }

            foreach (int v in d.Vereinsteams[t].Where(v => v > t && Math.Abs(d.Nummer[t] - d.Nummer[v]) == 1))
            {
                InternParallel(t, v, ziel);
            }
        }

        return ziel;
    }

    private void InternParallel(int t, int v, List<(int Spalte, double Faktor)> ziel)
    {
        ILookup<double, int> zeitenT = Zeiten(t, v);
        ILookup<double, int> zeitenV = Zeiten(v, t);
        foreach (IGrouping<double, int> a in zeitenT)
        {
            foreach (IGrouping<double, int> b in zeitenV.Where(b => Math.Abs(b.Key - a.Key) <= Parallelfenster))
            {
                // z ≥ X_t(a) + X_v(b) − 1
                int z = modell.Spalte(0);
                var zeile = new List<(int Spalte, double Faktor)> { (z, 1) };
                zeile.AddRange(a.Concat(b).Select(i => (termine[i].Spalte, -1.0)));
                modell.Zeile(Zusammenfassen(zeile), -1, double.PositiveInfinity);
                ziel.Add((z, 2));
            }
        }
    }

    /// <summary>Termin-Entscheidungen von <paramref name="t"/> ohne Spiele gegen <paramref name="ohne"/>, nach Zeitpunkt.</summary>
    private ILookup<double, int> Zeiten(int t, int ohne) =>
        jeMannschaft[t]
            .Where(i => d.SpielHeim[termine[i].Spiel] != ohne && d.SpielGast[termine[i].Spiel] != ohne)
            .ToLookup(i => d.WunschDatum[termine[i].Wunsch]);

    /// <summary>Wie <c>KernPlan.ParalleleSpiele</c>, nur mit den festen Nachbarspielen.</summary>
    private int ExternParallel(int t, double datum, int tag, int nummer, Suchrichtung richtung)
    {
        if (!d.Nachbarspiele[t].TryGetValue(tag, out Nachbarspiel[]? nachbarn))
        {
            return 0;
        }

        int anzahl = 0;
        foreach (Nachbarspiel spiel in nachbarn.Where(n => Math.Abs(n.Datum - datum) <= Parallelfenster))
        {
            int diff = nummer - spiel.Nummer;
            bool pruefen = richtung == Suchrichtung.Beide
                ? spiel.KeineParallelenSpiele
                : spiel.GleichesGeschlecht && ((diff == -1 && richtung == Suchrichtung.Tiefer) || (diff == 1 && richtung == Suchrichtung.Hoeher));
            if (!pruefen)
            {
                continue;
            }

            anzahl++;
            if (richtung is Suchrichtung.Beide or Suchrichtung.Tiefer)
            {
                anzahl += ExternParallel(t, datum, tag, spiel.Nummer, Suchrichtung.Tiefer);
            }

            if (richtung is Suchrichtung.Beide or Suchrichtung.Hoeher)
            {
                anzahl += ExternParallel(t, datum, tag, spiel.Nummer, Suchrichtung.Hoeher);
            }
        }

        return anzahl;
    }

    /// <summary>
    /// Wie <c>KernPlan.Pflichtspieltage</c>: fehlende Spiele je Mannschaft und Pflichtzeitraum. Mit der Regel für ungerade
    /// Mannschaftszahl darf je Zeitraum mit einem Spiel eine Mannschaft fehlen, die in der Woche davor gespielt hat.
    /// </summary>
    private List<(int Spalte, double Faktor)> Pflichtspieltage()
    {
        var ziel = new List<(int Spalte, double Faktor)>();
        foreach (RefPflichtzeit zeit in d.Pflichtzeiten.Where(z => d.IstInZuPlanenderRunde(z.Von)))
        {
            bool spielfrei = d.SpielfreiAmPflichtspieltag && zeit.AnzahlSpiele == 1;
            var frei = new List<int>();
            for (int t = 0; t < d.N; t++)
            {
                int fehlt = modell.Zahl(0, zeit.AnzahlSpiele);
                var zeile = new List<int> { fehlt };
                zeile.AddRange(jeMannschaft[t]
                    .Where(i => d.WunschTag[termine[i].Wunsch] >= zeit.Von && d.WunschTag[termine[i].Wunsch] <= zeit.Bis)
                    .Select(i => termine[i].Spalte));
                if (spielfrei)
                {
                    // Spielfrei sein darf nur, wer in der Woche davor gespielt hat.
                    int e = modell.Spalte(0);
                    zeile.Add(e);
                    frei.Add(e);
                    var davor = new List<(int Spalte, double Faktor)> { (e, 1) };
                    davor.AddRange(jeMannschaft[t]
                        .Where(i => d.WunschTag[termine[i].Wunsch] >= zeit.Von - 7 && d.WunschTag[termine[i].Wunsch] <= zeit.Von - 1)
                        .Select(i => (termine[i].Spalte, -1.0)));
                    modell.Zeile(davor, double.NegativeInfinity, 0);
                }

                modell.Zeile(zeile, zeit.AnzahlSpiele, double.PositiveInfinity);
                ziel.Add((fehlt, 1));
            }

            if (frei.Count > 0)
            {
                modell.Zeile(frei, 0, 1);
            }
        }

        return ziel;
    }

    /// <summary>
    /// Wie <c>KernPlan.TeamsNichtAmAnfang</c>: je Mannschaft die Spiele gegen fremde Vereine, die in ihrer Runde vor einem
    /// Spiel gegen eine Vereinsmannschaft liegen. Gleichzeitige Spiele zählen nicht (Lockerung); fehlt einer Mannschaft in
    /// einer Runde ein Vereinsspiel ganz, zählt das Original alle fremden Spiele – das Modell nur die davor (Lockerung).
    /// </summary>
    private List<(int Spalte, double Faktor)> VereinsinterneAmAnfang()
    {
        var ziel = new List<(int Spalte, double Faktor)>();
        for (int t = 0; t < d.N; t++)
        {
            if (d.Vereinsteams[t].Length == 0)
            {
                continue;
            }

            for (int r = 0; r < d.Runden.Length; r++)
            {
                if (d.RundeZuPlanen[r])
                {
                    VereinsinternInRunde(t, d.Runden[r], ziel);
                }
            }
        }

        return ziel;
    }

    private void VereinsinternInRunde(int t, RefRunde runde, List<(int Spalte, double Faktor)> ziel)
    {
        int n = d.N;
        List<int> inRunde = jeMannschaft[t].Where(i => runde.Enthaelt(d.WunschDatum[termine[i].Wunsch])).ToList();
        bool Verein(int i) => d.GleicherVerein[(t * n) + d.SpielHeim[termine[i].Spiel]] || d.GleicherVerein[(t * n) + d.SpielGast[termine[i].Spiel]];
        if (!inRunde.Exists(Verein) || inRunde.TrueForAll(Verein))
        {
            return;
        }

        double[] zeiten = inRunde.Select(i => d.WunschDatum[termine[i].Wunsch]).Distinct().Order().ToArray();
        int k = zeiten.Length;

        // b[j]: ein Vereinsspiel nach Zeitpunkt j; je fremdes Spiel zum Zeitpunkt j: z ≥ x + b[j] − 1 (gezählt, wenn danach
        // noch ein Vereinsspiel kommt).
        int[] b = Enumerable.Range(0, k).Select(_ => modell.Spalte(0)).ToArray();
        foreach (int i in inRunde.Where(Verein))
        {
            int j = Array.BinarySearch(zeiten, d.WunschDatum[termine[i].Wunsch]);
            if (j > 0)
            {
                modell.Zeile([(b[j - 1], 1.0), (termine[i].Spalte, -1.0)], 0, double.PositiveInfinity);
            }
        }

        for (int j = 0; j + 1 < k; j++)
        {
            modell.Zeile([(b[j], 1.0), (b[j + 1], -1.0)], 0, double.PositiveInfinity);
        }

        foreach (int i in inRunde.Where(i => !Verein(i)))
        {
            int j = Array.BinarySearch(zeiten, d.WunschDatum[termine[i].Wunsch]);
            int z = modell.Spalte(0);
            modell.Zeile([(z, 1.0), (termine[i].Spalte, -1.0), (b[j], -1.0)], -1, double.PositiveInfinity);
            ziel.Add((z, 1));
        }
    }

    private List<Spiel> Plan(double[]? werte)
    {
        if (werte is null)
        {
            return [];
        }

        var plan = ohneTermin
            .Where(u => werte[u.Spalte] > 0.5)
            .Select(u => new Spiel(null, d.Name[d.SpielHeim[u.Spiel]], d.Name[d.SpielGast[u.Spiel]], string.Empty))
            .Concat(feste.Select(f => new Spiel(DelphiDatum.ZuDateTime(f.Datum), d.Name[f.Heim], d.Name[f.Gast], string.Empty)))
            .ToList();

        foreach ((_, int s, int w) in termine.Where(e => werte[e.Spalte] > 0.5))
        {
            plan.Add(new Spiel(DelphiDatum.ZuDateTime(d.WunschDatum[w]), d.Name[d.SpielHeim[s]], d.Name[d.SpielGast[s]], d.WunschRef[w].Spiellokal));
        }

        return plan;
    }
}
