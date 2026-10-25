// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Kacheln mit dem Stand der Generierung im Kopf des Hauptfensters: Kosten, Verbesserungen, Tempo, Pflichtregeln.</summary>
public sealed class Generierungsanzeige : ObservableObject
{
    /// <summary>So lange reicht der Verlauf der Prozessorlast im Leitstand zurück.</summary>
    public static readonly TimeSpan Verlaufsdauer = TimeSpan.FromMinutes(2);

    private const string Leer = "–";

    private const string SeitStart = "seit dem ersten gültigen Plan";

    private const string SeitAnpassung = "seit der letzten Kostenanpassung";

    private const string TitelKosten = "Gesamtkosten (bester Plan)";

    /// <summary>Ab diesen Kosten hat ein Plan mindestens einen harten Fehler (Original: 10 × <c>cMaxKostenNoHardError</c>).</summary>
    private const double HarteFehler = 10.0 * 1_000_000_000.0;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private double? startkosten;
    private string bezug = SeitStart;
    private string kosten = Leer;
    private string kostenHinweis = "Generierung noch nicht gestartet";
    private string verbesserungen = Leer;
    private string letzteVerbesserung = string.Empty;
    private string plaene = Leer;
    private string tempo = string.Empty;
    private string pflicht = Leer;
    private string pflichtHinweis = string.Empty;
    private bool pflichtErfuellt = true;
    private bool zeigtVerstoesse;
    private string titel = TitelKosten;
    private string verstossA = Leer;
    private string verstossB = Leer;
    private string verstossC = Leer;
    private string basisA = string.Empty;
    private string basisB = string.Empty;
    private string basisC = string.Empty;
    private string phase = string.Empty;
    private Stufenwert? verstoesse;
    private Prozessorlast? last;
    private Prozessorlast? letzteMessung;
    private IReadOnlyList<Lastpunkt> lastverlauf = [];
    private Highsanzeige? highs;

    /// <summary>Wird ausgelöst, wenn zwischen Kosten und Verstößen umgeschaltet wird (Ziel des Klicks auf die Kachel).</summary>
    public event EventHandler? ZeigtVerstoesseGeaendert;

    /// <summary>Holt die Kosten des besten Plans.</summary>
    public string Kosten
    {
        get => kosten;
        private set => SetProperty(ref kosten, value);
    }

    /// <summary>Holt die Veränderung seit dem Start bzw. den Zustand der Generierung.</summary>
    public string KostenHinweis
    {
        get => kostenHinweis;
        private set => SetProperty(ref kostenHinweis, value);
    }

    /// <summary>Holt die Anzahl der Verbesserungen.</summary>
    public string Verbesserungen
    {
        get => verbesserungen;
        private set => SetProperty(ref verbesserungen, value);
    }

    /// <summary>Holt die Zeit seit der letzten Verbesserung.</summary>
    public string LetzteVerbesserung
    {
        get => letzteVerbesserung;
        private set => SetProperty(ref letzteVerbesserung, value);
    }

    /// <summary>Holt die Anzahl der berechneten Pläne.</summary>
    public string Plaene
    {
        get => plaene;
        private set => SetProperty(ref plaene, value);
    }

    /// <summary>Holt das Tempo in Plänen je Sekunde.</summary>
    public string Tempo
    {
        get => tempo;
        private set => SetProperty(ref tempo, value);
    }

    /// <summary>Holt den Zustand der Pflichtregeln (Stufe A) des besten Plans.</summary>
    public string Pflicht
    {
        get => pflicht;
        private set => SetProperty(ref pflicht, value);
    }

    /// <summary>Holt die Zahl der übrigen Kriterien mit Verstößen.</summary>
    public string PflichtHinweis
    {
        get => pflichtHinweis;
        private set => SetProperty(ref pflichtHinweis, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob alle Pflichtregeln erfüllt sind.</summary>
    public bool PflichtErfuellt
    {
        get => pflichtErfuellt;
        private set => SetProperty(ref pflichtErfuellt, value);
    }

    /// <summary>Holt die Überschrift der Kachel: Verfahren und was gezeigt wird.</summary>
    public string Titel
    {
        get => titel;
        private set => SetProperty(ref titel, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob statt der Kosten die Verstöße A, B und C gezeigt werden (Automodus).</summary>
    public bool ZeigtVerstoesse
    {
        get => zeigtVerstoesse;
        private set
        {
            if (SetProperty(ref zeigtVerstoesse, value))
            {
                ZeigtVerstoesseGeaendert?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Holt die Zahl der Verstöße der Stufe A des besten Plans.</summary>
    public string VerstossA
    {
        get => verstossA;
        private set => SetProperty(ref verstossA, value);
    }

    /// <summary>Holt die Zahl der Verstöße der Stufe B des besten Plans.</summary>
    public string VerstossB
    {
        get => verstossB;
        private set => SetProperty(ref verstossB, value);
    }

    /// <summary>Holt die Zahl der Verstöße der Stufe C des besten Plans.</summary>
    public string VerstossC
    {
        get => verstossC;
        private set => SetProperty(ref verstossC, value);
    }

    /// <summary>Holt die Verstöße der Stufe A am Ende der Basisoptimierung mit Pfeil, z. B. <c>14 →</c>; leer davor.</summary>
    public string BasisA
    {
        get => basisA;
        private set => SetProperty(ref basisA, value);
    }

    /// <summary>Holt die Verstöße der Stufe B am Ende der Basisoptimierung mit Pfeil; leer davor.</summary>
    public string BasisB
    {
        get => basisB;
        private set => SetProperty(ref basisB, value);
    }

    /// <summary>Holt die Verstöße der Stufe C am Ende der Basisoptimierung mit Pfeil; leer davor.</summary>
    public string BasisC
    {
        get => basisC;
        private set => SetProperty(ref basisC, value);
    }

    /// <summary>
    /// Holt die Kriterien, an denen der Automodus gerade arbeitet, alle mit Namen, z. B. <c>Hallenbelegung, Pflichtspieltage</c>
    /// oder <c>Spielverteilung</c> (unten in der Kachel); leer in der Basisoptimierung und bei der Kostenoptimierung.
    /// </summary>
    public string Phase
    {
        get => phase;
        private set => SetProperty(ref phase, value);
    }

    /// <summary>
    /// Holt, wie sich der ganze Prozessor gerade aufteilt (Suche, HiGHS, Rest); <c>null</c>, wenn nicht gemessen werden kann.
    /// HiGHS hat keinen eigenen Kern: Es rechnet auf einem eigenen Faden und teilt sich den Prozessor mit der Suche.
    /// </summary>
    public Prozessorlast? Last
    {
        get => last;
        private set
        {
            if (SetProperty(ref last, value))
            {
                OnPropertyChanged(nameof(ZeigtLast));
                OnPropertyChanged(nameof(Auslastung));
                OnPropertyChanged(nameof(Prozessorzeilen));
                OnPropertyChanged(nameof(SkalaMitte));
                OnPropertyChanged(nameof(SkalaEnde));
            }
        }
    }

    /// <summary>Holt, woran HiGHS gerade rechnet (rechte Hälfte der Kachel „Berechnete Pläne“); <c>null</c> außerhalb des Automodus.</summary>
    public Highsanzeige? Highs
    {
        get => highs;
        private set
        {
            if (SetProperty(ref highs, value))
            {
                OnPropertyChanged(nameof(ZeigtHighs));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob die Kachel „Berechnete Pläne“ HiGHS zeigt.</summary>
    public bool ZeigtHighs => Highs is not null;

    /// <summary>Holt einen Wert, der angibt, ob die Aufteilung des Prozessors gemessen wird.</summary>
    public bool ZeigtLast => Last is not null;

    /// <summary>Holt, wie stark der ganze Prozessor ausgelastet ist, z. B. <c>93 %</c>.</summary>
    public string Auslastung => Last is { } l ? (1 - l.Frei).ToString("P0", Deutsch) : string.Empty;

    /// <summary>Holt die Mitte der Skala unter der Prozessorleiste, z. B. <c>6 Kerne</c>.</summary>
    public string SkalaMitte => Last is { Prozessoren: > 2 } l ? string.Create(Deutsch, $"{l.Prozessoren / 2.0:0.#} Kerne") : string.Empty;

    /// <summary>Holt das Ende der Skala unter der Prozessorleiste, z. B. <c>12 Kerne</c>.</summary>
    public string SkalaEnde => Last is { } l ? string.Create(Deutsch, $"{l.Prozessoren} Kerne") : string.Empty;

    /// <summary>Holt die Teile des Prozessors (Suche, HiGHS, Rest, frei) mit ihrem Anteil in Kernen.</summary>
    public IReadOnlyList<Prozessorzeile> Prozessorzeilen => Last is { } l
        ?
        [
            new("Suche", l.Suche, 0, 0, 0, KerneText(l.Suche * l.Prozessoren)),
            new("HiGHS", 0, l.Highs, 0, 0, KerneText(l.HighsKerne)),
            new("Rest", 0, 0, l.Sonstiges, 0, KerneText(l.Sonstiges * l.Prozessoren)),
            new("frei", 0, 0, 0, l.Frei, KerneText(l.Frei * l.Prozessoren)),
        ]
        : [];

    /// <summary>Holt die Messungen der letzten zwei Minuten (älteste zuerst) für den Verlauf.</summary>
    public IReadOnlyList<Lastpunkt> Lastverlauf
    {
        get => lastverlauf;
        private set => SetProperty(ref lastverlauf, value);
    }

    /// <summary>Kerne als Text mit einer Nachkommastelle.</summary>
    /// <param name="kerne">Ausgelastete Kerne (1 = einer voll).</param>
    /// <returns>Z. B. <c>10,2 Kerne</c> oder <c>1,0 Kern</c>.</returns>
    public static string KerneText(double kerne)
    {
        string zahl = Math.Max(0, kerne).ToString("0.0", Deutsch);
        return zahl == "1,0" ? zahl + " Kern" : zahl + " Kerne";
    }

    /// <summary>
    /// Merkt sich, dass sich die Kostenfunktion geändert hat (Gewichte, Optionen oder Spielplandaten): Die alten Kosten
    /// sind nicht mehr vergleichbar, Bezug der prozentualen Verbesserung ist ab jetzt der erste gültige Plan danach.
    /// </summary>
    public void KostenAngepasst()
    {
        startkosten = null;
        bezug = SeitAnpassung;
    }

    /// <summary>Übernimmt den Stand der Generierung (im Takt der Statusabfrage).</summary>
    /// <param name="stand">Der Stand.</param>
    public void Uebernehmen(Optimierungsstand stand)
    {
        ArgumentNullException.ThrowIfNull(stand);
        LastZeigen(stand);
        Plaene = Kostenanzeige.Kurz(stand.Durchlaeufe);
        Tempo = stand.PlaeneProSekunde.ToString("N0", Deutsch) + " Pläne/s";
        ZeigtVerstoesse = stand.Lenkung is not null;
        Titel = stand.Lenkung is { } lenkung ? lenkung.Bezeichnung : "Kostenoptimierung · Gesamtkosten";
        LenkungZeigen(stand.Lenkung);
        if (stand.Kosten < 0)
        {
            KostenHinweis = stand.Pausiert ? "angehalten" : "erster Plan wird gesucht …";
            return;
        }

        // Die ersten Pläne haben oft noch harte Fehler (je 10 Mrd.); gegen sie gerechnet stünde dort immer „−100 %“.
        // Bezug ist deshalb der erste Plan ohne harte Fehler (nach einer Kostenanpassung der erste danach).
        if (stand.Kosten < HarteFehler)
        {
            startkosten ??= stand.Kosten;
        }

        Kosten = Kostenanzeige.Kurz(stand.Kosten);
        Verbesserungen = stand.Verbesserungen.ToString(Deutsch);
        LetzteVerbesserung = string.Create(Deutsch, $"letzte vor {stand.SeitLetzterVerbesserung:hh\\:mm\\:ss}");
        string veraenderung = ZeigtVerstoesse ? VerstossVeraenderung() : Veraenderung(stand.Kosten);
        KostenHinweis = (stand.Pausiert, veraenderung.Length) switch
        {
            (false, _) => veraenderung,
            (true, 0) => "angehalten",
            _ => "angehalten · " + veraenderung,
        };
    }

    /// <summary>Zeigt, woran HiGHS gerade rechnet (nur im Automodus).</summary>
    /// <param name="anzeige">Die Anzeige; <c>null</c> blendet sie aus.</param>
    internal void HighsZeigen(Highsanzeige? anzeige) => Highs = anzeige;

    /// <summary>Setzt die Anzeige für eine neue Generierung oder Staffel zurück.</summary>
    /// <param name="hinweis">Text unter den Kosten.</param>
    internal void Zuruecksetzen(string hinweis)
    {
        startkosten = null;
        bezug = SeitStart;
        Kosten = Leer;
        KostenHinweis = hinweis;
        Verbesserungen = Leer;
        LetzteVerbesserung = string.Empty;
        Plaene = Leer;
        Tempo = string.Empty;
        Pflicht = Leer;
        PflichtHinweis = string.Empty;
        PflichtErfuellt = true;
        ZeigtVerstoesse = false;
        Titel = TitelKosten;
        verstoesse = null;
        VerstossA = Leer;
        VerstossB = Leer;
        VerstossC = Leer;
        LenkungZeigen(null);
        Highs = null;
        Last = null;
        letzteMessung = null;
        Lastverlauf = [];
    }

    /// <summary>Die Generierung ist beendet: keine Prozessorlast mehr, HiGHS rechnet nicht mehr.</summary>
    internal void Anhalten()
    {
        Last = null;
        Highs = Highs is { } h ? h with { Zeit = "Generierung beendet", Anteil = null, Rechnet = false } : null;
    }

    /// <summary>Zeigt vor dem Start einen gewählten Ausgangsplan: seine Kosten und Pflichtregeln, noch ohne Generierung.</summary>
    /// <param name="bewertung">Bewertung des Plans.</param>
    /// <param name="einteilung">Einteilung der Kriterien.</param>
    /// <param name="name">Name des Plans für den Hinweis.</param>
    internal void PlanZeigen(Planbewertung bewertung, Stufeneinteilung einteilung, string name)
    {
        Zuruecksetzen("Ausgangsplan: " + name);
        Kosten = Kostenanzeige.Kurz(bewertung.Gesamtkosten);
        Uebernehmen(bewertung, einteilung);
    }

    /// <summary>Übernimmt die Pflichtregeln des besten Plans.</summary>
    /// <param name="bewertung">Die Bewertung des besten Plans.</param>
    /// <param name="einteilung">Einteilung der Kriterien durch den Staffelleiter.</param>
    internal void Uebernehmen(Planbewertung bewertung, Stufeneinteilung einteilung)
    {
        IReadOnlyList<Qualitaetskriterium> kriterien = Planqualitaet.Kriterien(bewertung, einteilung);
        int verletzt = kriterien.Count(k => k.IstPflicht && !k.Erfuellt);
        int weitere = kriterien.Count(k => !k.IstPflicht && !k.Erfuellt);
        PflichtErfuellt = verletzt == 0;
        Pflicht = verletzt == 0 ? "alle erfüllt" : string.Create(Deutsch, $"{verletzt} verletzt");
        PflichtHinweis = weitere == 1 ? "1 weiteres Kriterium mit Verstößen" : string.Create(Deutsch, $"{weitere} weitere Kriterien mit Verstößen");

        // Verstöße nach Stufen (wie im Automodus); Vergleich mit dem Stand nach der Basisoptimierung (Lenkungsstand).
        Stufenwert wert = einteilung.Zaehlen(bewertung);
        verstoesse = wert;

        VerstossA = wert.A.ToString(Deutsch);
        VerstossB = wert.B.ToString(Deutsch);
        VerstossC = wert.CAnzahl.ToString(Deutsch);
    }

    private static string Basiswert(int? wert) => wert is int w ? string.Create(Deutsch, $"{w} →") : string.Empty;

    /// <summary>Automodus: Verstöße am Ende der Basisoptimierung und die Kriterien, an denen gerade gearbeitet wird.</summary>
    private void LenkungZeigen(Lenkungsstand? lenkung)
    {
        Stufenwert? basis = lenkung?.Basis;
        BasisA = Basiswert(basis?.A);
        BasisB = Basiswert(basis?.B);
        BasisC = Basiswert(basis?.CAnzahl);
        IReadOnlyList<Kostenkriterium> kriterien = lenkung?.Kriterien ?? [];
        Phase = string.Join(", ", kriterien.Select(Planqualitaet.Name));
    }

    /// <summary>Untere Zeile im Automodus: nur Hinweise auf den Zustand (Bewertung ausstehend, harte Fehler), sonst leer.</summary>
    private string VerstossVeraenderung() => verstoesse switch
    {
        null => "erster Plan wird bewertet …",
        { Harte: > 0 } jetzt => string.Create(Deutsch, $"Plan hat noch {jetzt.Harte} harte Fehler"),
        _ => string.Empty,
    };

    private string Veraenderung(double jetzt)
    {
        if (jetzt >= HarteFehler)
        {
            return "Plan hat noch harte Fehler";
        }

        if (startkosten is not double start || start <= 0 || jetzt >= start)
        {
            return bezug + " unverändert";
        }

        double prozent = Math.Round((1 - (jetzt / start)) * 100);
        return prozent < 1
            ? "−< 1 % " + bezug
            : string.Create(Deutsch, $"−{prozent:0} % {bezug}");
    }

    /// <summary>Übernimmt die Aufteilung des Prozessors und ergänzt den Verlauf.</summary>
    /// <param name="stand">Der Stand.</param>
    private void LastZeigen(Optimierungsstand stand)
    {
        Last = stand.Laeuft ? stand.Last : null;
        if (stand.Laeuft && stand.Last is { } neu && !ReferenceEquals(neu, letzteMessung))
        {
            // Je neue Messung ein Punkt; nach zwei Minuten fällt er heraus.
            letzteMessung = neu;
            long jetzt = Stopwatch.GetTimestamp();
            Lastverlauf =
            [
                .. Lastverlauf.Where(p => Stopwatch.GetElapsedTime(p.Zeitpunkt, jetzt) <= Verlaufsdauer),
                new Lastpunkt(jetzt, neu.Suche, neu.Highs, neu.Sonstiges),
            ];
        }
    }
}
