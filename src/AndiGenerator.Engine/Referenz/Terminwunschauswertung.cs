// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Kern;

namespace AndiGenerator.Engine.Referenz;

/// <summary>
/// Inhalt des Tabs „Terminwünsche“ im Original (<c>PaintTerminMeldung</c>, <c>PaintSpielPlanMeldungen</c>): je Mannschaft
/// ihre Heimspielwünsche mit Hallenbelegung und parallelen Spielen der Nachbarmannschaften, Sperrtermine, Auswärtskoppeln,
/// 60-km-Regel, Heimrecht und eine Auswertung, ob die Termine reichen. Unabhängig vom Plan, nur aus den Stammdaten.
/// </summary>
public static class Terminwunschauswertung
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Ermittelt die Übersicht.</summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="optionen">Berechnungsoptionen (Rundenplanung, Doppelrunde, Freitag bei 60 km).</param>
    /// <returns>Die Übersicht.</returns>
    public static Terminwunschuebersicht Ermitteln(Staffel staffel, Berechnungsoptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        RefPlan plan = RefPlan.Laden(staffel with { BestehenderSpielplan = [] }, optionen);
        List<double> wuensche = plan.Mannschaften.SelectMany(m => m.Wunschtermine).Select(w => w.Datum).Where(d => d != 0.0).ToList();
        DateOnly? erster = wuensche.Count == 0 ? null : Tag(wuensche.Min());
        DateOnly? letzter = wuensche.Count == 0 ? null : Tag(wuensche.Max());

        var hinweise = new List<string>();
        if (!plan.Mannschaften.Exists(m => m.KeinWochenspiel.Count > 0))
        {
            hinweise.Add("Keine Wünsche für die 60km-Regel");
        }

        if (!plan.HatKoppeltermine())
        {
            hinweise.Add("Keine Koppeltermine/Doppelspieltage gemeldet");
        }

        if (!plan.HatAuswaertsKoppeltermine())
        {
            hinweise.Add("Keine Auswärtskoppelwünsche gemeldet");
        }

        return new Terminwunschuebersicht(
            erster,
            letzter,
            plan.HatKoppeltermine(),
            hinweise,
            plan.Mannschaften.Select(m => new MannschaftsTerminwuensche(m.TeamName, Marken(m), Zeilen(plan, m))).ToList());
    }

    private static DateOnly Tag(double datum) => DateOnly.FromDateTime(DelphiDatum.ZuDateTime(datum));

    private static List<Terminwunschmarke> Marken(RefMannschaft m)
    {
        var marken = m.Wunschtermine
            .Where(w => !Terminoptionen.Hat(w.Optionen, Wunschterminoptionen.AuswaertsKoppelZweitzeit))
            .Select(w => new Terminwunschmarke(Tag(w.Datum), Terminoptionen.IstKoppel(w.Optionen) ? Terminwunschart.Koppel : Terminwunschart.Wunsch))
            .ToList();
        marken.AddRange(m.Sperrtermine.Select(d => new Terminwunschmarke(Tag(d), Terminwunschart.Sperrtermin)));
        return marken;
    }

    private static List<Terminwunschtext> Zeilen(RefPlan plan, RefMannschaft m)
    {
        var zeilen = new List<Terminwunschtext>();
        var zaehler = new Terminzaehler();
        foreach (RefWunschtermin w in m.Wunschtermine.Where(w => !Terminoptionen.Hat(w.Optionen, Wunschterminoptionen.AuswaertsKoppelZweitzeit)))
        {
            zeilen.Add(Wunschzeile(plan, m, w, zaehler));
        }

        List<double> sperr = m.Sperrtermine.Where(plan.IstInZuPlanenderRunde).ToList();
        if (sperr.Count > 0)
        {
            zeilen.Add(Kopf($"Sperrtermine ({sperr.Count} Stück):"));
            zeilen.AddRange(sperr.Select(d => Eintrag(DelphiDatum.ZuDateTime(d).ToString("ddd dd.MM.yyyy", Deutsch))));
        }

        if (m.Auswaertskoppeln.Count > 0)
        {
            zeilen.Add(Kopf("Auswärtskoppelwünsche:"));
            zeilen.AddRange(m.Auswaertskoppeln.Where(k => k.Art != AuswaertsKoppelTyp.Keiner).Select(k => Eintrag(k.TeamA + " und " + k.TeamB + " " + k.Art switch
            {
                AuswaertsKoppelTyp.GleicherTag => "am gleichen Tag",
                AuswaertsKoppelTyp.AndererTag => "mit Übernachtung",
                _ => "mit optionaler Übernachtung",
            })));
        }

        if (m.KeinWochenspiel.Count > 0)
        {
            zeilen.Add(Kopf("Spiele gegen diese Mannschaften nur am Wochenende (60km Regel):"));
            zeilen.AddRange(m.KeinWochenspiel.Select(Eintrag));
        }

        Heimrecht(plan, m, zeilen);
        Auswertung(plan, m, zaehler, zeilen);
        return zeilen;
    }

    private static Terminwunschtext Kopf(string text) => new(text, Terminwunschfarbe.Normal, true);

    private static Terminwunschtext Eintrag(string text) => new(text, Terminwunschfarbe.Normal, false);

    private static Terminwunschtext Wunschzeile(RefPlan plan, RefMannschaft m, RefWunschtermin w, Terminzaehler zaehler)
    {
        string text = DelphiDatum.ZuDateTime(w.Datum).ToString("ddd dd.MM.yyyy HH:mm", Deutsch);
        Terminwunschfarbe farbe = Terminwunschfarbe.Normal;
        bool vorrunde = plan.Optionen.Rundenplanung == Rundenplanung.Halbrunde || plan.RueckrundenbeginnXml > w.Datum;
        zaehler.Termin(vorrunde);

        if (Terminoptionen.IstKoppel(w.Optionen))
        {
            text += ", " + KoppelName(w.Optionen);
        }

        if (w.ParalleleSpiele > 0)
        {
            text += ", maximale Heimspiele: " + w.ParalleleSpiele.ToString(CultureInfo.InvariantCulture);
        }

        if (m.IstAusweichtermin(w.Datum))
        {
            text += ", Ausweichtermin";
        }

        if (w.Spiellokal.Length > 0)
        {
            text += ", Spiellokal: " + w.Spiellokal;
        }

        bool spielfrei = plan.IstSpielfreierTag(w.Datum);
        List<string> belegtVon = HallenbelegungDurchNachbarn(m, w.Datum, w.Spiellokal);
        if (belegtVon.Count > 0 && w.ParalleleSpiele > 0 && w.ParalleleSpiele <= belegtVon.Count)
        {
            text += ", Halle ist belegt von: " + string.Join(", ", belegtVon);
            farbe = Terminwunschfarbe.Gelb;
            if (!spielfrei)
            {
                zaehler.Belegt(vorrunde);
            }
        }

        var parallel = new List<string>();
        ParalleleNachbarspiele(plan, m, w.Datum, m.TeamNummer, Suchrichtung.Beide, parallel);
        if (parallel.Count > 0)
        {
            text += ", parallele Spiele (" + parallel.Count.ToString(CultureInfo.InvariantCulture) + "): " + string.Join(", ", parallel);
            zaehler.Parallel(vorrunde);
        }

        if (spielfrei)
        {
            text += ", spielfreier Tag";
            farbe = Terminwunschfarbe.Rot;
            zaehler.Spielfrei(vorrunde);
        }

        return new Terminwunschtext(text, farbe, false);
    }

    /// <summary>Original <c>GetHallenBelegung(…, IgnoreThisPlan = true)</c>: nur Heimspiele der Nachbarmannschaften.</summary>
    private static List<string> HallenbelegungDurchNachbarn(RefMannschaft m, double datum, string lokal)
    {
        double fenster = DelphiDatum.EncodeTime(1, 29);
        return m.Nachbarspiele.TryGetValue(DelphiDatum.Trunc(datum), out List<RefNachbarspiel>? liste)
            ? liste.Where(s => s.IstHeimspiel && Math.Abs(s.Datum - datum) <= fenster && s.Lokal == lokal).Select(s => s.Heim + " (" + s.Geschlecht + ")").ToList()
            : [];
    }

    /// <summary>Original <c>GetParallelGamesIntern(…, IgnoreThisPlan = true)</c>: nur Spiele der Nachbarmannschaften.</summary>
    private static void ParalleleNachbarspiele(RefPlan plan, RefMannschaft m, double datum, int teamNummer, Suchrichtung richtung, List<string> texte)
    {
        if (!m.Nachbarspiele.TryGetValue(DelphiDatum.Trunc(datum), out List<RefNachbarspiel>? liste))
        {
            return;
        }

        double fenster = DelphiDatum.EncodeTime(3, 59);
        foreach (RefNachbarspiel spiel in liste)
        {
            int diff = teamNummer - spiel.Nummer;
            bool pruefen = richtung == Suchrichtung.Beide
                ? spiel.KeineParallelenSpiele
                : spiel.Geschlecht == plan.Geschlecht && ((diff == -1 && richtung == Suchrichtung.Tiefer) || (diff == 1 && richtung == Suchrichtung.Hoeher));
            if (!pruefen || Math.Abs(spiel.Datum - datum) > fenster)
            {
                continue;
            }

            texte.Add(spiel.Heim + " - " + spiel.Gast);
            if (richtung is Suchrichtung.Beide or Suchrichtung.Tiefer)
            {
                ParalleleNachbarspiele(plan, m, datum, spiel.Nummer, Suchrichtung.Tiefer, texte);
            }

            if (richtung is Suchrichtung.Beide or Suchrichtung.Hoeher)
            {
                ParalleleNachbarspiele(plan, m, datum, spiel.Nummer, Suchrichtung.Hoeher, texte);
            }
        }
    }

    private static string KoppelName(Wunschterminoptionen o)
    {
        // Original KoppelValueFromOptions: die letzte zutreffende Prüfung gewinnt.
        string name = "kein Koppeltermin/Doppelspieltag";
        (Wunschterminoptionen Flag, string Name)[] reihenfolge =
        [
            (Wunschterminoptionen.KoppelHart, "Zwei Heimspiele an diesem Tag gewünscht (hohe Prio)"),
            (Wunschterminoptionen.KoppelMoeglich, "Zwei Heimspiele an diesem Tag möglich"),
            (Wunschterminoptionen.KoppelWeich, "Zwei Heimspiele an diesem Tag gewünscht"),
            (Wunschterminoptionen.DoppelWeich, "Doppelspieltag gewünscht"),
            (Wunschterminoptionen.DoppelHart, "Doppelspieltag gewünscht (hohe Prio)"),
            (Wunschterminoptionen.DoppelMoeglich, "Doppelspieltag möglich"),
        ];
        foreach (string text in reihenfolge.Where(r => Terminoptionen.Hat(o, r.Flag)).Select(r => r.Name))
        {
            name = text;
        }

        return name;
    }

    private static void Heimrecht(RefPlan plan, RefMannschaft m, List<Terminwunschtext> zeilen)
    {
        if (m.HeimspieleRundeEins < 0 && m.Heimrechte.Count == 0)
        {
            return;
        }

        zeilen.Add(Kopf("Heimrecht:"));
        if (m.HeimspieleRundeEins >= 0)
        {
            int rueck = plan.Mannschaften.Count - m.HeimspieleRundeEins - 1;
            zeilen.Add(Eintrag(string.Create(CultureInfo.InvariantCulture, $"Vorrunde: {m.HeimspieleRundeEins} Rückrunde: {rueck}")));
        }

        foreach ((string team, HeimrechtWert wert) in m.Heimrechte.Where(h => h.Wert != HeimrechtWert.Keins))
        {
            string runde = wert == HeimrechtWert.Runde1 ? "Vorrunde" : "Rückrunde";
            zeilen.Add(Eintrag($"Heimspiel gegen {team} in der {runde}"));
        }
    }

    private static void Auswertung(RefPlan plan, RefMannschaft m, Terminzaehler z, List<Terminwunschtext> zeilen)
    {
        zeilen.Add(Kopf("Auswertung:"));
        Berechnungsoptionen o = plan.Optionen;
        double minimum = (plan.Mannschaften.Count - 1) * 1.0 / 2;
        if (o.Doppelrunde)
        {
            minimum *= 2;
        }

        minimum = Math.Truncate(minimum + 0.75) + 2;
        string titel = o.Rundenplanung == Rundenplanung.Halbrunde ? "Terminwünsche: " : "Terminwünsche Vorrunde: ";
        zeilen.Add(Rundenzeile(titel, z.Vorrunde, minimum));

        if (o.Rundenplanung is not (Rundenplanung.Halbrunde or Rundenplanung.NurVorrunde or Rundenplanung.Corona))
        {
            zeilen.Add(Rundenzeile("Terminwünsche Rückrunde: ", z.Rueckrunde, minimum));
            zeilen.AddRange(NichtMoeglich(plan, m).Select(t => new Terminwunschtext(t, Terminwunschfarbe.Rot, false)));
        }
    }

    private static Terminwunschtext Rundenzeile(string titel, (int Termine, int Belegt, int Spielfrei, int Parallel) r, double minimum)
    {
        int effektiv = r.Termine - r.Belegt - r.Spielfrei;
        string text = titel + r.Termine.ToString(CultureInfo.InvariantCulture);
        if (r.Belegt > 0)
        {
            text += ", davon belegt: " + r.Belegt.ToString(CultureInfo.InvariantCulture);
        }

        if (r.Spielfrei > 0)
        {
            text += ", davon an spielfreien Tagen: " + r.Spielfrei.ToString(CultureInfo.InvariantCulture);
        }

        if (r.Parallel > 0)
        {
            text += ", davon mit parallelen Spielen: " + r.Parallel.ToString(CultureInfo.InvariantCulture);
        }

        text += ", effektiv: " + effektiv.ToString(CultureInfo.InvariantCulture);
        return new Terminwunschtext(text, effektiv < minimum ? Terminwunschfarbe.Rot : Terminwunschfarbe.Gruen, false);
    }

    /// <summary>Original <c>getTermineNotPossible</c>.</summary>
    private static List<string> NichtMoeglich(RefPlan plan, RefMannschaft m)
    {
        var meldungen = new List<string>();
        List<RefMannschaft> andereMitWuenschen = plan.Mannschaften.Where(a => !ReferenceEquals(a, m) && a.Wunschtermine.Count > 0).ToList();
        meldungen.AddRange(andereMitWuenschen
            .Where(andere => andere.Wunschtermine.TrueForAll(w => m.Sperrtermine.Contains(DelphiDatum.Trunc(w.Datum))))
            .Select(andere => "Alle Termine beim " + andere.TeamName + " wurden gesperrt"));
        meldungen.AddRange(andereMitWuenschen
            .Where(andere => andere.Wunschtermine.TrueForAll(w => !plan.IstWochenendeFuer60Km(w.Datum) && m.KeinWochenspiel.Contains(andere.TeamName)))
            .Select(andere => "60km Regel kann beim " + andere.TeamName + " nicht eingehalten werden"));

        if (plan.Mannschaften.Count % 2 == 0 && m.Wunschtermine.Count > 0 && m.Wunschtermine.TrueForAll(w => Terminoptionen.IstKoppelAmTag(w.Optionen)))
        {
            meldungen.Add("Es wurden nur Koppeltermine angegeben, die Anzahl der Heimspiele ist ungerade weshalb mindestens ein Koppeltermin nicht eingehalten werden kann.");
        }

        foreach (RefAuswaertskoppel koppel in m.Auswaertskoppeln)
        {
            RefMannschaft? a = koppel.MannschaftA(plan);
            RefMannschaft? b = koppel.MannschaftB(plan);
            if (a is null || b is null)
            {
                if (a is null)
                {
                    meldungen.Add("Datenfehler: Unbekanntes Team " + koppel.TeamA + " in einem Auswärtskoppelwunsch.");
                }

                if (b is null)
                {
                    meldungen.Add("Datenfehler: Unbekanntes Team " + koppel.TeamB + " in einem Auswärtskoppelwunsch.");
                }

                continue;
            }

            bool moeglich = a.Wunschtermine.Exists(w1 => b.Wunschtermine.Exists(w2 => m.IstGueltigesAuswaertsKoppelDatum(w1.Datum, w2.Datum, a.TeamName, b.TeamName)));
            if (!moeglich)
            {
                meldungen.Add("Für den Auswärtskoppelwunsch bei " + koppel.TeamA + " und " + koppel.TeamB + " existieren keine geeigneten Wunschtermine.");
            }
        }

        return meldungen;
    }

    /// <summary>Zähler je Runde wie im Original (Termine, belegt, spielfrei, parallel).</summary>
    private sealed class Terminzaehler
    {
        private readonly int[] vor = new int[4];
        private readonly int[] rueck = new int[4];

        public (int Termine, int Belegt, int Spielfrei, int Parallel) Vorrunde => (vor[0], vor[1], vor[2], vor[3]);

        public (int Termine, int Belegt, int Spielfrei, int Parallel) Rueckrunde => (rueck[0], rueck[1], rueck[2], rueck[3]);

        public void Termin(bool vorrunde) => Zaehlen(vorrunde, 0);

        public void Belegt(bool vorrunde) => Zaehlen(vorrunde, 1);

        public void Spielfrei(bool vorrunde) => Zaehlen(vorrunde, 2);

        public void Parallel(bool vorrunde) => Zaehlen(vorrunde, 3);

        private void Zaehlen(bool vorrunde, int index) => (vorrunde ? vor : rueck)[index]++;
    }
}
