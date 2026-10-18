// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Schranke;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>
/// Zustand des Schaubilds im Leitstand: je Insel ihr Wert und grob, was sie gerade tut (kommt voran, sucht, steckt
/// fest, beginnt neu), dazu die wenigen Ereignisse, die kurz aufleuchten: ein besserer bester Plan (von seiner Insel über
/// den Koordinator), ein Neustart, die Übergabe der Spezial-Insel und eine Anhebung des Automodus. Die Ereignisse ergeben
/// sich aus dem Vergleich zweier aufeinanderfolgender Stände. Bei einem besseren besten Plan steht kurz, was sich geändert
/// hat: bei der Kostenoptimierung die Kriterien mit den größten Kostendifferenzen, im Automodus die wichtigsten Änderungen
/// der Verstöße (Stufe A zuerst) – Verbesserungen wie Verschlechterungen.
/// </summary>
public sealed class Schaubild : ObservableObject
{
    /// <summary>
    /// So lange (Sekunden) wandert der grüne Punkt von der Insel zum besten Plan; erst wenn er ankommt, zeigt das Schaubild,
    /// was sich geändert hat.
    /// </summary>
    public const double Wanderzeit = 1.4;

    /// <summary>So lange (Sekunden) nach der Ankunft des letzten grünen Punkts steht, was sich geändert hat.</summary>
    public const double Anzeigedauer = 10;

    /// <summary>So lange (Sekunden) bleibt ein Ereignis in der Liste; länger dauert keine Animation.</summary>
    private const double Lebensdauer = 3;

    /// <summary>So lange (Sekunden) nach einer Verbesserung gilt eine Insel als „kommt voran“.</summary>
    private const double Voran = 5;

    /// <summary>Ab diesem Anteil des Wegs bis zum Neustart gilt eine Insel als festgefahren.</summary>
    private const double Fest = 0.6;

    /// <summary>So viele Kriterien zeigt die Änderung eines besseren besten Plans höchstens.</summary>
    private const int AenderungenHoechstens = 4;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private readonly List<Schaubildimpuls> impulse = [];
    private readonly List<Verbesserungsmeldung> meldungen = [];
    private Maschinenraum? vorher;
    private IReadOnlyList<Kriterienwert>? kriterienVorher;
    private long[] zuletztVoran = [];
    private int besterPlanVorher = -1;
    private int anhebungenVorher = -1;
    private int vonHighsVorher = -1;
    private int wechselVorher = -1;
    private string lenkungName = "Koordinator";
    private string lenkungText = string.Empty;
    private string herkunft = string.Empty;
    private Schrankenfortschritt? schranke;
    private Teilneuplanungsstand? teilneuplanung;
    private Prozessorlast? last;
    private int kerne;
    private bool spezial;
    private bool rechnet;
    private string bester = "–";
    private IReadOnlyList<Inselzustand> inseln = [];
    private int besteInsel = -1;
    private int version;

    /// <summary>Holt die Zahl der Rechenkerne (0 = keine Generierung).</summary>
    public int Kerne
    {
        get => kerne;
        private set => SetProperty(ref kerne, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob es eine Spezial-Insel gibt (Kostenoptimierung).</summary>
    public bool Spezial
    {
        get => spezial;
        private set => SetProperty(ref spezial, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob gerade gerechnet wird.</summary>
    public bool Rechnet
    {
        get => rechnet;
        private set => SetProperty(ref rechnet, value);
    }

    /// <summary>Holt den Namen des Kastens, der vergleicht und lenkt: „Automodus“ oder „Koordinator“.</summary>
    public string Lenkung
    {
        get => lenkungName;
        private set => SetProperty(ref lenkungName, value);
    }

    /// <summary>Holt, woran der Automodus gerade arbeitet (z. B. „Stufe C: 3-Tage-Abstand“) bzw. was der Koordinator tut.</summary>
    public string LenkungText
    {
        get => lenkungText;
        private set => SetProperty(ref lenkungText, value);
    }

    /// <summary>
    /// Holt, woher die Verbesserungen des besten Plans im Automodus kommen, z. B. <c>Inseln 23 · HiGHS 8</c>; leer bei der
    /// Kostenoptimierung.
    /// </summary>
    public string Herkunft
    {
        get => herkunft;
        private set => SetProperty(ref herkunft, value);
    }

    /// <summary>Holt, woher der beste Plan zuletzt kam (z. B. „Insel 3“ oder „HiGHS“); <c>null</c>, wenn unbekannt.</summary>
    public string? ZuletztVon { get; private set; }

    /// <summary>Holt den Wert des besten Plans (Kosten bzw. Verstöße A, B und C).</summary>
    public string Bester
    {
        get => bester;
        private set => SetProperty(ref bester, value);
    }

    /// <summary>Holt je Insel (ohne Spezial-Insel) ihren Zustand.</summary>
    public IReadOnlyList<Inselzustand> Inseln
    {
        get => inseln;
        private set => SetProperty(ref inseln, value);
    }

    /// <summary>Holt den Index der Insel mit dem besten Plan; −1, wenn keine.</summary>
    public int BesteInsel
    {
        get => besteInsel;
        private set => SetProperty(ref besteInsel, value);
    }

    /// <summary>
    /// Holt je grünem Punkt der letzten Sekunden, was seine Verbesserung geändert hat (ältester zuerst). Zu sehen ist die
    /// Meldung des zuletzt angekommenen Punkts (<see cref="Wanderzeit"/> nach ihrem Zeitpunkt).
    /// </summary>
    public IReadOnlyList<Verbesserungsmeldung> Meldungen => meldungen;

    /// <summary>Holt die Ereignisse der letzten Sekunden.</summary>
    public IReadOnlyList<Schaubildimpuls> Impulse => impulse;

    /// <summary>Holt, woran HiGHS für die untere Schranke gerade rechnet; <c>null</c>, wenn es nicht rechnet.</summary>
    public Schrankenfortschritt? Schranke
    {
        get => schranke;
        private set
        {
            if (SetProperty(ref schranke, value))
            {
                OnPropertyChanged(nameof(ZeigtHighs));
            }
        }
    }

    /// <summary>Holt den Stand der Teilneuplanung mit HiGHS; <c>null</c>, wenn sie nicht läuft.</summary>
    public Teilneuplanungsstand? Teilneuplanung
    {
        get => teilneuplanung;
        private set
        {
            if (SetProperty(ref teilneuplanung, value))
            {
                OnPropertyChanged(nameof(ZeigtHighs));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob das Schaubild HiGHS zeigt (untere Schranke oder Teilneuplanung).</summary>
    public bool ZeigtHighs => Schranke is not null || Teilneuplanung is not null;

    /// <summary>Holt, wie sich der Prozessor gerade aufteilt (für den Anteil von HiGHS); <c>null</c> ohne Messung.</summary>
    public Prozessorlast? Last
    {
        get => last;
        private set => SetProperty(ref last, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob HiGHS gerade rechnet (untere Schranke oder Teilneuplanung).</summary>
    public bool HighsRechnet => Schranke is not null || Teilneuplanung is { Rechnet: true };

    /// <summary>Holt eine Zahl, die sich mit jedem Stand ändert (zum Neuzeichnen).</summary>
    public int Version
    {
        get => version;
        private set => SetProperty(ref version, value);
    }

    /// <summary>
    /// Was sich zwischen zwei besten Plänen geändert hat: bei der Kostenoptimierung die größten Kostendifferenzen, im
    /// Automodus die Änderungen der Verstöße, wichtigste Stufe zuerst. Leer, wenn die beiden nicht vergleichbar sind.
    /// </summary>
    /// <param name="alt">Die Kriterien des vorigen besten Plans.</param>
    /// <param name="neu">Die Kriterien des jetzigen besten Plans.</param>
    /// <returns>Höchstens vier Änderungen.</returns>
    public static IReadOnlyList<Planaenderung> Vergleichen(IReadOnlyList<Kriterienwert>? alt, IReadOnlyList<Kriterienwert>? neu)
    {
        if (!Vergleichbar(alt, neu))
        {
            return [];
        }

        bool automodus = neu[0].Stufe is not null;
        Dictionary<Kostenkriterium, double> vorige = alt.ToDictionary(k => k.Kriterium, k => k.Wert);
        var unterschiede = neu
            .Select(k => (Neu: k, Alt: vorige.GetValueOrDefault(k.Kriterium)))
            .Where(u => Math.Abs(u.Neu.Wert - u.Alt) >= (automodus ? 0.5 : 0.005));
        unterschiede = automodus
            ? unterschiede.OrderBy(u => u.Neu.Stufe).ThenByDescending(u => Math.Abs(u.Neu.Wert - u.Alt))
            : unterschiede.OrderByDescending(u => Math.Abs(u.Neu.Wert - u.Alt));
        return unterschiede.Take(AenderungenHoechstens).Select(u => Aenderung(u.Neu, u.Alt, automodus)).ToArray();
    }

    /// <summary>Gibt an, ob sich zwei beste Pläne vergleichen lassen (beide nach Kosten oder beide nach Verstößen).</summary>
    /// <param name="alt">Die Kriterien des vorigen besten Plans.</param>
    /// <param name="neu">Die Kriterien des jetzigen besten Plans.</param>
    /// <returns><c>true</c>, wenn sie vergleichbar sind.</returns>
    public static bool Vergleichbar([NotNullWhen(true)] IReadOnlyList<Kriterienwert>? alt, [NotNullWhen(true)] IReadOnlyList<Kriterienwert>? neu) =>
        alt is { Count: > 0 } && neu is { Count: > 0 } && (alt[0].Stufe is null) == (neu[0].Stufe is null);

    /// <summary>Zeigt, woran HiGHS für die untere Schranke gerade rechnet.</summary>
    /// <param name="fortschritt">Der Schritt; <c>null</c>, wenn HiGHS fertig ist.</param>
    public void SchrankeZeigen(Schrankenfortschritt? fortschritt)
    {
        Schranke = fortschritt;
        Version++;
    }

    /// <summary>Übernimmt den Stand und leitet die Ereignisse seit dem letzten Stand ab.</summary>
    /// <param name="stand">Der Stand.</param>
    /// <param name="karten">Die Inseln, wie sie die Karten zeigen (Werte und beste Insel).</param>
    public void Uebernehmen(Optimierungsstand stand, IReadOnlyList<Inselanzeige> karten)
    {
        ArgumentNullException.ThrowIfNull(stand);
        ArgumentNullException.ThrowIfNull(karten);
        long jetzt = Stopwatch.GetTimestamp();
        impulse.RemoveAll(i => Stopwatch.GetElapsedTime(i.Zeitpunkt, jetzt).TotalSeconds > Lebensdauer);
        meldungen.RemoveAll(m => Stopwatch.GetElapsedTime(m.Zeitpunkt, jetzt).TotalSeconds > Wanderzeit + Anzeigedauer + 1);
        Rechnet = stand.Laeuft && !stand.Pausiert;
        Teilneuplanung = stand.Teilneuplanung;
        Last = stand.Last;
        LenkungZeigen(stand.Lenkung);
        if (stand.Maschinenraum is not Maschinenraum raum)
        {
            vorher = null;
            vonHighsVorher = -1;
            wechselVorher = -1;
            kriterienVorher = null;
            meldungen.Clear();
            besterPlanVorher = -1;
            anhebungenVorher = -1;
            Kerne = 0;
            Version++;
            return;
        }

        Inselstand[] normale = raum.Inseln.Where(i => !i.Spezial).ToArray();
        if (zuletztVoran.Length != normale.Length)
        {
            zuletztVoran = new long[normale.Length];
        }

        Kerne = raum.Kerne.Count;
        Spezial = raum.Inseln.Any(i => i.Spezial);
        BesteInsel = Enumerable.Range(0, karten.Count).FirstOrDefault(i => karten[i].IstBeste && !karten[i].IstSpezial, -1);
        Bester = BesterText(stand);
        if (Rechnet)
        {
            EreignisseAbleiten(stand, raum, jetzt);
        }

        Inseln = normale.Select((insel, i) => Zustand(insel, i < karten.Count ? karten[i] : null, raum.DurchlaeufeVorNeustart, i, jetzt)).ToArray();
        vorher = raum;
        kriterienVorher = raum.BesterPlan;
        besterPlanVorher = raum.BesterPlanNummer;
        anhebungenVorher = stand.Lenkung?.Anhebungen ?? -1;
        vonHighsVorher = stand.Lenkung?.VonHighs ?? -1;
        wechselVorher = stand.Teilneuplanung?.Wechsel ?? -1;
        Version++;
    }

    private static string BesterText(Optimierungsstand stand)
    {
        if (stand.Lenkung is { Basis: not null } lenkung)
        {
            return string.Create(Deutsch, $"A {lenkung.Bester.A} · B {lenkung.Bester.B} · C {lenkung.Bester.CAnzahl}");
        }

        // Basisoptimierung im Automodus: schon die Verstöße je Stufe (des besten Plans nach Kosten).
        if (stand.Lenkung is not null && stand.Maschinenraum?.BesterPlan is { Count: > 0 } kriterien && kriterien[0].Stufe is not null)
        {
            double Summe(Stufe stufe) => kriterien.Where(k => k.Stufe == stufe).Sum(k => k.Wert);
            return string.Create(Deutsch, $"A {Summe(Stufe.A):0} · B {Summe(Stufe.B):0} · C {Summe(Stufe.C):0}");
        }

        return stand.Kosten >= 0 ? Kostenanzeige.Kurz(stand.Kosten) : "–";
    }

    private static Planaenderung Aenderung(Kriterienwert neu, double alt, bool automodus)
    {
        double differenz = neu.Wert - alt;
        string name = Planqualitaet.Name(neu.Kriterium);
        string text = automodus
            ? string.Create(Deutsch, $"{neu.Stufe} · {name}  {alt:0} → {neu.Wert:0}")
            : $"{name}  {Vorzeichen(differenz)}{Kostenanzeige.Kurz(Math.Abs(differenz))}";
        return new Planaenderung(text, differenz < 0);
    }

    private static string Vorzeichen(double differenz) => differenz < 0 ? "−" : "+";

    private static string Wert(Inselanzeige? karte) => karte switch
    {
        null => string.Empty,
        { ZeigtVerstoesse: true } => $"{karte.VerstossA} · {karte.VerstossB} · {karte.VerstossC}",
        _ => karte.Kosten,
    };

    /// <summary>Womit eine Insel neu beginnt, kurz (für ihre Karte).</summary>
    /// <param name="start">Das Startverfahren.</param>
    /// <returns>Z. B. <c>Zeitfenster neu</c>.</returns>
    private static string Starttext(Startverfahren start) => start switch
    {
        Startverfahren.Leer => "leerer Plan",
        Startverfahren.Mannschaften => "eine Mannschaft neu",
        Startverfahren.Zeitfenster => "eine Woche neu",
        Startverfahren.Spiele => "einige Spiele neu",
        _ => "aus Teilneuplanung",
    };

    /// <summary>Grob, was eine Insel gerade tut.</summary>
    private Inselzustand Zustand(Inselstand insel, Inselanzeige? karte, long grenze, int index, long jetzt)
    {
        double stillstand = Math.Clamp(insel.OhneVerbesserung / (double)Math.Max(1, grenze), 0, 1);
        bool neu = impulse.Exists(i => i.Art == Schaubildereignis.Neustart && i.Insel == index);
        bool voran = zuletztVoran[index] != 0 && Stopwatch.GetElapsedTime(zuletztVoran[index], jetzt).TotalSeconds < Voran;
        string taetigkeit = (neu, voran, stillstand) switch
        {
            // Wie die Karte der Insel: Wer über der Grenze die schlechteste ist, beginnt gleich neu, auch wenn sie eben
            // noch verbessert hat (bei vielen Plänen je Sekunde ist die Grenze schnell wieder erreicht).
            (true, _, _) => insel.Start is { } start ? "beginnt neu: " + Starttext(start) : "beginnt neu",
            (_, _, >= 1) when insel.Schlechteste => "beginnt gleich neu",
            (_, true, _) => "kommt voran",
            (_, _, >= 1) => "steckt fest, rechnet weiter",
            (_, _, >= Fest) => "findet lange nichts",
            _ => "sucht",
        };
        return new Inselzustand(Wert(karte), taetigkeit, stillstand);
    }

    /// <summary>
    /// Merkt sich zum grünen Punkt einer Verbesserung, was sie gegenüber dem vorigen besten Plan geändert hat; angezeigt
    /// wird es, wenn dieser Punkt ankommt.
    /// </summary>
    /// <param name="neu">Die Kriterien des jetzigen besten Plans.</param>
    /// <param name="anzahl">So viele Verbesserungen seit dem vorigen Stand (meist eine).</param>
    /// <param name="letzte">Nummer der letzten, wie sie die Kachel „Verbesserungen“ zeigt.</param>
    /// <param name="jetzt">Zeitstempel, zugleich Start des grünen Punkts.</param>
    private void AenderungMerken(IReadOnlyList<Kriterienwert>? neu, int anzahl, int letzte, long jetzt)
    {
        IReadOnlyList<Planaenderung> liste = Vergleichen(kriterienVorher, neu);
        string titel = anzahl == 1
            ? string.Create(Deutsch, $"Verbesserung {letzte}")
            : string.Create(Deutsch, $"Verbesserungen {letzte - anzahl + 1}–{letzte}");
        meldungen.Add(new Verbesserungsmeldung(titel, liste.Count > 0 ? liste : [new Planaenderung("Feinschliff, Kriterien fast unverändert", true)], jetzt));
    }

    private void EreignisseAbleiten(Optimierungsstand stand, Maschinenraum raum, long jetzt)
    {
        if (vorher is { } alt && alt.Inseln.Count == raum.Inseln.Count)
        {
            int normal = 0;
            for (int i = 0; i < raum.Inseln.Count; i++)
            {
                Inselstand neu = raum.Inseln[i];
                if (neu.Spezial)
                {
                    continue;
                }

                if (neu.Neustarts > alt.Inseln[i].Neustarts)
                {
                    impulse.Add(new Schaubildimpuls(Schaubildereignis.Neustart, normal, jetzt));
                }
                else if (neu.Erfolge > alt.Inseln[i].Erfolge)
                {
                    zuletztVoran[normal] = jetzt;
                }

                normal++;
            }

            if (raum.SpezialVerwendet > alt.SpezialVerwendet)
            {
                int ziel = impulse.LastOrDefault(i => i.Art == Schaubildereignis.Neustart && i.Zeitpunkt == jetzt)?.Insel ?? 0;
                impulse.Add(new Schaubildimpuls(Schaubildereignis.Uebergabe, ziel, jetzt));
            }
        }

        // Nur ein besserer bester Plan wandert: von seiner Herkunft (Insel oder HiGHS) zum besten Plan. Die Nummer des besten
        // Plans ist dieselbe Zahl wie in der Kachel „Verbesserungen“ (ohne die Läufe vor einer Datenänderung); zu jedem
        // Anstieg wandert ein Punkt, und seine Meldung nennt die Nummern, die Kachel und Verlauf zeigen.
        if (besterPlanVorher >= 0 && raum.BesterPlanNummer > besterPlanVorher)
        {
            bool vonHighs = stand.Lenkung is { } l && l.VonHighs > vonHighsVorher && vonHighsVorher >= 0;
            int insel = stand.Lenkung?.ZuletztVon is { } von && von.StartsWith("Insel ", StringComparison.Ordinal)
                && int.TryParse(von.AsSpan(6), NumberStyles.Integer, CultureInfo.InvariantCulture, out int nummer)
                ? nummer - 1
                : BesteInsel;
            impulse.Add(vonHighs
                ? new Schaubildimpuls(Schaubildereignis.Teilneuplanung, -1, jetzt)
                : new Schaubildimpuls(Schaubildereignis.BesterPlan, insel, jetzt));
            AenderungMerken(raum.BesterPlan, raum.BesterPlanNummer - besterPlanVorher, stand.Verbesserungen, jetzt);
        }

        if (anhebungenVorher >= 0 && stand.Lenkung is { } lenkung && lenkung.Anhebungen > anhebungenVorher)
        {
            impulse.Add(new Schaubildimpuls(Schaubildereignis.Anhebung, -1, jetzt));
        }

        if (wechselVorher >= 0 && stand.Teilneuplanung is { } w && w.Wechsel > wechselVorher)
        {
            impulse.Add(new Schaubildimpuls(Schaubildereignis.Ausgangsplan, -1, jetzt));
        }
    }

    /// <summary>Der Kasten in der Mitte oben: im Automodus Phase und Kriterien, sonst der Koordinator.</summary>
    private void LenkungZeigen(Lenkungsstand? stand)
    {
        if (stand is null)
        {
            Lenkung = "Koordinator";
            LenkungText = "vergleicht die Kosten, startet Inseln neu";
            Herkunft = string.Empty;
            ZuletztVon = null;
            return;
        }

        Lenkung = "Automodus";
        string phase = stand.Bezeichnung.Replace("Optimierung ", string.Empty, StringComparison.Ordinal);
        string kriterien = string.Join(", ", (stand.Kriterien ?? []).Select(Planqualitaet.Name));
        LenkungText = kriterien.Length > 0 ? $"{phase}: {kriterien}" : phase;
        Herkunft = stand.Basis is null ? string.Empty : string.Create(Deutsch, $"Inseln {stand.VonInseln} · HiGHS {stand.VonHighs}");
        ZuletztVon = stand.ZuletztVon;
    }
}
