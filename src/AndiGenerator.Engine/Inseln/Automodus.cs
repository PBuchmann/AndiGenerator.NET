// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Kern;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Inseln;

/// <summary>
/// Automodus (MIGRATIONSPLAN Abschnitt 11, Vorschlag Peter): lenkt die Suche über die Gewichtungen, wie es ein
/// erfahrener Anwender an der Oberfläche tut. Die Einteilung der Kriterien in A, B und C und die Wichtigkeit der
/// C-Kriterien legt der Staffelleiter fest (<see cref="Stufeneinteilung"/>).
/// <list type="bullet">
/// <item>Grundlauf mit den Gewichtungen des Anwenders; sein bester Plan ist der Maßstab je Mannschaft und C-Kriterium.</item>
/// <item>
/// Drängen, erst Stufe A, dann Stufe B: Der Automodus beobachtet die Verstöße im Plan der Suche. Ändern sie sich nach einer
/// Gewichtsänderung 10 s lang nicht mehr, steigen die Gewichte genau der Mannschaften und Kriterien um eine Stufe, die einen
/// Verstoß der Stufe haben – bis bei ihr nichts mehr geht (nichts mehr anzuheben oder dreimal kein Fortschritt). Die
/// A-Gewichte bleiben beim Drängen von B stehen. Zeitgrenzen gibt es keine, außer der Höchstdauer der Basisoptimierung. Kennt
/// der Automodus die untere Schranke aus HiGHS (Summe je Stufe), hört er bei einer Stufe auf, sobald der beste Plan sie
/// erreicht, und schützt sie (Gewichte „extrem hoch“).
/// </item>
/// <item>
/// Glätten, ab dem besten Plan: C-Kriterien nach Wichtigkeit, Verstöße allgemein reduzieren, Spitzen zuerst. Eine
/// Mannschaft reißt bei einem C-Kriterium aus, wenn sie mehr Verstöße als im Grundlauf hat als der Spielraum erlaubt
/// (<see cref="Optimierungseinstellungen.AutoSpielraum"/>, Standard 2) – zu viele bei einer Mannschaft sind unfair. Für die Ausreißer, sonst für die Mannschaften mit den meisten
/// Verstößen steigt das Gewicht des Kriteriums eine Stufe, so lange, bis nichts mehr anzuheben ist (dann das nächste
/// Kriterium) oder A oder B wieder steigen (<see cref="Stufenvergleich.AOderBSchlechter"/>): Dann wird die Anhebung ganz
/// zurückgezogen, beim besten Plan neu begonnen und mit dem nächsten Kriterium weitergemacht. Sind alle C-Kriterien durch,
/// wird wieder gedrängt – oder, wenn A und B bei der Schranke sind, von vorn geglättet.
/// </item>
/// <item>
/// Der beste Plan wird immer mit den Gewichtungen des Anwenders bewertet und nach einer Regel verglichen
/// (<see cref="Stufenvergleich"/>): harte Fehler, A, B, C-Verstöße, C-Ausreißer, dann C-Kosten. Pläne aus HiGHS (Schranke,
/// Alternativen, Teilneuplanung) werden genauso bewertet.
/// </item>
/// </list>
/// </summary>
internal sealed class Automodus
{
    /// <summary>Sekunden, denen ein Lauf mit fester Zahl von Durchläufen (<see cref="Inseloptimierer.Rechnen"/>) ohne Zeitbudget entspricht.</summary>
    public const double StandardBudget = 300;

    /// <summary>So viele Schritte behält das Protokoll höchstens.</summary>
    private const int ProtokollHoechstens = 500;

    /// <summary>Mindestdauer des Grundlaufs in Sekunden.</summary>
    private const double MinGrundlauf = 10;

    /// <summary>Sekunden ohne Verbesserung der Suche, ab denen sie als festgefahren gilt (mit mindestens <see cref="stillstandPlaene"/> Plänen).</summary>
    private const double Stillstand = 10;

    /// <summary>Mindestabstand zweier Änderungen in Sekunden.</summary>
    private const double MinPhase = 8;

    /// <summary>
    /// Sekunden, die ein verschobenes Kriterium unter „In Arbeit“ stehen bleibt, auch wenn der Automodus schon weiter ist
    /// (etwa weil es nichts zu verbessern gab): So sieht der Staffelleiter, dass die Verschiebung angekommen ist.
    /// </summary>
    private const double AnzeigeVerschoben = 5;

    /// <summary>Änderungen ohne weniger A/B-Verstöße im Plan der Suche, nach denen das Drängen endet.</summary>
    private const int DraengenOhneFortschritt = 3;

    /// <summary>Mindestspielraum bei den Kosten für Kriterien ohne Anzahl (Spieltage), als Anteil der gesamten C-Kosten des Maßstabs.</summary>
    private const double SpielraumKosten = 0.02;

    private readonly KernPlan bewerter;
    private readonly string[] ids;
    private readonly string[] namen;
    private readonly Loesung basisLoesung;
    private readonly int spielraum;
    private readonly double kostenToleranz;
    private readonly Berechnungsoptionen original;
    private readonly double deutlich;
    private readonly long stillstandPlaene;
    private readonly double maxGrundlauf;
    private readonly double[] grenzeKosten = new double[Stufeneinteilung.AnzahlKriterien];
    private readonly int[] basisMannschaft;

    /// <summary>Beim Glätten angehobene Gewichte des aktuellen C-Kriteriums mit ihrem Wert vor der ersten Anhebung.</summary>
    private readonly Dictionary<int, Gewichtung> angehobenC = [];
    private readonly List<string> aenderungen = [];

    /// <summary>Beim Glätten zählt die Suche Verstöße (siehe <see cref="Optimierungseinstellungen.AutoZaehlen"/>).</summary>
    private readonly bool zaehlen;

    /// <summary>Die Auswertung des zuletzt geprüften Plans (<see cref="Pruefen"/>), für <see cref="Verursacher"/>.</summary>
    private Auswertung? letzte;
    private Phase phase = Phase.Grundlauf;

    /// <summary>
    /// Untere Schranke aus HiGHS für die Summe der Stufe A und – bei bestmöglichem A – der Stufe B; <c>null</c> = unbekannt
    /// (siehe <see cref="SchrankenSetzen"/>).
    /// </summary>
    private (int? A, int? B) schranke;

    /// <summary>Die Stufen A bzw. B des besten Plans sind bei der Schranke aus HiGHS (beim letzten <see cref="Lenken"/>).</summary>
    private (bool A, bool B) amZiel;

    /// <summary>Bester Plan zu Beginn des aktuellen C-Kriteriums (Bezug beim Zurückziehen).</summary>
    private Stufenwert bezugWert;
    private (int, int, int)? draengBester;
    private int ohneDraengfortschritt;
    private int indexC;
    private Stufe draengStufe = Stufe.A;
    private double letzteAenderung;
    private double letzteSuche;
    private long plaeneLetzteSuche;
    private long plaene;
    private double deutlicheKosten = -1;
    private (int, int, int, int, int)? sucheVerstoesse;
    private int zurueckgezogen;
    private int vonInseln;
    private int vonHighs;
    private string? zuletztVon;
    private int zaehlerAnhebungen;
    private Stufeneinteilung einteilung;
    private Stufe[] stufen;
    private int[] reihenfolgeC;

    /// <summary>Festlegung des Staffelleiters: je Kriterium die Obergrenze der Verstöße (−1 = keine); <c>null</c> = keine.</summary>
    private int[]? obergrenze;

    /// <summary>Was festgelegt wurde (für die Anzeige), z. B. „Heimkoppel bestmöglich“.</summary>
    private string? festgelegt;
    private Kostenkriterium[] draengKriterien = [];
    private Kostenkriterium? fokus;
    private Kostenkriterium? verschoben;
    private double verschobenBis;
    private double zuletzt;
    private bool sofort;

    /// <summary>
    /// Ein Plan aus HiGHS ist bester Plan geworden (seit dem letzten Lenken): A und B gelten dann als erreicht – HiGHS hat in
    /// seiner Zeit nichts Besseres gefunden, die Inseln mit ihren abgestuften Gewichten finden es kaum. Sie werden geschützt, und
    /// die Suche beginnt beim besten Plan, statt mit ihren in A und B viel schlechteren Plänen weiterzurechnen.
    /// </summary>
    private bool vonHighsUebernommen;

    /// <summary>Legt den Automodus an.</summary>
    /// <param name="referenz">Referenzmodell mit den Gewichtungen des Anwenders.</param>
    /// <param name="definition">Stammdaten dazu.</param>
    /// <param name="einstellungen">Einstellungen (Spielraum, Stufen).</param>
    public Automodus(RefPlan referenz, KernDefinition definition, Optimierungseinstellungen einstellungen)
    {
        bewerter = new KernPlan(referenz, definition);
        bewerter.UngueltigeTermineEntfernen();
        bewerter.Merken();
        ids = referenz.Mannschaften.Select(m => m.TeamId).ToArray();
        namen = definition.Name;
        einteilung = (einstellungen.AutoStufen ?? Stufeneinteilung.Standard).Vervollstaendigt();
        stufen = einteilung.AlsFeld();
        zaehlen = einstellungen.AutoZaehlen;
        reihenfolgeC = einteilung.C.Select(k => (int)k).ToArray();
        spielraum = einstellungen.AutoSpielraum;
        kostenToleranz = einstellungen.AutoKostenToleranz;
        deutlich = einstellungen.AutoDeutlich;
        stillstandPlaene = einstellungen.AutoStillstandPlaene;
        maxGrundlauf = einstellungen.AutoMaxGrundlauf;
        original = referenz.Optionen;
        Optionen = referenz.Optionen;
        basisMannschaft = new int[ids.Length * KernDefinition.AnzahlArten];
        Beste = new Loesung(bewerter.AnzahlErlaubt);
        basisLoesung = new Loesung(bewerter.AnzahlErlaubt);
    }

    private enum Phase
    {
        Grundlauf,
        Draengen,
        Glaetten,
    }

    /// <summary>Gets die aktuell geltenden Gewichtungen.</summary>
    public Berechnungsoptionen Optionen { get; private set; }

    /// <summary>Gets a value indicating whether noch der Grundlauf läuft (keine Lenkung).</summary>
    public bool Grundlauf => phase == Phase.Grundlauf;

    /// <summary>Holt einen Wert, der angibt, ob C geglättet wird (A und B sind dann entschieden).</summary>
    public bool Glaettet => phase == Phase.Glaetten;

    /// <summary>Holt einen Wert, der angibt, ob die Suche gerade Verstöße zählt statt Kosten (beim Glätten).</summary>
    public bool Zaehlt => zaehlen && phase == Phase.Glaetten;

    /// <summary>Holt die Stufe je Kriterium (Index = <see cref="Kostenkriterium"/>).</summary>
    public IReadOnlyList<Stufe> Stufen => stufen;

    /// <summary>Gets den besten Plan nach Stufen (gültig nach dem Grundlauf).</summary>
    public Loesung Beste { get; }

    /// <summary>Gets seine Kosten mit den Gewichtungen des Anwenders.</summary>
    public double BesteKosten { get; private set; }

    /// <summary>Gets seine Bewertung nach Stufen.</summary>
    public Stufenwert BesterWert { get; private set; }

    /// <summary>Gets die Anzahl der Verbesserungen des besten Plans nach Stufen seit dem Grundlauf.</summary>
    public int Verbesserungen { get; private set; }

    /// <summary>Gets die Bewertung des Plans der Suche beim letzten <see cref="Verfolgen"/>.</summary>
    public Stufenwert? Suche { get; private set; }

    /// <summary>Gets die Bewertung am Ende des Grundlaufs.</summary>
    public Stufenwert? Basis { get; private set; }

    /// <summary>Gets or sets a value indicating whether die Suche nach der nächsten Änderung beim besten Plan neu beginnen soll (sonst beim aktuellen Plan der Suche).</summary>
    public bool NeustartVomBesten { get; set; }

    /// <summary>Gets den aktuellen Stand für Anzeige und Auswertung.</summary>
    public Lenkungsstand Stand => new(
        Basis,
        BesterWert,
        Suche,
        PhaseText,
        Bezeichnung,
        zaehlerAnhebungen,
        zurueckgezogen,
        Optionen,
        aenderungen.ToArray(),
        AktuelleKriterien,
        Anhebungsliste(),
        schranke.A,
        schranke.B,
        vonInseln,
        vonHighs,
        zuletztVon,
        festgelegt);

    /// <summary>Rang der laufenden Phase wie bei <see cref="ErsteAenderung"/>: Drängen A = 0, Drängen B = 1, Glätten des i-ten C-Kriteriums = 2 + i.</summary>
    private int Rang => phase switch
    {
        Phase.Draengen => draengStufe == Stufe.A ? 0 : 1,
        Phase.Glaetten => 2 + indexC,
        _ => -1,
    };

    /// <summary>
    /// Die Kriterien, an denen gerade gearbeitet wird: beim Drängen die zuletzt angehobenen, beim Glätten das aktuelle
    /// C-Kriterium; ein eben verschobenes Kriterium steht noch <see cref="AnzeigeVerschoben"/> Sekunden vorn.
    /// </summary>
    private Kostenkriterium[] AktuelleKriterien
    {
        get
        {
            Kostenkriterium[] aktuell = phase switch
            {
                Phase.Draengen => draengKriterien,
                Phase.Glaetten when indexC < reihenfolgeC.Length => [(Kostenkriterium)reihenfolgeC[indexC]],
                _ => [],
            };
            return verschoben is Kostenkriterium v && zuletzt < verschobenBis && !aktuell.Contains(v) ? [v, .. aktuell] : aktuell;
        }
    }

    /// <summary>Bezeichnung der Phase für die Oberfläche: Basisoptimierung, dann Optimierung der Stufe A, B oder C.</summary>
    private string Bezeichnung => phase switch
    {
        Phase.Grundlauf => "Basisoptimierung",
        Phase.Draengen => draengStufe == Stufe.A ? "Optimierung Stufe A" : "Optimierung Stufe B",
        _ => "Optimierung Stufe C",
    };

    private string PhaseText => phase switch
    {
        Phase.Grundlauf => "Grundlauf",
        Phase.Draengen => "Drängen",
        _ => indexC < reihenfolgeC.Length ? $"Glätten {(Kostenkriterium)reihenfolgeC[indexC]}" : "Glätten",
    };

    /// <summary>Übernimmt die Zahl der bisher berechneten Pläne (für den Stillstand nach Rechenleistung statt nur nach Zeit).</summary>
    /// <param name="berechnet">Berechnete Pläne seit dem Start.</param>
    public void Zaehlen(long berechnet) => plaene = berechnet;

    /// <summary>
    /// Merkt sich im Grundlauf, wann die Suche (Kostenoptimierung) zuletzt deutlich besser geworden ist: um mindestens
    /// <see cref="Optimierungseinstellungen.AutoDeutlich"/> gegenüber der letzten deutlichen Verbesserung. Kleine
    /// Verbesserungen (etwa alle paar Sekunden ein paar Kosten weniger) halten die Basisoptimierung so nicht fest.
    /// </summary>
    /// <param name="besteKosten">Beste Kosten der Suche mit den aktuellen Gewichten.</param>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    public void Beobachten(double besteKosten, double jetzt)
    {
        if (Grundlauf && besteKosten >= 0 && (deutlicheKosten < 0 || besteKosten < deutlicheKosten * (1 - deutlich)))
        {
            deutlicheKosten = besteKosten;
            UhrNeu(jetzt);
        }
    }

    /// <summary>
    /// Der Grundlauf hat die Mindestdauer von 10 s erreicht (so lange läuft die Basisoptimierung auch, wenn der Plan aus HiGHS
    /// schon früher vorliegt – ihr bester Plan ist der Maßstab für die C-Ausreißer).
    /// </summary>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    /// <returns><c>true</c> ab 10 s.</returns>
    public bool GrundlaufLangGenug(double jetzt) => Grundlauf && jetzt >= MinGrundlauf;

    /// <summary>
    /// Der Grundlauf ist vorbei, wenn die Basisoptimierung steht (nach mindestens 10 s; 10 s und
    /// <see cref="Optimierungseinstellungen.AutoStillstandPlaene"/> Pläne ohne deutliche Verbesserung), spätestens nach
    /// <see cref="Optimierungseinstellungen.AutoMaxGrundlauf"/>.
    /// </summary>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    /// <returns><c>true</c>, wenn die Lenkung beginnen kann.</returns>
    public bool GrundlaufVorbei(double jetzt) => jetzt >= MinGrundlauf && (Steht(jetzt) || jetzt >= maxGrundlauf);

    /// <summary>
    /// Beendet den Grundlauf: der beste Plan wird Maßstab je Mannschaft und C-Kriterium. Die erste Änderung der Gewichte
    /// folgt sofort – die Suche steht ja schon (Ende der Basisoptimierung) bzw. der Plan ist schon optimiert (Wechsel von
    /// der Kostenoptimierung); ein weiteres Abwarten zeigte nur lange „Stufe A“ ohne Kriterium.
    /// </summary>
    /// <param name="beste">Bester Plan des Grundlaufs.</param>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    public void GrundlaufBeenden(Loesung beste, double jetzt)
    {
        Auswertung basis = Auswerten(beste);
        Stufenwert wert = basis.Wert;
        double gesamt = basis.Gesamt;
        for (int s = 0; s < basisMannschaft.Length; s++)
        {
            basisMannschaft[s] = Math.Max(0, basis.Mannschaft[s]);
        }

        double spielraumKosten = SpielraumKosten * Math.Max(1, wert.C);
        for (int k = 0; k < stufen.Length; k++)
        {
            grenzeKosten[k] = Math.Max(basis.Kosten[k] * (1 + kostenToleranz), basis.Kosten[k] + spielraumKosten);
        }

        phase = Phase.Draengen;
        draengStufe = Stufe.A;
        indexC = 0;
        angehobenC.Clear();
        bezugWert = wert;
        Basis = wert;
        beste.KopierenNach(Beste);
        beste.KopierenNach(basisLoesung);
        BesterWert = wert;
        BesteKosten = gesamt;
        letzteAenderung = jetzt;
        UhrNeu(jetzt);
        sofort = true;
        sucheVerstoesse = null;
    }

    /// <summary>
    /// Der Staffelleiter hat die Einteilung geändert (während der Generierung): Keine neue Basisoptimierung, die Gewichte
    /// bleiben. Basis und bester Plan werden nach der neuen Einteilung bewertet, und der Automodus macht beim wichtigsten
    /// geänderten Kriterium weiter – in Stufe A oder B mit dem Drängen dieser Stufe, in C mit dem Glätten ab diesem Kriterium –,
    /// sofern er nicht ohnehin noch davor steht. Zählt die Suche schon die Verstöße, zählt sie gleich nach der neuen Einteilung
    /// weiter. Die Inseln beginnen in jedem Fall beim besten Plan neu.
    /// </summary>
    /// <param name="neu">Die neue Einteilung.</param>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    public void EinteilungAendern(Stufeneinteilung neu, double jetzt)
    {
        ArgumentNullException.ThrowIfNull(neu);
        Stufeneinteilung alt = einteilung;
        neu = neu.Vervollstaendigt();
        if (neu.Text() == alt.Text())
        {
            return;
        }

        einteilung = neu;
        stufen = neu.AlsFeld();
        reihenfolgeC = neu.C.Select(k => (int)k).ToArray();

        // Die Schranke gilt nur für die Rangfolge, mit der sie berechnet wurde.
        schranke = (null, null);
        if (Grundlauf)
        {
            return;
        }

        Basis = Bewerten(basisLoesung, out _);
        BesterWert = Bewerten(Beste, out double gesamt);
        BesteKosten = gesamt;
        sucheVerstoesse = null;

        // Die Inseln beginnen beim besten Plan neu und bewerten nach der neuen Einteilung (beim Zählen zählt ihr Ziel noch nach
        // der alten; mit ihren Plänen suchten sie sonst lange in die alte Richtung weiter).
        NeustartVomBesten = true;
        (int ziel, Kostenkriterium? geaendert) = ErsteAenderung(alt, neu);
        verschoben = geaendert;
        verschobenBis = jetzt + AnzeigeVerschoben;
        zuletzt = jetzt;
        if (Zaehlt)
        {
            sofort = true;
        }

        if (ziel > Rang || Zaehlt)
        {
            // Zählt die Suche schon (beim Glätten), zählt sie jetzt nach der neuen Einteilung – zuerst A, dann B, dann C. Zurück
            // zum Drängen mit den Kosten des Originals ginge es nicht: Damit tauschten die Inseln Verstöße in A gegen die
            // angehobenen Gewichte in B.
            Notieren($"{jetzt:0} s Einteilung geändert, bester Plan {BesterWert}: weiter bei {PhaseText}");
            return;
        }

        if (ziel <= 1)
        {
            phase = Phase.Draengen;
            draengStufe = ziel == 0 ? Stufe.A : Stufe.B;
            draengBester = null;
            ohneDraengfortschritt = 0;
            draengKriterien = geaendert is Kostenkriterium k ? [k] : [];
            fokus = geaendert;
        }
        else
        {
            phase = Phase.Glaetten;
            indexC = ziel - 2;
            angehobenC.Clear();
            bezugWert = BesterWert;
            fokus = null;
        }

        // Sofort lenken: nicht erst warten, bis die Suche wieder steht. Das verschobene Kriterium bleibt eine Weile sichtbar.
        sofort = true;
        Notieren($"{jetzt:0} s Einteilung geändert, bester Plan {BesterWert}: weiter bei {PhaseText}{(fokus is { } f ? " ab " + f : string.Empty)}");
    }

    /// <summary>Bewertet den Plan der Suche (bester nach den aktuellen Gewichten) für die Anzeige und den Stillstand.</summary>
    /// <param name="suche">Der Plan.</param>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    public void Verfolgen(Loesung suche, double jetzt)
    {
        Stufenwert wert = Bewerten(suche, out _);
        Suche = wert;
        zuletzt = jetzt;

        // Beobachtet wird, ob sich die Verstöße noch ändern: Jede Verbesserung der Anzahlen setzt die Uhr des Stillstands zurück.
        var verstoesse = Stufenvergleich.Schluessel(wert);
        if (!Grundlauf && (sucheVerstoesse is not { } bisher || verstoesse.CompareTo(bisher) < 0))
        {
            sucheVerstoesse = verstoesse;
            UhrNeu(jetzt);
        }
    }

    /// <summary>Übernimmt <paramref name="loesung"/> als besten Plan, wenn sie nach Stufen besser ist.</summary>
    /// <param name="loesung">Ein Plan (einer Insel oder aus HiGHS).</param>
    /// <returns>Seine Bewertung nach Stufen (für den Leitstand).</returns>
    public Stufenwert Pruefen(Loesung loesung)
    {
        letzte = Auswerten(loesung);
        Stufenwert wert = letzte.Wert;
        if (Stufenvergleich.Besser(wert, BesterWert))
        {
            loesung.KopierenNach(Beste);
            BesterWert = wert;
            BesteKosten = letzte.Gesamt;
            Verbesserungen++;
        }

        return wert;
    }

    /// <summary>Wie <see cref="Pruefen"/> für den Plan einer Insel; merkt sich, wenn er bester Plan wird (Herkunft).</summary>
    /// <param name="loesung">Lösung der Insel.</param>
    /// <param name="insel">Nummer der Insel.</param>
    /// <returns>Ihre Bewertung nach Stufen.</returns>
    public Stufenwert PruefenVonInsel(Loesung loesung, int insel)
    {
        int vorher = Verbesserungen;
        Stufenwert wert = Pruefen(loesung);
        if (Verbesserungen > vorher)
        {
            vonInseln++;
            zuletztVon = $"Insel {insel}";
        }

        return wert;
    }

    /// <summary>
    /// Der Staffelleiter legt A und B fest (gewählte Alternative aus HiGHS): Kein Kriterium der Stufen A und B darf mehr
    /// Verstöße haben als angegeben – ein Plan, der eine Obergrenze überschreitet, zählt mit jedem Verstoß darüber wie ein harter
    /// Fehler. Danach wird nicht mehr gedrängt, nur noch C geglättet. <c>null</c> hebt die Festlegung auf.
    /// </summary>
    /// <param name="grenzen">Je Kriterium die Obergrenze, oder <c>null</c>.</param>
    /// <param name="name">Was festgelegt wurde (für die Anzeige).</param>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    public void Festlegen(IReadOnlyDictionary<Kostenkriterium, int>? grenzen, string? name, double jetzt)
    {
        if (grenzen is null)
        {
            obergrenze = null;
            festgelegt = null;
            Notieren($"{jetzt:0} s Festlegung aufgehoben");
        }
        else
        {
            obergrenze = Enumerable.Repeat(-1, Stufeneinteilung.AnzahlKriterien).ToArray();
            foreach ((Kostenkriterium k, int wert) in grenzen)
            {
                obergrenze[(int)k] = wert;
            }

            festgelegt = name;
            Notieren($"{jetzt:0} s Festgelegt: {name} – A und B bleiben so, C wird geglättet");
        }

        if (Grundlauf)
        {
            return;
        }

        // Neu bewerten und von vorn: Mit Festlegung erreicht der beste Plan sie, und es geht sofort ans Glätten.
        Basis = Bewerten(basisLoesung, out _);
        BesterWert = Bewerten(Beste, out double gesamt);
        BesteKosten = gesamt;
        sucheVerstoesse = null;
        phase = Phase.Draengen;
        draengStufe = Stufe.A;
        draengBester = null;
        ohneDraengfortschritt = 0;
        draengKriterien = [];
        fokus = null;
        sofort = true;
    }

    /// <summary>
    /// Übernimmt die untere Schranke aus HiGHS: Die Stufe, die gerade gedrängt wird, endet, sobald der beste Plan bei ihr ist
    /// (sofort geprüft, ohne Stillstand abzuwarten).
    /// </summary>
    /// <param name="a">Bewiesene Schranke der Summe der Stufe A, oder <c>null</c>.</param>
    /// <param name="b">Bewiesene Schranke der Summe der Stufe B (bei bestmöglichem A), oder <c>null</c>.</param>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    public void SchrankenSetzen(int? a, int? b, double jetzt)
    {
        schranke = (a, a is null ? null : b);
        sofort = !Grundlauf;
        Notieren($"{jetzt:0} s Schranke aus HiGHS: A ≥ {a?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?"}, B ≥ {schranke.B?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?"}");
    }

    /// <summary>Schreibt einen Schritt ins Protokoll (für die Anzeige im Leitstand).</summary>
    /// <param name="text">Der Schritt mit Zeitpunkt.</param>
    public void Melden(string text) => Notieren(text);

    /// <summary>Bewertet einen Plan nach Stufen wie den besten Plan (mit C-Ausreißern gegenüber der Basis).</summary>
    /// <param name="plan">Der Plan.</param>
    /// <returns>Die Bewertung.</returns>
    public Stufenwert NachStufen(Loesung plan) => Bewerten(plan, out _);

    /// <summary>
    /// Plan von außen (aus HiGHS oder der Teilneuplanung): wird bester Plan, wenn er nach Stufen besser ist.
    /// </summary>
    /// <param name="plan">Der Plan.</param>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    /// <param name="herkunft">Für das Protokoll, z. B. „Plan aus HiGHS“.</param>
    /// <param name="sofortLenken">Ist er besser, gleich lenken (beim Plan aus HiGHS: Schranke prüfen).</param>
    /// <returns><c>true</c>, wenn er bester Plan geworden ist.</returns>
    public bool Einspielen(Loesung plan, double jetzt, string herkunft, bool sofortLenken)
    {
        int vorher = Verbesserungen;
        Stufenwert wert = Pruefen(plan);
        bool besser = Verbesserungen > vorher;
        if (besser)
        {
            vonHighs++;
            zuletztVon = herkunft.StartsWith("Teilneuplanung", StringComparison.Ordinal) ? "Teilneuplanung" : "HiGHS";
        }

        Notieren($"{jetzt:0} s {herkunft} ({wert}): " + (besser ? "bester Plan" : "nicht besser"));
        sofort |= besser && sofortLenken;
        vonHighsUebernommen |= besser && sofortLenken;
        return besser;
    }

    /// <summary>
    /// Die Kriterien mit den meisten Verstößen im zuletzt bewerteten Plan (<see cref="Pruefen"/>): wichtigste Stufe zuerst,
    /// innerhalb der Stufe die meisten Verstöße zuerst.
    /// </summary>
    /// <param name="hoechstens">Höchstens so viele.</param>
    /// <returns>Die Kriterien mit ihren Verstößen.</returns>
    public Kostenverursacher[] Verursacher(int hoechstens)
    {
        if (letzte is not { Anzahl: { } anzahl })
        {
            return [];
        }

        int[] auswahl = Enumerable.Range(0, stufen.Length)
            .Where(k => Stufeneinteilung.HatAnzahl((Kostenkriterium)k) && anzahl[k] > 0)
            .OrderBy(k => stufen[k])
            .ThenByDescending(k => anzahl[k])
            .Take(hoechstens)
            .ToArray();
        double groesste = auswahl.Select(k => anzahl[k]).DefaultIfEmpty(1).Max();
        return auswahl.Select(k => new Kostenverursacher((Kostenkriterium)k, anzahl[k] / groesste, (int)anzahl[k])).ToArray();
    }

    /// <summary>Die Verstöße je Kriterium im besten Plan nach Stufen mit der Stufe des Kriteriums.</summary>
    /// <returns>Alle Kriterien, die Verstöße zählen.</returns>
    public Kriterienwert[] BesteKriterien() => KriterienVon(Beste);

    /// <summary>Die Verstöße je Kriterium eines Plans mit der Stufe des Kriteriums (z. B. des besten Plans im Grundlauf).</summary>
    /// <param name="loesung">Der Plan.</param>
    /// <returns>Alle Kriterien, die Verstöße zählen.</returns>
    public Kriterienwert[] KriterienVon(Loesung loesung)
    {
        double[] anzahl = Auswerten(loesung).Anzahl;
        return Enumerable.Range(0, stufen.Length)
            .Where(k => Stufeneinteilung.HatAnzahl((Kostenkriterium)k))
            .Select(k => new Kriterienwert((Kostenkriterium)k, anzahl[k], stufen[k]))
            .ToArray();
    }

    /// <summary>
    /// Beim Glätten: Wird der Plan der Suche durch die angehobenen C-Gewichte in A oder B schlechter als der Bezug, wird nicht
    /// auf den Stillstand gewartet – die Anhebung wird beim nächsten Lenken zurückgezogen (frühestens nach der Mindestdauer
    /// einer Phase), und die Inseln beginnen wieder beim besten Plan. Sonst wanderten sie lange zu Plänen, die in C billiger,
    /// in A oder B aber schlechter sind und nie bester Plan werden.
    /// </summary>
    /// <param name="suche">Bester Plan nach den aktuellen Gewichten.</param>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    public void GlaettenPruefen(Loesung suche, double jetzt)
    {
        if (phase == Phase.Glaetten && angehobenC.Count > 0 && jetzt - letzteAenderung >= MinPhase
            && Stufenvergleich.AOderBSchlechter(Auswerten(suche).Wert, bezugWert))
        {
            sofort = true;
        }
    }

    /// <summary>Entscheidet über die nächste Änderung der Gewichte, wenn die Suche steht (oder nach der Höchstdauer).</summary>
    /// <param name="suche">Bester Plan nach den aktuellen Gewichten.</param>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    /// <returns>Die neuen Gewichtungen; <c>null</c> = keine Änderung.</returns>
    public Berechnungsoptionen? Lenken(Loesung suche, double jetzt)
    {
        if (Grundlauf || (!sofort && (jetzt - letzteAenderung < MinPhase || !Steht(jetzt))))
        {
            return null;
        }

        sofort = false;

        letzteAenderung = jetzt;

        // Was sich ändert, bestimmt der Plan der Suche (wie ihn ein Anwender in der Oberfläche sieht).
        // Erst der beste Plan gegen die Schranke aus HiGHS: Ist eine Stufe dort, ist bei ihr nichts mehr zu holen.
        bool highs = vonHighsUebernommen && BesterWert.Harte == 0;
        vonHighsUebernommen = false;
        amZiel = (AmZiel(Stufe.A) || highs, AmZiel(Stufe.B) || highs);
        Auswertung plan = Auswerten(suche);
        Berechnungsoptionen neu = phase switch
        {
            Phase.Draengen when SchrankeErreicht() is { } erreicht => erreicht,
            Phase.Draengen => DraengenSchritt(plan),
            _ when zaehlen => Optionen,
            _ => GlaettenSchritt(plan, Optionen),
        };
        if (neu == Optionen && !NeustartVomBesten)
        {
            return null;
        }

        Optionen = neu;
        sucheVerstoesse = null;
        UhrNeu(jetzt);
        return neu;
    }

    /// <summary>Eine Stufe höher, mindestens „normal“, höchstens „extrem hoch“.</summary>
    private static Gewichtung Hoeher(Gewichtung g) => g < Gewichtung.Normal ? Gewichtung.Normal : (Gewichtung)Math.Min((int)g + 1, (int)Gewichtung.ExtremHoch);

    /// <summary>Schlüssel eines Gewichts für das ganze Kriterium (alle Mannschaften bzw. Plan-Gewichtung).</summary>
    private static int Gesamtschluessel(int kriterium) => -kriterium - 1;

    /// <summary>Setzt das Gewicht einer Kostenart für alle Mannschaften.</summary>
    private static Berechnungsoptionen GesamtSetzen(Berechnungsoptionen optionen, int art, Gewichtung wert)
    {
        Gewichtung[] je = optionen.JeKostenart.ToArray();
        je[art] = wert;
        return optionen with { JeKostenart = je };
    }

    private static bool Anhebbar(Gewichtung g) => g is not Gewichtung.NichtBeruecksichtigen and < Gewichtung.ExtremHoch;

    /// <summary>
    /// Rang der wichtigsten Änderung zwischen zwei Einteilungen: 0 = in Stufe A, 1 = in Stufe B, 2 + i = beim i-ten
    /// C-Kriterium (Stelle oder Stufe eines Kriteriums anders).
    /// </summary>
    private static (int Rang, Kostenkriterium? Kriterium) ErsteAenderung(Stufeneinteilung alt, Stufeneinteilung neu)
    {
        Kostenkriterium[] vorher = [.. alt.A, .. alt.B, .. alt.C];
        Kostenkriterium[] nachher = [.. neu.A, .. neu.B, .. neu.C];
        int i = 0;
        while (i < nachher.Length && i < vorher.Length && vorher[i] == nachher[i] && alt.StufeVon(vorher[i]) == neu.StufeVon(nachher[i]))
        {
            i++;
        }

        Kostenkriterium? kriterium = i < nachher.Length ? nachher[i] : null;
        if (i < neu.A.Count)
        {
            return (0, kriterium);
        }

        return (i < neu.A.Count + neu.B.Count ? 1 : 2 + i - neu.A.Count - neu.B.Count, kriterium);
    }

    /// <summary>
    /// Die Gewichte, die gerade von denen des Anwenders abweichen: je Kriterium das Gewicht des ganzen Kriteriums und bei
    /// Kriterien mit Anzahl das Gewicht je Mannschaft.
    /// </summary>
    /// <returns>Die geänderten Gewichte.</returns>
    private Gewichtsanhebung[] Anhebungsliste()
    {
        var liste = new List<Gewichtsanhebung>();
        foreach (Kostenkriterium k in Enum.GetValues<Kostenkriterium>())
        {
            Vergleichen(liste, k, null, Gesamtschluessel((int)k));
            if (Stufeneinteilung.HatAnzahl(k) && (int)k < KernDefinition.AnzahlArten)
            {
                for (int m = 0; m < ids.Length; m++)
                {
                    Vergleichen(liste, k, namen[m], (m * KernDefinition.AnzahlArten) + (int)k);
                }
            }
        }

        return liste.ToArray();
    }

    private void Vergleichen(List<Gewichtsanhebung> liste, Kostenkriterium k, string? mannschaft, int schluessel)
    {
        Gewichtung vorher = Gewicht(original, schluessel);
        Gewichtung jetzt = Gewicht(Optionen, schluessel);
        if (vorher != jetzt)
        {
            liste.Add(new Gewichtsanhebung(k, mannschaft, vorher, jetzt));
        }
    }

    /// <summary>Startet die Uhr des Stillstands neu: Zeit und Zahl der berechneten Pläne der letzten Verbesserung.</summary>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    private void UhrNeu(double jetzt)
    {
        letzteSuche = jetzt;
        plaeneLetzteSuche = plaene;
    }

    /// <summary>
    /// Die Suche steht: seit der letzten Verbesserung mindestens 10 s und
    /// <see cref="Optimierungseinstellungen.AutoStillstandPlaene"/> berechnete Pläne. So sucht ein langsamer Rechner ebenso
    /// viel wie ein schneller, bevor der Automodus entscheidet.
    /// </summary>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    /// <returns><c>true</c>, wenn die Suche als festgefahren gilt.</returns>
    private bool Steht(double jetzt) => jetzt - letzteSuche >= Stillstand && plaene - plaeneLetzteSuche >= stillstandPlaene;

    /// <summary>
    /// Drängen, erst Stufe A, dann Stufe B (die A-Gewichte bleiben stehen): Verstöße der Stufe im Plan der Suche herausdrängen,
    /// bis bei ihr nichts mehr geht (nichts mehr anzuheben oder dreimal kein Fortschritt); dann beginnt
    /// das Glätten beim besten Plan.
    /// </summary>
    private Berechnungsoptionen DraengenSchritt(Auswertung plan)
    {
        Stufenwert wert = plan.Wert;

        // Nach einer geänderten Einteilung zuerst nur das wichtigste geänderte Kriterium, dann die ganze Stufe.
        if (fokus is Kostenkriterium f)
        {
            (int, int, int) fokusStand = (wert.Harte, (int)plan.Anzahl[(int)f], 0);
            if (draengBester is not { } vorher || fokusStand.CompareTo(vorher) < 0)
            {
                draengBester = fokusStand;
                ohneDraengfortschritt = 0;
            }
            else
            {
                ohneDraengfortschritt++;
            }

            (Berechnungsoptionen Neu, int[] Ziele) nurFokus = Draengen(draengStufe, plan);
            if (nurFokus.Neu != Optionen && ohneDraengfortschritt < DraengenOhneFortschritt)
            {
                return Angehoben(draengStufe, nurFokus);
            }

            Notieren($"{letzteAenderung:0} s bei {f} geht nichts mehr ({wert}): ganze Stufe {draengStufe}");
            fokus = null;
            draengBester = null;
            ohneDraengfortschritt = 0;
        }

        (int, int, int) stand = (wert.Harte, wert.A, draengStufe == Stufe.B ? wert.B : 0);
        if (draengBester is not { } bisher || stand.CompareTo(bisher) < 0)
        {
            draengBester = stand;
            ohneDraengfortschritt = 0;
        }
        else
        {
            ohneDraengfortschritt++;
        }

        (Berechnungsoptionen Neu, int[] Ziele) anhebung = Draengen(draengStufe, plan);
        if (anhebung.Neu != Optionen && ohneDraengfortschritt < DraengenOhneFortschritt)
        {
            return Angehoben(draengStufe, anhebung);
        }

        if (draengStufe == Stufe.A)
        {
            // Bei A geht nichts mehr: Stufe B, die A-Gewichte bleiben stehen.
            draengStufe = Stufe.B;
            draengBester = null;
            ohneDraengfortschritt = 0;
            Notieren($"{letzteAenderung:0} s bei A geht nichts mehr ({wert}): Stufe B");
            anhebung = Draengen(Stufe.B, plan);
            if (anhebung.Neu != Optionen)
            {
                return Angehoben(Stufe.B, anhebung);
            }
        }

        // Bei A und B geht nichts mehr (eine zuletzt berechnete, aber verworfene Anhebung gilt nicht). Hat der beste Plan keine
        // C-Verstöße mehr, deren Gewicht steigen kann, gibt es nichts zu glätten: Die Suche läuft mit den aktuellen Gewichten
        // ruhig weiter.
        Auswertung beste = Auswerten(Beste);
        if (!reihenfolgeC.Any(k => ZieleC(k, Optionen, beste).Length > 0))
        {
            return Optionen;
        }

        // Sonst C glätten, beginnend beim besten Plan und beim wichtigsten C-Kriterium.
        Notieren($"{letzteAenderung:0} s bei A und B geht nichts mehr, bester Plan {BesterWert}: C glätten");
        return GlaettenBeginnen(Optionen, beste);
    }

    /// <summary>C glätten, beginnend beim besten Plan und beim wichtigsten C-Kriterium.</summary>
    private Berechnungsoptionen GlaettenBeginnen(Berechnungsoptionen neu, Auswertung beste)
    {
        phase = Phase.Glaetten;
        indexC = 0;
        angehobenC.Clear();
        bezugWert = BesterWert;
        NeustartVomBesten = true;
        if (zaehlen)
        {
            // Die Suche zählt jetzt die Verstöße in C selbst: keine C-Gewichte anheben.
            Notieren($"{letzteAenderung:0} s Suche zählt Verstöße (C)");
            return neu;
        }

        return GlaettenSchritt(beste, neu);
    }

    /// <summary>
    /// Drängen mit Schranke: Ist die Stufe, die gerade gedrängt wird, im besten Plan bei der Schranke aus HiGHS, ist bei ihr
    /// nichts mehr zu holen. Ihre Gewichte gehen auf „extrem hoch“ (damit die Suche dort bleibt), und es geht mit Stufe B bzw.
    /// dem Glätten von C weiter – ohne die drei Versuche ohne Fortschritt abzuwarten.
    /// </summary>
    /// <returns>Die neuen Gewichtungen oder <c>null</c>, wenn die Stufe nicht bei der Schranke ist.</returns>
    private Berechnungsoptionen? SchrankeErreicht()
    {
        if (fokus is not null || !(draengStufe == Stufe.A ? amZiel.A : amZiel.B))
        {
            return null;
        }

        // Geschützt wird der beste Plan: Die Suche beginnt bei ihm (nicht bei ihrem Plan, der in der Stufe schlechter sein kann).
        Berechnungsoptionen neu = Schuetzen(Optionen, draengStufe);
        NeustartVomBesten = true;
        draengBester = null;
        ohneDraengfortschritt = 0;
        if (draengStufe == Stufe.A)
        {
            Notieren($"{letzteAenderung:0} s Stufe A bei der Schranke bzw. aus HiGHS ({BesterWert}): A geschützt, Stufe B");
            draengStufe = Stufe.B;
            if (!amZiel.B)
            {
                return neu;
            }
        }

        Notieren($"{letzteAenderung:0} s Stufe B bei der Schranke bzw. aus HiGHS ({BesterWert}): B geschützt, C glätten");
        neu = Schuetzen(neu, Stufe.B);
        return GlaettenBeginnen(neu, Auswerten(Beste));
    }

    /// <summary>
    /// Ist der beste Plan in der Stufe bei ihrer Schranke (Summe der Verstöße, keine harten Fehler)? B nur, wenn auch A dort
    /// ist – die Schranke von B gilt nur bei bestmöglichem A.
    /// </summary>
    private bool AmZiel(Stufe stufe)
    {
        // Festgelegt: A und B sind entschieden, sobald der beste Plan die Obergrenzen einhält.
        if (obergrenze is not null)
        {
            return BesterWert.Harte == 0;
        }

        bool a = schranke.A is int sa && BesterWert.Harte == 0 && BesterWert.A <= sa;
        return stufe == Stufe.A ? a : a && schranke.B is int sb && BesterWert.B <= sb;
    }

    /// <summary>Verstöße einer Stufe im zuletzt bewerteten Plan herausdrängen: ihre Gewichte eine Stufe höher.</summary>
    private (Berechnungsoptionen Neu, int[] Ziele) Draengen(Stufe stufe, Auswertung plan)
    {
        Berechnungsoptionen neu = Optionen;
        int[] ziele = Ziele(stufe, plan);
        foreach (int schluessel in ziele)
        {
            neu = Setzen(neu, schluessel, Hoeher(Gewicht(neu, schluessel)));
        }

        return (neu, ziele);
    }

    /// <summary>Eine Anhebung beim Drängen gilt: Kriterien merken, zählen, protokollieren.</summary>
    private Berechnungsoptionen Angehoben(Stufe stufe, (Berechnungsoptionen Neu, int[] Ziele) anhebung)
    {
        var betroffen = anhebung.Ziele.Select(z => z >= 0 ? z % KernDefinition.AnzahlArten : -z - 1).ToHashSet();
        draengKriterien = einteilung.A.Concat(einteilung.B).Where(k => betroffen.Contains((int)k)).ToArray();
        zaehlerAnhebungen++;
        Protokoll($"{stufe} angehoben", anhebung.Neu, anhebung.Ziele);
        return anhebung.Neu;
    }

    /// <summary>
    /// Glätten: Steigen A oder B im Plan der Suche über den Stand zu Beginn des Kriteriums, wird dessen Anhebung zurückgezogen
    /// und beim besten Plan mit dem nächsten Kriterium weitergemacht. Sonst steigt für die ausreißenden Mannschaften des
    /// aktuellen Kriteriums das Gewicht; reißt keine mehr aus, kommt das nächste Kriterium dran.
    /// </summary>
    private Berechnungsoptionen GlaettenSchritt(Auswertung plan, Berechnungsoptionen neu)
    {
        Stufenwert wert = plan.Wert;
        bool schlechter = Stufenvergleich.AOderBSchlechter(wert, bezugWert);
        if (angehobenC.Count > 0 && schlechter)
        {
            // Ganz zurück auf den Stand vor dem Kriterium (auch wenn ein Gewicht mehrmals angehoben wurde).
            foreach ((int schluessel, Gewichtung vorher) in angehobenC)
            {
                neu = Setzen(neu, schluessel, vorher);
            }

            zurueckgezogen++;
            Notieren($"{letzteAenderung:0} s A/B steigen ({wert}): {(Kostenkriterium)reihenfolgeC[indexC]} zurückgezogen");
            NaechstesKriterium();
            plan = Auswerten(Beste);
        }

        while (indexC < reihenfolgeC.Length)
        {
            int k = reihenfolgeC[indexC];
            int[] ziele = ZieleC(k, neu, plan);
            if (ziele.Length > 0)
            {
                foreach (int schluessel in ziele)
                {
                    angehobenC.TryAdd(schluessel, Gewicht(neu, schluessel));
                    neu = Setzen(neu, schluessel, Hoeher(Gewicht(neu, schluessel)));
                }

                zaehlerAnhebungen++;
                Protokoll($"C {(Kostenkriterium)k} angehoben", neu, ziele);
                return neu;
            }

            NaechstesKriterium();
        }

        // Sind A und B bei der Schranke: von vorn glätten, statt wieder zu drängen.
        if (amZiel is (true, true))
        {
            // Nur melden und neu beginnen, wenn es beim Glätten noch etwas anzuheben gibt (sonst bei jedem Stillstand dieselbe Zeile).
            indexC = 0;
            angehobenC.Clear();
            bezugWert = BesterWert;
            if (reihenfolgeC.Any(k => ZieleC(k, neu, plan).Length > 0))
            {
                Notieren($"{letzteAenderung:0} s alle C-Kriterien durch: von vorn glätten");
                NeustartVomBesten = true;
            }

            return neu;
        }

        // Alle C-Kriterien durch (geglättet oder zurückgezogen): wieder drängen.
        Notieren($"{letzteAenderung:0} s alle C-Kriterien durch: wieder A/B drängen");
        phase = Phase.Draengen;
        draengStufe = Stufe.A;
        draengBester = null;
        ohneDraengfortschritt = 0;
        draengKriterien = [];
        fokus = null;
        return neu;
    }

    /// <summary>Alle Kriterien einer Stufe (je Mannschaft bzw. für den ganzen Plan) auf „extrem hoch“.</summary>
    private Berechnungsoptionen Schuetzen(Berechnungsoptionen optionen, Stufe stufe)
    {
        Berechnungsoptionen neu = optionen;
        foreach (int k in (stufe == Stufe.A ? einteilung.A : einteilung.B).Select(k => (int)k))
        {
            IEnumerable<int> schluessel = k >= KernDefinition.AnzahlArten
                ? [Gesamtschluessel(k)]
                : Enumerable.Range(0, ids.Length).Select(t => (t * KernDefinition.AnzahlArten) + k);
            foreach (int z in schluessel.Where(z => Gewicht(neu, z) != Gewichtung.NichtBeruecksichtigen))
            {
                neu = Setzen(neu, z, Gewichtung.ExtremHoch);
            }
        }

        return neu;
    }

    private void NaechstesKriterium()
    {
        indexC++;
        angehobenC.Clear();
        bezugWert = BesterWert;
        NeustartVomBesten = true;
    }

    /// <summary>
    /// Was beim Glätten eines C-Kriteriums angehoben wird: zuerst die Ausreißer (Spitzen), sonst die Mannschaften mit den
    /// meisten Verstößen bei diesem Kriterium (Verstöße allgemein reduzieren); bei Plan-Kriterien das ganze Kriterium, solange
    /// es Verstöße bzw. Kosten hat. Nur Gewichte, die noch steigen können.
    /// </summary>
    private int[] ZieleC(int k, Berechnungsoptionen optionen, Auswertung plan)
    {
        int[] spitzen = AusreisserSchluessel(k, plan).Where(z => Anhebbar(Gewicht(optionen, z))).ToArray();
        if (spitzen.Length > 0)
        {
            return spitzen;
        }

        if (k >= KernDefinition.AnzahlArten)
        {
            int gesamt = Gesamtschluessel(k);
            return (plan.Anzahl[k] > 0 || (!Stufeneinteilung.HatAnzahl((Kostenkriterium)k) && plan.Kosten[k] > 0)) && Anhebbar(Gewicht(optionen, gesamt)) ? [gesamt] : [];
        }

        int[] mitVerstoss = Enumerable.Range(0, ids.Length)
            .Select(t => (t * KernDefinition.AnzahlArten) + k)
            .Where(s => plan.Mannschaft[s] > 0 && Anhebbar(Gewicht(optionen, s)))
            .ToArray();
        if (mitVerstoss.Length == 0)
        {
            return [];
        }

        int meiste = mitVerstoss.Max(s => plan.Mannschaft[s]);
        return mitVerstoss.Where(s => plan.Mannschaft[s] == meiste).ToArray();
    }

    /// <summary>
    /// Ausreißer eines C-Kriteriums im zuletzt bewerteten Plan: Mannschaften mit mehr als <c>spielraum</c> Verstößen mehr als
    /// im Grundlauf; bei Kriterien ohne Anzahl (Spieltage) der ganze Plan, wenn die Kosten über der Grenze liegen.
    /// </summary>
    private IEnumerable<int> AusreisserSchluessel(int k, Auswertung plan)
    {
        if (k >= KernDefinition.AnzahlArten)
        {
            if (!Stufeneinteilung.HatAnzahl((Kostenkriterium)k) && plan.Kosten[k] > grenzeKosten[k])
            {
                yield return Gesamtschluessel(k);
            }

            yield break;
        }

        if (plan.Anzahl[k] <= 0)
        {
            yield break;
        }

        for (int t = 0; t < ids.Length; t++)
        {
            int s = (t * KernDefinition.AnzahlArten) + k;
            if (plan.Mannschaft[s] > basisMannschaft[s] + spielraum)
            {
                yield return s;
            }
        }
    }

    /// <summary>Verstöße der Stufe A bzw. B im zuletzt bewerteten Plan, deren Gewicht noch angehoben werden kann.</summary>
    private int[] Ziele(Stufe stufe, Auswertung plan)
    {
        var ziele = new List<int>();
        for (int k = 0; k < stufen.Length; k++)
        {
            if (stufen[k] != stufe || plan.Anzahl[k] <= 0 || (fokus is Kostenkriterium f && (int)f != k))
            {
                continue;
            }

            if (k >= KernDefinition.AnzahlArten)
            {
                ziele.Add(Gesamtschluessel(k));
                continue;
            }

            for (int t = 0; t < ids.Length; t++)
            {
                int s = (t * KernDefinition.AnzahlArten) + k;
                if (plan.Mannschaft[s] > 0)
                {
                    ziele.Add(s);
                }
            }
        }

        return ziele.Where(z => Anhebbar(Gewicht(Optionen, z))).ToArray();
    }

    private void Protokoll(string was, Berechnungsoptionen optionen, int[] schluessel) =>
        Notieren($"{letzteAenderung:0} s {was}: " + string.Join(", ", schluessel.Select(z => Beschreibung(z) + " " + Gewicht(optionen, z))));

    /// <summary>Gewicht zu einem Schlüssel: ≥ 0 Mannschaft × Kostenart, &lt; 0 ganzes Kriterium.</summary>
    private Gewichtung Gewicht(Berechnungsoptionen optionen, int schluessel)
    {
        if (schluessel < 0)
        {
            return (Kostenkriterium)(-schluessel - 1) switch
            {
                Kostenkriterium.VereinsinterneSpieleAmAnfang => optionen.VereinsinterneSpieleAmAnfang,
                Kostenkriterium.Spieltaglaenge => optionen.Spieltag,
                Kostenkriterium.Spieltagueberlappung => optionen.SpieltagUeberlappung,
                Kostenkriterium.LetzterSpieltagLaenge => optionen.LetzterSpieltag,
                Kostenkriterium.LetzterSpieltagUeberlappung => optionen.LetzterSpieltagUeberlappung,
                Kostenkriterium k => optionen.Fuer((MannschaftsKostenart)(int)k),
            };
        }

        string id = ids[schluessel / KernDefinition.AnzahlArten];
        return optionen.Mannschaften.FirstOrDefault(m => m.MannschaftsId == id)?.JeKostenart[schluessel % KernDefinition.AnzahlArten] ?? Gewichtung.Normal;
    }

    private Berechnungsoptionen Setzen(Berechnungsoptionen optionen, int schluessel, Gewichtung wert)
    {
        if (schluessel < 0)
        {
            return (Kostenkriterium)(-schluessel - 1) switch
            {
                Kostenkriterium.VereinsinterneSpieleAmAnfang => optionen with { VereinsinterneSpieleAmAnfang = wert },
                Kostenkriterium.Spieltaglaenge => optionen with { Spieltag = wert },
                Kostenkriterium.Spieltagueberlappung => optionen with { SpieltagUeberlappung = wert },
                Kostenkriterium.LetzterSpieltagLaenge => optionen with { LetzterSpieltag = wert },
                Kostenkriterium.LetzterSpieltagUeberlappung => optionen with { LetzterSpieltagUeberlappung = wert },
                Kostenkriterium k => GesamtSetzen(optionen, (int)k, wert),
            };
        }

        string id = ids[schluessel / KernDefinition.AnzahlArten];
        var liste = optionen.Mannschaften.ToList();
        int index = liste.FindIndex(m => m.MannschaftsId == id);
        MannschaftsGewichtung alt = index >= 0
            ? liste[index]
            : new MannschaftsGewichtung(id, Gewichtung.Normal, Enumerable.Repeat(Gewichtung.Normal, Berechnungsoptionen.AnzahlKostenarten).ToArray());
        Gewichtung[] je = alt.JeKostenart.ToArray();
        je[schluessel % KernDefinition.AnzahlArten] = wert;
        MannschaftsGewichtung neu = alt with { JeKostenart = je };
        if (index >= 0)
        {
            liste[index] = neu;
        }
        else
        {
            liste.Add(neu);
        }

        return optionen with { Mannschaften = liste };
    }

    private string Beschreibung(int schluessel) => schluessel < 0
        ? $"{(Kostenkriterium)(-schluessel - 1)} (alle)"
        : $"{(MannschaftsKostenart)(schluessel % KernDefinition.AnzahlArten)} {namen[schluessel / KernDefinition.AnzahlArten]}";

    /// <summary>Bewertet eine Lösung mit den Gewichtungen des Anwenders je Kriterium und nach Stufen, mit den C-Ausreißern.</summary>
    /// <summary>Ein Schritt ins Protokoll; es behält nur die letzten <see cref="ProtokollHoechstens"/>.</summary>
    private void Notieren(string text)
    {
        aenderungen.Add(text);
        if (aenderungen.Count > ProtokollHoechstens)
        {
            aenderungen.RemoveRange(0, aenderungen.Count - ProtokollHoechstens);
        }
    }

    /// <summary>Bewertet einen Plan vollständig: nach Stufen (mit C-Ausreißern), je Kriterium und je Mannschaft × Kostenart.</summary>
    private Auswertung Auswerten(Loesung loesung)
    {
        bewerter.LoesungUebernehmen(loesung);
        bewerter.Zuruecksetzen();
        bewerter.Sortieren();
        double gesamt = bewerter.Kosten();
        double[] anzahl = new double[Stufeneinteilung.AnzahlKriterien];
        double[] kosten = new double[Stufeneinteilung.AnzahlKriterien];
        Stufenwert wert = bewerter.StufenBewerten(stufen, anzahl, kosten);
        var plan = new Auswertung(wert, gesamt, anzahl, kosten, [.. bewerter.StufenAnzahl]);
        if (Grundlauf)
        {
            return plan with { Wert = wert with { Harte = wert.Harte + Ueberschreitung(anzahl) } };
        }

        int ausreisser = reihenfolgeC.Sum(k => AusreisserSchluessel(k, plan).Count());
        return plan with { Wert = wert with { Ausreisser = ausreisser, Harte = wert.Harte + Ueberschreitung(anzahl) } };
    }

    /// <summary>Verstöße über den festgelegten Obergrenzen (0 ohne Festlegung).</summary>
    private int Ueberschreitung(double[] anzahl)
    {
        if (obergrenze is null)
        {
            return 0;
        }

        int ueber = 0;
        for (int k = 0; k < obergrenze.Length; k++)
        {
            if (obergrenze[k] >= 0 && anzahl[k] > obergrenze[k])
            {
                ueber += (int)Math.Ceiling(anzahl[k] - obergrenze[k]);
            }
        }

        return ueber;
    }

    /// <summary>Bewertet einen Plan nach Stufen (siehe <see cref="Auswerten"/>).</summary>
    private Stufenwert Bewerten(Loesung loesung, out double gesamt)
    {
        Auswertung plan = Auswerten(loesung);
        gesamt = plan.Gesamt;
        return plan.Wert;
    }

    /// <summary>Bewertung eines Plans: nach Stufen, Gesamtkosten, je Kriterium Anzahl und Kosten, je Mannschaft × Kostenart die Verstöße.</summary>
    private sealed record Auswertung(Stufenwert Wert, double Gesamt, double[] Anzahl, double[] Kosten, int[] Mannschaft);
}
