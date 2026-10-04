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
/// A-Gewichte bleiben beim Drängen von B stehen. Es gibt keine Zeitgrenzen.
/// </item>
/// <item>
/// Glätten, ab dem besten Plan: C-Kriterien nach Wichtigkeit, Verstöße allgemein reduzieren, Spitzen zuerst. Eine
/// Mannschaft reißt bei einem C-Kriterium aus, wenn sie mehr als zwei Verstöße mehr hat als im Grundlauf – ein, zwei sind
/// in Ordnung, zu viele bei einer Mannschaft sind unfair. Für die Ausreißer, sonst für die Mannschaften mit den meisten
/// Verstößen steigt das Gewicht des Kriteriums eine Stufe, so lange, bis nichts mehr anzuheben ist (dann das nächste
/// Kriterium) oder A oder B wieder steigen: Dann wird die Anhebung zurückgezogen, beim besten Plan neu begonnen und mit dem
/// nächsten Kriterium weitergemacht. Sind alle C-Kriterien durch, wird wieder gedrängt.
/// </item>
/// <item>
/// Der beste Plan wird immer mit den Gewichtungen des Anwenders bewertet: harte Fehler, A, B, C-Ausreißer, C-Verstöße, dann
/// C-Kosten (<see cref="Stufenwert"/>).
/// </item>
/// </list>
/// </summary>
internal sealed class Automodus
{
    /// <summary>Sekunden, denen ein Lauf mit fester Zahl von Durchläufen (<see cref="Inseloptimierer.Rechnen"/>) ohne Zeitbudget entspricht.</summary>
    public const double StandardBudget = 300;

    /// <summary>Mindestdauer des Grundlaufs in Sekunden.</summary>
    private const double MinGrundlauf = 10;

    /// <summary>Sekunden ohne Verbesserung der Suche, ab denen sie als festgefahren gilt.</summary>
    private const double Stillstand = 10;

    /// <summary>Mindestabstand zweier Änderungen in Sekunden.</summary>
    private const double MinPhase = 8;

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
    private readonly double[] anzahl = new double[Stufeneinteilung.AnzahlKriterien];
    private readonly double[] kosten = new double[Stufeneinteilung.AnzahlKriterien];
    private readonly double[] grenzeKosten = new double[Stufeneinteilung.AnzahlKriterien];
    private readonly int[] basisMannschaft;
    private readonly List<int> angehobenC = [];
    private readonly List<string> aenderungen = [];
    private Phase phase = Phase.Grundlauf;
    private (int, int, int)? draengBester;
    private int ohneDraengfortschritt;
    private int indexC;
    private Stufe draengStufe = Stufe.A;
    private (int A, int B) bezug;
    private double letzteAenderung;
    private double letzteSuche;
    private double sucheKosten = -1;
    private (int, int, int, int, int)? sucheVerstoesse;
    private double sekunden;
    private int zurueckgezogen;
    private int zaehlerAnhebungen;
    private Stufeneinteilung einteilung;
    private Stufe[] stufen;
    private int[] reihenfolgeC;
    private Kostenkriterium[] draengKriterien = [];
    private Kostenkriterium? fokus;
    private bool sofort;

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
        reihenfolgeC = einteilung.C.Select(k => (int)k).ToArray();
        spielraum = einstellungen.AutoSpielraum;
        kostenToleranz = einstellungen.AutoKostenToleranz;
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
        AktuelleKriterien);

    /// <summary>Rang der laufenden Phase wie bei <see cref="ErsteAenderung"/>: Drängen A = 0, Drängen B = 1, Glätten des i-ten C-Kriteriums = 2 + i.</summary>
    private int Rang => phase switch
    {
        Phase.Draengen => draengStufe == Stufe.A ? 0 : 1,
        Phase.Glaetten => 2 + indexC,
        _ => -1,
    };

    /// <summary>Die Kriterien, an denen gerade gearbeitet wird: beim Drängen die zuletzt angehobenen, beim Glätten das aktuelle C-Kriterium.</summary>
    private Kostenkriterium[] AktuelleKriterien => phase switch
    {
        Phase.Draengen => draengKriterien,
        Phase.Glaetten when indexC < reihenfolgeC.Length => [(Kostenkriterium)reihenfolgeC[indexC]],
        _ => [],
    };

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

    /// <summary>Merkt sich im Grundlauf, wann die Suche (Kostenoptimierung) zuletzt besser geworden ist.</summary>
    /// <param name="besteKosten">Beste Kosten der Suche mit den aktuellen Gewichten.</param>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    public void Beobachten(double besteKosten, double jetzt)
    {
        if (Grundlauf && besteKosten >= 0 && (sucheKosten < 0 || besteKosten < sucheKosten))
        {
            sucheKosten = besteKosten;
            letzteSuche = jetzt;
        }
    }

    /// <summary>Der Grundlauf ist vorbei, wenn die Basisoptimierung steht (nach mindestens 10 s, 10 s ohne Verbesserung).</summary>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    /// <returns><c>true</c>, wenn die Lenkung beginnen kann.</returns>
    public bool GrundlaufVorbei(double jetzt) => jetzt >= MinGrundlauf && jetzt - letzteSuche >= Stillstand;

    /// <summary>Beendet den Grundlauf: der beste Plan wird Maßstab je Mannschaft und C-Kriterium.</summary>
    /// <param name="beste">Bester Plan des Grundlaufs.</param>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    /// <param name="sofortLenken"><c>true</c>, um gleich die erste Änderung der Gewichte zu machen (ohne auf Stillstand zu warten).</param>
    public void GrundlaufBeenden(Loesung beste, double jetzt, bool sofortLenken = false)
    {
        Stufenwert wert = Bewerten(beste, out double gesamt);
        for (int s = 0; s < basisMannschaft.Length; s++)
        {
            basisMannschaft[s] = Math.Max(0, bewerter.KostenAnzahl[s]);
        }

        double spielraumKosten = SpielraumKosten * Math.Max(1, wert.C);
        for (int k = 0; k < stufen.Length; k++)
        {
            grenzeKosten[k] = Math.Max(kosten[k] * (1 + kostenToleranz), kosten[k] + spielraumKosten);
        }

        phase = Phase.Draengen;
        draengStufe = Stufe.A;
        Basis = wert;
        beste.KopierenNach(Beste);
        beste.KopierenNach(basisLoesung);
        BesterWert = wert;
        BesteKosten = gesamt;
        letzteAenderung = jetzt;
        letzteSuche = jetzt;
        sofort = sofortLenken;
        sucheVerstoesse = null;
    }

    /// <summary>
    /// Der Staffelleiter hat die Einteilung geändert (während der Generierung): Keine neue Basisoptimierung, die Gewichte
    /// bleiben. Basis und bester Plan werden nach der neuen Einteilung bewertet, und der Automodus macht beim wichtigsten
    /// geänderten Kriterium weiter – in Stufe A oder B mit dem Drängen dieser Stufe, in C mit dem Glätten ab diesem Kriterium –,
    /// sofern er nicht ohnehin noch davor steht.
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
        if (Grundlauf)
        {
            return;
        }

        Basis = Bewerten(basisLoesung, out _);
        BesterWert = Bewerten(Beste, out double gesamt);
        BesteKosten = gesamt;
        sucheVerstoesse = null;

        (int ziel, Kostenkriterium? geaendert) = ErsteAenderung(alt, neu);
        if (ziel > Rang)
        {
            aenderungen.Add($"{jetzt:0} s Einteilung geändert: weiter bei {PhaseText}");
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
            bezug = (BesterWert.A, BesterWert.B);
            fokus = null;
        }

        // Sofort lenken: nicht erst warten, bis die Suche wieder steht.
        sofort = true;
        aenderungen.Add($"{jetzt:0} s Einteilung geändert, bester Plan {BesterWert}: weiter bei {PhaseText}{(fokus is { } f ? " ab " + f : string.Empty)}");
    }

    /// <summary>Bewertet den Plan der Suche (bester nach den aktuellen Gewichten) für die Anzeige.</summary>
    /// <param name="suche">Der Plan.</param>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    public void Verfolgen(Loesung suche, double jetzt)
    {
        Stufenwert wert = Bewerten(suche, out _);
        Suche = wert;

        // Beobachtet wird, ob sich die Verstöße noch ändern: Jede Verbesserung der Anzahlen setzt die Uhr des Stillstands zurück.
        var verstoesse = (wert.Harte, wert.A, wert.B, wert.Ausreisser, wert.CAnzahl);
        if (!Grundlauf && (sucheVerstoesse is not { } bisher || verstoesse.CompareTo(bisher) < 0))
        {
            sucheVerstoesse = verstoesse;
            letzteSuche = jetzt;
        }
    }

    /// <summary>Übernimmt <paramref name="loesung"/> als besten Plan, wenn sie nach Stufen besser ist.</summary>
    /// <param name="loesung">Lösung einer Insel.</param>
    public void Pruefen(Loesung loesung)
    {
        Stufenwert wert = Bewerten(loesung, out double gesamt);
        if (wert.IstBesserAls(BesterWert))
        {
            loesung.KopierenNach(Beste);
            BesterWert = wert;
            BesteKosten = gesamt;
            Verbesserungen++;
        }
    }

    /// <summary>Entscheidet über die nächste Änderung der Gewichte, wenn die Suche steht (oder nach der Höchstdauer).</summary>
    /// <param name="suche">Bester Plan nach den aktuellen Gewichten.</param>
    /// <param name="jetzt">Sekunden reiner Rechenzeit.</param>
    /// <returns>Die neuen Gewichtungen; <c>null</c> = keine Änderung.</returns>
    public Berechnungsoptionen? Lenken(Loesung suche, double jetzt)
    {
        if (Grundlauf || (!sofort && (jetzt - letzteAenderung < MinPhase || jetzt - letzteSuche < Stillstand)))
        {
            return null;
        }

        sofort = false;

        letzteAenderung = jetzt;
        sekunden = jetzt;

        // Was sich ändert, bestimmt der Plan der Suche (wie ihn ein Anwender in der Oberfläche sieht).
        Stufenwert wert = Bewerten(suche, out _);
        Berechnungsoptionen neu = phase == Phase.Draengen ? DraengenSchritt(wert) : GlaettenSchritt(wert, Optionen);
        if (neu == Optionen && !NeustartVomBesten)
        {
            return null;
        }

        Optionen = neu;
        sucheVerstoesse = null;
        letzteSuche = jetzt;
        return neu;
    }

    /// <summary>Eine Stufe höher, mindestens „normal“, höchstens „extrem hoch“.</summary>
    private static Gewichtung Hoeher(Gewichtung g) => g < Gewichtung.Normal ? Gewichtung.Normal : (Gewichtung)Math.Min((int)g + 1, (int)Gewichtung.ExtremHoch);

    /// <summary>Eine Stufe niedriger, nicht unter die Gewichtung des Anwenders.</summary>
    private static Gewichtung Niedriger(Gewichtung g, Gewichtung untergrenze) => (Gewichtung)Math.Max((int)g - 1, (int)untergrenze);

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
    /// Drängen, erst Stufe A, dann Stufe B (die A-Gewichte bleiben stehen): Verstöße der Stufe im Plan der Suche herausdrängen,
    /// bis bei ihr nichts mehr geht (nichts mehr anzuheben oder dreimal kein Fortschritt); dann beginnt
    /// das Glätten beim besten Plan.
    /// </summary>
    private Berechnungsoptionen DraengenSchritt(Stufenwert wert)
    {
        // Nach einer geänderten Einteilung zuerst nur das wichtigste geänderte Kriterium, dann die ganze Stufe.
        if (fokus is Kostenkriterium f)
        {
            (int, int, int) fokusStand = (wert.Harte, (int)anzahl[(int)f], 0);
            if (draengBester is not { } vorher || fokusStand.CompareTo(vorher) < 0)
            {
                draengBester = fokusStand;
                ohneDraengfortschritt = 0;
            }
            else
            {
                ohneDraengfortschritt++;
            }

            Berechnungsoptionen nurFokus = Draengen(draengStufe);
            if (nurFokus != Optionen && ohneDraengfortschritt < DraengenOhneFortschritt)
            {
                return nurFokus;
            }

            aenderungen.Add($"{sekunden:0} s bei {f} geht nichts mehr ({wert}): ganze Stufe {draengStufe}");
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

        Berechnungsoptionen neu = Draengen(draengStufe);
        if (neu != Optionen && ohneDraengfortschritt < DraengenOhneFortschritt)
        {
            return neu;
        }

        if (draengStufe == Stufe.A)
        {
            // Bei A geht nichts mehr: Stufe B, die A-Gewichte bleiben stehen.
            draengStufe = Stufe.B;
            draengBester = null;
            ohneDraengfortschritt = 0;
            aenderungen.Add($"{sekunden:0} s bei A geht nichts mehr ({wert}): Stufe B");
            neu = Draengen(Stufe.B);
            if (neu != Optionen)
            {
                return neu;
            }
        }

        // Bei A und B geht nichts mehr. Hat der beste Plan keine C-Verstöße mehr, deren Gewicht steigen kann, gibt es nichts
        // zu glätten: Die Suche läuft mit den aktuellen Gewichten ruhig weiter.
        Bewerten(Beste, out _);
        if (!reihenfolgeC.Any(k => ZieleC(k, Optionen).Length > 0))
        {
            return neu;
        }

        // Sonst C glätten, beginnend beim besten Plan und beim wichtigsten C-Kriterium.
        phase = Phase.Glaetten;
        indexC = 0;
        angehobenC.Clear();
        bezug = (BesterWert.A, BesterWert.B);
        NeustartVomBesten = true;
        aenderungen.Add($"{sekunden:0} s bei A und B geht nichts mehr, bester Plan {BesterWert}: C glätten");
        return GlaettenSchritt(BesterWert, Optionen);
    }

    /// <summary>Verstöße einer Stufe im zuletzt bewerteten Plan herausdrängen: ihre Gewichte eine Stufe höher.</summary>
    private Berechnungsoptionen Draengen(Stufe stufe)
    {
        Berechnungsoptionen neu = Optionen;
        int[] ziele = Ziele(stufe);
        var betroffen = ziele.Select(z => z >= 0 ? z % KernDefinition.AnzahlArten : -z - 1).ToHashSet();
        draengKriterien = einteilung.A.Concat(einteilung.B).Where(k => betroffen.Contains((int)k)).ToArray();
        foreach (int schluessel in ziele)
        {
            neu = Setzen(neu, schluessel, Hoeher(Gewicht(neu, schluessel)));
        }

        if (ziele.Length > 0)
        {
            zaehlerAnhebungen++;
            Protokoll($"{stufe} angehoben", neu, ziele);
        }

        return neu;
    }

    /// <summary>
    /// Glätten: Steigen A oder B im Plan der Suche über den Stand zu Beginn des Kriteriums, wird dessen Anhebung zurückgezogen
    /// und beim besten Plan mit dem nächsten Kriterium weitergemacht. Sonst steigt für die ausreißenden Mannschaften des
    /// aktuellen Kriteriums das Gewicht; reißt keine mehr aus, kommt das nächste Kriterium dran.
    /// </summary>
    private Berechnungsoptionen GlaettenSchritt(Stufenwert wert, Berechnungsoptionen neu)
    {
        if (angehobenC.Count > 0 && (wert.A > bezug.A || wert.B > bezug.B + 1))
        {
            foreach (int schluessel in angehobenC.Distinct())
            {
                neu = Setzen(neu, schluessel, Niedriger(Gewicht(neu, schluessel), Gewicht(original, schluessel)));
            }

            zurueckgezogen++;
            aenderungen.Add($"{sekunden:0} s A/B steigen ({wert}): {(Kostenkriterium)reihenfolgeC[indexC]} zurückgezogen");
            NaechstesKriterium();
            Bewerten(Beste, out _);
        }

        while (indexC < reihenfolgeC.Length)
        {
            int k = reihenfolgeC[indexC];
            int[] ziele = ZieleC(k, neu);
            if (ziele.Length > 0)
            {
                foreach (int schluessel in ziele)
                {
                    neu = Setzen(neu, schluessel, Hoeher(Gewicht(neu, schluessel)));
                }

                angehobenC.AddRange(ziele);
                zaehlerAnhebungen++;
                Protokoll($"C {(Kostenkriterium)k} angehoben", neu, ziele);
                return neu;
            }

            NaechstesKriterium();
        }

        // Alle C-Kriterien durch (geglättet oder zurückgezogen): wieder drängen.
        aenderungen.Add($"{sekunden:0} s alle C-Kriterien durch: wieder A/B drängen");
        phase = Phase.Draengen;
        draengStufe = Stufe.A;
        draengBester = null;
        ohneDraengfortschritt = 0;
        draengKriterien = [];
        fokus = null;
        return neu;
    }

    private void NaechstesKriterium()
    {
        indexC++;
        angehobenC.Clear();
        bezug = (BesterWert.A, BesterWert.B);
        NeustartVomBesten = true;
    }

    /// <summary>
    /// Was beim Glätten eines C-Kriteriums angehoben wird: zuerst die Ausreißer (Spitzen), sonst die Mannschaften mit den
    /// meisten Verstößen bei diesem Kriterium (Verstöße allgemein reduzieren); bei Plan-Kriterien das ganze Kriterium, solange
    /// es Verstöße bzw. Kosten hat. Nur Gewichte, die noch steigen können.
    /// </summary>
    private int[] ZieleC(int k, Berechnungsoptionen optionen)
    {
        int[] spitzen = AusreisserSchluessel(k).Where(z => Anhebbar(Gewicht(optionen, z))).ToArray();
        if (spitzen.Length > 0)
        {
            return spitzen;
        }

        if (k >= KernDefinition.AnzahlArten)
        {
            int gesamt = Gesamtschluessel(k);
            return (anzahl[k] > 0 || (!Stufeneinteilung.HatAnzahl((Kostenkriterium)k) && kosten[k] > 0)) && Anhebbar(Gewicht(optionen, gesamt)) ? [gesamt] : [];
        }

        int[] mitVerstoss = Enumerable.Range(0, ids.Length)
            .Select(t => (t * KernDefinition.AnzahlArten) + k)
            .Where(s => bewerter.KostenAnzahl[s] > 0 && Anhebbar(Gewicht(optionen, s)))
            .ToArray();
        if (mitVerstoss.Length == 0)
        {
            return [];
        }

        int meiste = mitVerstoss.Max(s => bewerter.KostenAnzahl[s]);
        return mitVerstoss.Where(s => bewerter.KostenAnzahl[s] == meiste).ToArray();
    }

    /// <summary>
    /// Ausreißer eines C-Kriteriums im zuletzt bewerteten Plan: Mannschaften mit mehr als <c>spielraum</c> Verstößen mehr als
    /// im Grundlauf; bei Kriterien ohne Anzahl (Spieltage) der ganze Plan, wenn die Kosten über der Grenze liegen.
    /// </summary>
    private IEnumerable<int> AusreisserSchluessel(int k)
    {
        if (k >= KernDefinition.AnzahlArten)
        {
            if (!Stufeneinteilung.HatAnzahl((Kostenkriterium)k) && kosten[k] > grenzeKosten[k])
            {
                yield return Gesamtschluessel(k);
            }

            yield break;
        }

        if (anzahl[k] <= 0)
        {
            yield break;
        }

        for (int t = 0; t < ids.Length; t++)
        {
            int s = (t * KernDefinition.AnzahlArten) + k;
            if (bewerter.KostenAnzahl[s] > basisMannschaft[s] + spielraum)
            {
                yield return s;
            }
        }
    }

    /// <summary>Verstöße der Stufe A bzw. B im zuletzt bewerteten Plan, deren Gewicht noch angehoben werden kann.</summary>
    private int[] Ziele(Stufe stufe)
    {
        var ziele = new List<int>();
        for (int k = 0; k < stufen.Length; k++)
        {
            if (stufen[k] != stufe || anzahl[k] <= 0 || (fokus is Kostenkriterium f && (int)f != k))
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
                if (bewerter.KostenAnzahl[s] > 0)
                {
                    ziele.Add(s);
                }
            }
        }

        return ziele.Where(z => Anhebbar(Gewicht(Optionen, z))).ToArray();
    }

    private void Protokoll(string was, Berechnungsoptionen optionen, int[] schluessel) =>
        aenderungen.Add($"{sekunden:0} s {was}: " + string.Join(", ", schluessel.Select(z => Beschreibung(z) + " " + Gewicht(optionen, z))));

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
    private Stufenwert Bewerten(Loesung loesung, out double gesamt)
    {
        bewerter.LoesungUebernehmen(loesung);
        bewerter.Zuruecksetzen();
        bewerter.Sortieren();
        gesamt = bewerter.Kosten();
        Stufenwert wert = bewerter.StufenBewerten(stufen, anzahl, kosten);
        if (Grundlauf)
        {
            return wert;
        }

        int ausreisser = 0;
        foreach (int k in reihenfolgeC)
        {
            ausreisser += AusreisserSchluessel(k).Count();
        }

        return wert with { Ausreisser = ausreisser };
    }
}
