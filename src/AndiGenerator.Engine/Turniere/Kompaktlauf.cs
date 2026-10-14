// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Ein Lauf des Kompaktplaners: zuerst die gezielte Konstruktion nach den Kriterien (<see cref="Kompaktoptimierung"/>),
/// ohne sie (HiGHS nicht verfügbar) wie bisher, wo es geht, die vollständige Suche nach dem kürzesten Plan nur aus 4er-Spieltagen
/// (<see cref="Viererbaum"/>), danach mehrere zufällige Suchen parallel bis zum Ende der Zeit (auch mit 3er-Spieltagen).
/// Findet sich kein Plan, werden schrittweise Verstöße gegen die Meldungen erlaubt, erst danach Mannschaften ohne
/// Heimspieltag.
/// </summary>
internal sealed class Kompaktlauf(Kompaktmodell modell, Kompaktplanoptionen optionen, Kompaktsteuerung steuerung, Action<Kompaktfortschritt>? fortschritt)
{
    /// <summary>Höchstzahl erlaubter Verstöße gegen die Meldungen.</summary>
    public const int MaxVerstoesse = 6;

    private const int VersucheJeStufe = 200;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private readonly List<(string Name, Kompaktloesung Loesung)> ohneVerstoss = [];
    private readonly List<string> optimierungshinweise = [];
    private readonly Stopwatch meldeuhr = Stopwatch.StartNew();
    private readonly Lock sperre = new();
    private long geprueft;
    private long letzteMeldung = -1000;
    private int stufe;
    private int phasen = 1;
    private int phase;
    private TimeSpan suchbeginn;

    /// <summary>Holt die besten Pläne.</summary>
    public Kompaktfront Front { get; } = new();

    /// <summary>Holt die Zahl der Suchversuche.</summary>
    public long Geprueft => Interlocked.Read(ref geprueft);

    /// <summary>
    /// Holt die besten Pläne mit 3er-Spieltagen (ohne Verstöße): Daraus werden Varianten, wenn sie etwas können, was kein
    /// Plan nur aus 4er-Spieltagen kann.
    /// </summary>
    public Kompaktfront Dreierfront { get; } = new(true);

    /// <summary>Holt einen Hinweis der Nachsuche ohne Verstöße, oder <c>null</c>.</summary>
    public string? OhneVerstossHinweis { get; private set; }

    /// <summary>Holt die vollständige Suche nach 4er-Plänen, oder <c>null</c>, wenn sie für diese Aufgabe nicht geht.</summary>
    public Viererbaum? Baum { get; private set; }

    /// <summary>Holt die Suche mit Spitzen- und Abstiegsspieltag, oder <c>null</c>, wenn sie nicht lief.</summary>
    public Viererbaum? Gruppenbaum { get; private set; }

    /// <summary>Holt den kürzesten Plan mit Spitzen- und Abstiegsspieltag, oder <c>null</c>.</summary>
    public Kompaktloesung? Gruppenloesung { get; private set; }

    /// <summary>Holt das beste Gesamtpaket (<see cref="Kompaktgesamt"/>) unter den gesammelten kurzen 4er-Plänen, oder <c>null</c>.</summary>
    public Kompaktloesung? Gesamtloesung { get; private set; }

    /// <summary>Holt einen Wert, der angibt, ob die Pläne gezielt konstruiert wurden (<see cref="Kompaktoptimierung"/>).</summary>
    public bool Optimiert { get; private set; }

    /// <summary>Holt das Signal „beenden“.</summary>
    private CancellationToken Abbruch => steuerung.Token;

    /// <summary>Holt die Rechenzeit der Zufallssuche (ohne Pausen).</summary>
    private TimeSpan Suchzeit => steuerung.Laufzeit - suchbeginn;

    /// <summary>
    /// Die Pläne ganz ohne Verstöße aus der Nachsuche, wenn die übrige Suche nur Pläne mit Verstößen fand: mit Namen
    /// („Ohne Verstöße“, „Ohne Verstöße, mit Einzelspielen“).
    /// </summary>
    /// <returns>Eine Kopie der Liste.</returns>
    public List<(string Name, Kompaktloesung Loesung)> OhneVerstossPlaene()
    {
        lock (sperre)
        {
            return ohneVerstoss.ToList();
        }
    }

    /// <summary>Die Hinweise der gezielten Konstruktion (was gefunden wurde und ob es beweisbar das Beste ist).</summary>
    /// <returns>Eine Kopie der Liste.</returns>
    public List<string> Optimierungshinweise()
    {
        lock (sperre)
        {
            return optimierungshinweise.ToList();
        }
    }

    /// <summary>Sucht, bis die Zeit abgelaufen, die Zahl der Versuche erreicht oder der Lauf abgebrochen ist.</summary>
    public void Ausfuehren()
    {
        Front.ViererVorrang = modell.NurViererMoeglich;
        if (optionen.Loeser is IGanzzahlloeser loeser)
        {
            Optimiert = true;
            phasen = 2;
            Optimieren(loeser);
            phase++;
        }
        else
        {
            Durchsuchen();
        }

        suchbeginn = steuerung.Laufzeit;
        int threads = Math.Max(1, optionen.Threads);
        if (threads == 1)
        {
            Arbeiten(0);
        }
        else
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, Arbeiten);
        }

        OhneVerstoesseSuchen();
        Senden("Fertig.", 1, true);
    }

    /// <summary>Bisheriger Weg ohne Optimierung: vollständige 4er-Suche, Spitzen- und Abstiegsspieltag, Gesamtpaket.</summary>
    private void Durchsuchen()
    {
        int threads = Math.Max(1, optionen.Threads);
        bool vierer = Viererbaum.Moeglich(modell);
        bool gruppen = vierer && modell.Gruppen.Count > 0 && modell.Gruppen[0].Length == 4;
        phasen = 1 + (vierer ? 2 : 0) + (gruppen ? 1 : 0);
        if (vierer)
        {
            Baum = new Viererbaum(modell, [], "Nur 4er-Spieltage");
            Viererbaum baum = Baum;
            baum.Suchen(threads, optionen.ExaktDauer, steuerung, text => Melden(text, baum.Anteil));
            if (Baum.Beste is IReadOnlyList<Kompaktkandidat> turniere)
            {
                Aufnehmen(new Kompaktloesung(modell, turniere));
            }

            phase++;
        }

        if (gruppen)
        {
            Gruppenbaum = new Viererbaum(modell, modell.Gruppen, "Mit Spitzen- und Abstiegsspieltag");
            Viererbaum baum = Gruppenbaum;
            baum.Suchen(threads, optionen.ExaktDauer, steuerung, text => Melden(text, baum.Anteil));
            if (Gruppenbaum.Beste is IReadOnlyList<Kompaktkandidat> gruppenturniere)
            {
                // Sonntage bleiben, wie die Suche sie gelegt hat (Gruppenspieltage so spät wie möglich).
                Gruppenloesung = Kompaktverbesserung.EinzelAusgleichen(modell, new Kompaktloesung(modell, gruppenturniere));
            }

            phase++;
        }

        if (vierer)
        {
            GesamtpaketSuchen(threads);
            phase++;
        }
    }

    /// <summary>
    /// Konstruiert die Pläne gezielt (<see cref="Kompaktoptimierung"/>) auf allen Kernen: zugleich den kürzesten Plan nur
    /// aus 4er-Spieltagen (wenn das aufgehen kann) und den kürzesten überhaupt; dann den mit den wenigsten Punkten (bestes
    /// Gesamtpaket); zuletzt zugleich einen mit Spitzen- und Abstiegsspieltag (wenn ihn das Gesamtpaket nicht hat) und
    /// weitere Pläne mit anderen Spieltagen zur Auswahl. Jeder Schritt rechnet mehrere Läufe des Lösers parallel.
    /// </summary>
    private void Optimieren(IGanzzahlloeser loeser)
    {
        var optimierung = new Kompaktoptimierung(modell, loeser);
        TimeSpan beginn = steuerung.Laufzeit;
        TimeSpan gesamt = optionen.ExaktDauer;
        int kerne = Math.Max(1, optionen.Threads);
        bool gruppen = modell.Gruppen.Count > 0;
        int schritte = (modell.NurViererMoeglich ? 1 : 0) + 2 + (gruppen ? 1 : 0) + 2;
        int schritt = 0;

        Kompaktloesung? Schritt(string text, Kompaktauftrag auftrag, Kompaktloesung? start, double anteilZeit, int parallel)
        {
            TimeSpan seit = steuerung.Laufzeit;
            TimeSpan grenze = TimeSpan.FromTicks(Math.Min((long)(gesamt.Ticks * anteilZeit), (gesamt - (seit - beginn)).Ticks));
            if (grenze <= TimeSpan.Zero || Abbruch.IsCancellationRequested)
            {
                Interlocked.Increment(ref schritt);
                return null;
            }

            bool Aufhoeren()
            {
                steuerung.Warten();
                TimeSpan verstrichen = steuerung.Laufzeit - seit;
                double anteil = (Volatile.Read(ref schritt) + Math.Min(1, verstrichen / grenze)) / schritte;
                Melden(string.Create(Deutsch, $"{text} ({verstrichen.TotalSeconds:N0} s, {parallel} Läufe parallel) …"), anteil);
                return Abbruch.IsCancellationRequested || verstrichen > grenze;
            }

            // HiGHS bekommt die Zeit des Schritts mit Reserve als harte Grenze: Sie greift auch in Phasen ohne Rückfrage
            // (Stopp, Fortschritt); die Reserve deckt kurze Pausen, in denen die Uhr des Lösers weiterläuft.
            TimeSpan hart = grenze + TimeSpan.FromTicks(Math.Max(TimeSpan.FromSeconds(15).Ticks, grenze.Ticks / 2));
            (Kompaktloesung? l, bool optimal) = optimierung.Suchen(auftrag, start, Math.Max(1, parallel), Aufhoeren, hart);
            Interlocked.Increment(ref schritt);
            if (l is null)
            {
                return null;
            }

            Aufnehmen(l);
            string beweis = optimal ? "bestmöglich" : "nicht bewiesen bestmöglich (Zeit)";
            lock (sperre)
            {
                optimierungshinweise.Add(string.Create(Deutsch, $"{text}: {l.Km:N0} km, {l.Vierer}× 4er, {l.Dreier}× 3er, {l.Einzel} Einzel, {l.Verstoesse} Verstöße – {beweis}."));
            }

            return l;
        }

        // Startpunkte aus der Zufallssuche, damit der Löser sofort einen vollständigen Plan hat.
        Kompaktloesung? vierer = null;
        Kompaktloesung? kurz = null;
        if (modell.NurViererMoeglich)
        {
            Parallel.Invoke(
                () => vierer = Schritt("Kürzester Plan nur aus 4er-Spieltagen", new Kompaktauftrag(null, true, false, false, false), Zufallsplan(true), 0.25, kerne / 2),
                () => kurz = Schritt("Kürzester Plan", new Kompaktauftrag(null, false, false, false, false), Zufallsplan(false), 0.25, kerne - (kerne / 2)));
        }
        else
        {
            kurz = Schritt("Kürzester Plan", new Kompaktauftrag(null, false, false, false, false), Zufallsplan(false), 0.25, kerne);
        }

        double? bezug = (vierer ?? kurz)?.Bewertungskm;
        var punkte = new Kompaktauftrag(bezug, false, vierer is not null, gruppen, false);
        Kompaktloesung? best = Schritt("Plan mit den wenigsten Punkten", punkte, vierer ?? kurz, 0.3, kerne);
        if (best is not null && bezug is double kmMin)
        {
            double spanneMin = Front.Alle().Select(l => l.KmSpanne).DefaultIfEmpty(best.KmSpanne).Min();
            Comparison<Kompaktloesung> vergleich = Kompaktgesamt.Vergleich(kmMin, 4 * (modell.N / 4), spanneMin, kmMin / modell.N);
            Gesamtloesung = Kompaktverbesserung.TageVerteilen(modell, Kompaktverbesserung.EinzelAusgleichen(modell, best), vergleich);
            Front.Hinzufuegen(Gesamtloesung);
        }

        if (best is null)
        {
            return;
        }

        // Zugleich: Spitzen- und Abstiegsspieltag (falls nötig) und zwei weitere Pläne mit anderen Spieltagen.
        bool mitGruppen = gruppen && best.GruppenFehlen > 0;
        int teile = mitGruppen ? 3 : 2;
        Parallel.Invoke(
            () =>
            {
                if (mitGruppen && Schritt("Plan mit Spitzen- und Abstiegsspieltag", punkte with { GruppenPflicht = true }, null, 0.15, kerne / teile) is Kompaktloesung g)
                {
                    Gruppenloesung = Kompaktverbesserung.EinzelAusgleichen(modell, g);
                }
            },
            () => Schritt("Weiterer Plan mit anderen Spieltagen", punkte with { Ausser = [best] }, kurz, 0.15, kerne / teile),
            () => Schritt("Weiterer Plan mit anderen Spieltagen", punkte with { Ausser = [best, .. new[] { kurz }.OfType<Kompaktloesung>()] }, vierer, 0.15, kerne - (2 * (kerne / teile))));
    }

    /// <summary>Ein Plan der Zufallssuche ohne Verstöße als Startpunkt für den Löser (höchstens zwei Sekunden), oder <c>null</c>.</summary>
    private Kompaktloesung? Zufallsplan(bool nurVierer)
    {
        var suche = new Kompaktsuche(modell, optionen.Startwert);
        var uhr = Stopwatch.StartNew();
        while (uhr.Elapsed < TimeSpan.FromSeconds(2) && !Abbruch.IsCancellationRequested)
        {
            if (suche.Versuch(0, modell.MindestensOhneAusrichtung, nurVierer) is IReadOnlyList<Kompaktkandidat> turniere)
            {
                return new Kompaktloesung(modell, turniere);
            }
        }

        return null;
    }

    /// <summary>Meldet den Stand höchstens fünfmal je Sekunde; der Anteil gilt für den laufenden Schritt.</summary>
    private void Melden(string text, double anteilImSchritt) => Senden(text, anteilImSchritt, false);

    private void Senden(string text, double anteilImSchritt, bool immer)
    {
        if (fortschritt is null)
        {
            return;
        }

        long jetzt = meldeuhr.ElapsedMilliseconds;
        long letzte = Interlocked.Read(ref letzteMeldung);
        if (!immer && (jetzt - letzte < 200 || Interlocked.CompareExchange(ref letzteMeldung, jetzt, letzte) != letzte))
        {
            return;
        }

        int schritt = Math.Min(phase, phasen - 1);
        double anteil = Math.Clamp((schritt + Math.Clamp(anteilImSchritt, 0, 1)) / phasen, 0, 1);
        fortschritt(new Kompaktfortschritt(string.Create(Deutsch, $"Schritt {schritt + 1} von {phasen} – {text}"), anteil));
    }

    /// <summary>
    /// Das beste Gesamtpaket: Für jeden gesammelten kurzen 4er-Plan (mit und ohne Spitzen- und Abstiegsspieltag) werden die
    /// Spieltage so auf die Sonntage verlegt, dass die Gesamtwertung am besten ist; der beste gilt.
    /// </summary>
    private void GesamtpaketSuchen(int threads)
    {
        if (Baum?.Beste is not IReadOnlyList<Kompaktkandidat> kuerzeste)
        {
            return;
        }

        double kmMin = kuerzeste.Sum(k => k.Km);
        int ziel = 4 * (modell.N / 4);
        List<IReadOnlyList<Kompaktkandidat>> plaene = [.. Baum.Gesammelt(), .. Gruppenbaum?.Gesammelt() ?? []];

        // Der Unterschied der Kilometer hängt nicht von den Sonntagen ab: kleinster Wert vorab über alle gesammelten Pläne.
        double spanneMin = plaene.Select(p => new Kompaktloesung(modell, p).KmSpanne).DefaultIfEmpty(0).Min();
        Comparison<Kompaktloesung> vergleich = Kompaktgesamt.Vergleich(kmMin, ziel, spanneMin, kmMin / modell.N);
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = threads, CancellationToken = Abbruch };
        int fertig = 0;
        try
        {
            Parallel.ForEach(plaene, parallel, plan =>
            {
                steuerung.Warten();
                int nummer = Interlocked.Increment(ref fertig);
                Melden(string.Create(Deutsch, $"Bestes Gesamtpaket: {nummer:N0} von {plaene.Count:N0} kurzen Plänen bewertet …"), nummer / (double)plaene.Count);
                Kompaktloesung l = Kompaktverbesserung.EinzelAusgleichen(modell, new Kompaktloesung(modell, plan));
                l = Kompaktverbesserung.TageVerteilen(modell, l, vergleich);
                Front.Hinzufuegen(l);
                lock (sperre)
                {
                    if (Gesamtloesung is null || vergleich(l, Gesamtloesung) < 0)
                    {
                        Gesamtloesung = l;
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Abgebrochen: das bis dahin beste Gesamtpaket gilt.
            Senden("Gesamtpaket abgebrochen.", 1, true);
        }
    }

    /// <summary>
    /// Nachsuche, wenn kein gefundener Plan ohne Verstöße ist (Peter, 13.10.2026): vollständig nach einem Plan ganz ohne
    /// Verstoß, zuerst mit möglichst wenigen Mannschaften ohne Heimspieltag, dann – wenn dabei eine Mannschaft ohne
    /// Heimspiel bleibt – mit Einzelspielen, sodass jede wenigstens ein Heimspiel hat.
    /// </summary>
    private void OhneVerstoesseSuchen()
    {
        // Die gezielte Konstruktion sucht schon nach möglichst wenigen Verstößen.
        if (Optimiert || Abbruch.IsCancellationRequested || Front.Alle().Exists(l => l.Verstoesse == 0))
        {
            return;
        }

        var baum = new Meldungsbaum(modell);
        TimeSpan beginn = steuerung.Laufzeit;
        TimeSpan Rest() => optionen.ExaktDauer - (steuerung.Laufzeit - beginn);
        int grund = modell.N % 2 == 0 ? modell.N / 2 : 0;
        Kompaktloesung? ohneEinzel = null;
        for (int ohne = modell.MindestensOhneAusrichtung; ohne <= modell.N && Rest() > TimeSpan.Zero; ohne++)
        {
            Melden(string.Create(Deutsch, $"Pläne ohne Verstöße: höchstens {ohne} Mannschaft(en) ohne Heimspieltag …"), 1);
            if (baum.Suchen(grund, ohne, false, Rest(), steuerung) is IReadOnlyList<Kompaktkandidat> turniere)
            {
                ohneEinzel = new Kompaktloesung(modell, turniere);
                Merken("Ohne Verstöße", ohneEinzel);
                break;
            }

            if (baum.Abgebrochen)
            {
                break;
            }
        }

        if (ohneEinzel is null)
        {
            OhneVerstossHinweis = baum.Abgebrochen || Rest() <= TimeSpan.Zero
                ? "Ein Plan ganz ohne Verstöße wurde in der verfügbaren Zeit nicht gefunden."
                : "Mit diesen Meldungen gibt es keinen Plan ganz ohne Verstöße (vollständig geprüft).";
            return;
        }

        if (ohneEinzel.OhneHeimspieltag <= modell.MindestensOhneAusrichtung)
        {
            return;
        }

        for (int extra = 1; extra <= modell.N / 2 && Rest() > TimeSpan.Zero; extra++)
        {
            Melden(string.Create(Deutsch, $"Pläne ohne Verstöße mit {extra} Einzelspiel(en), jede Mannschaft mit Heimspiel …"), 1);
            if (baum.Suchen(grund + extra, modell.MindestensOhneAusrichtung, true, Rest(), steuerung) is IReadOnlyList<Kompaktkandidat> turniere)
            {
                Merken("Ohne Verstöße, mit Einzelspielen", new Kompaktloesung(modell, turniere));
                return;
            }

            if (baum.Abgebrochen)
            {
                return;
            }
        }
    }

    private void Merken(string name, Kompaktloesung loesung)
    {
        lock (sperre)
        {
            ohneVerstoss.Add((name, loesung));
        }
    }

    /// <summary>Erlaubte Verstöße und Mannschaften ohne Heimspieltag einer Stufe.</summary>
    private (int Verstoesse, int OhneAusrichtung) Grenzen(int s) =>
        s <= MaxVerstoesse
            ? (s, modell.MindestensOhneAusrichtung)
            : (MaxVerstoesse, Math.Min(modell.N, modell.MindestensOhneAusrichtung + s - MaxVerstoesse));

    private void Arbeiten(int nummer)
    {
        var suche = new Kompaktsuche(modell, unchecked(optionen.Startwert + (nummer * 7919)));
        int versuche = 0;
        while (Weiter(versuche))
        {
            steuerung.Warten();
            versuche++;
            int s = Volatile.Read(ref stufe);
            (int verstoesse, int ohne) = Grenzen(s);

            // Jeder zweite Versuch nur mit 4er-Spieltagen, wenn das rechnerisch aufgehen kann.
            bool nurVierer = modell.NurViererMoeglich && versuche % 2 == 0;
            IReadOnlyList<Kompaktkandidat>? turniere = suche.Versuch(verstoesse, ohne, nurVierer);
            long anzahl = Interlocked.Increment(ref geprueft);
            if (turniere is not null)
            {
                Aufnehmen(new Kompaktloesung(modell, turniere));
            }
            else
            {
                Lockern(s, versuche);
            }

            if (anzahl % 64 == 0)
            {
                double anteil = optionen.Versuche is int max
                    ? anzahl / (double)(max * Math.Max(1, optionen.Threads))
                    : Suchzeit / optionen.Dauer;
                Melden(string.Create(Deutsch, $"Weitere Varianten (auch mit 3er-Spieltagen): {anzahl:N0} Versuche …"), anteil);
            }
        }
    }

    private bool Weiter(int versuche) =>
        !Abbruch.IsCancellationRequested && (optionen.Versuche is int hoechstens ? versuche < hoechstens : Suchzeit < optionen.Dauer);

    /// <summary>Ohne gefundenen Plan nach einer gewissen Zeit die nächste Stufe erlauben.</summary>
    private void Lockern(int s, int versuche)
    {
        if (!Front.Leer || s >= MaxVerstoesse + modell.N)
        {
            return;
        }

        bool faellig = optionen.Versuche is not null
            ? versuche % VersucheJeStufe == 0
            : Suchzeit > optionen.Dauer * (s + 1) / 16;
        if (faellig)
        {
            Interlocked.CompareExchange(ref stufe, s + 1, s);
        }
    }

    private void Aufnehmen(Kompaktloesung loesung)
    {
        Kompaktloesung ausgeglichen = Kompaktverbesserung.EinzelAusgleichen(modell, loesung);
        if (ausgeglichen.Dreier > 0 && ausgeglichen.Verstoesse == 0 && ausgeglichen.OhneHeimspieltag <= modell.MindestensOhneAusrichtung
            && Dreierfront.Hinzufuegen(ausgeglichen))
        {
            Dreierfront.Hinzufuegen(Kompaktverbesserung.TageVerteilen(modell, ausgeglichen, Kompaktverbesserung.Stand));
            Dreierfront.Hinzufuegen(Kompaktverbesserung.TageVerteilen(modell, ausgeglichen, Kompaktverbesserung.Gemeinsam));
        }

        if (!Front.Hinzufuegen(ausgeglichen))
        {
            return;
        }

        Front.Hinzufuegen(Kompaktverbesserung.TageVerteilen(modell, ausgeglichen, Kompaktverbesserung.Stand));
        Front.Hinzufuegen(Kompaktverbesserung.TageVerteilen(modell, ausgeglichen, Kompaktverbesserung.Gemeinsam));
    }
}
