// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Referenz;

/// <summary>
/// Daten des Tabs „Diagramme“ im Original (<c>PaintVerteilung</c>): Spieltage mit Überlappungen, Wechsel Heim/Auswärts,
/// Spielverteilung, Abstand Heimspiel zu Auswärtsspiel, Anzahl Spiele pro Woche und Setzliste. Gezeichnet wird in der
/// Oberfläche; hier stehen die Berechnungen des Originals.
/// </summary>
public static class Diagrammauswertung
{
    /// <summary>Eine Sekunde als Delphi-<c>TDateTime</c> (<c>EncodeTime(0, 0, 1, 0)</c>).</summary>
    private const double Sekunde = 1.0 / 86400.0;

    /// <summary>Ermittelt die Diagrammdaten des bestehenden Spielplans der Staffel.</summary>
    /// <param name="staffel">Staffel mit dem Plan als bestehendem Spielplan.</param>
    /// <param name="optionen">Berechnungsoptionen.</param>
    /// <returns>Die Diagrammdaten.</returns>
    public static Diagrammdaten Ermitteln(Staffel staffel, Berechnungsoptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        RefPlan plan = RefPlan.Laden(staffel, optionen);
        List<double> wuensche = plan.Mannschaften.SelectMany(m => m.Wunschtermine).Select(w => w.Datum).Where(d => d != 0.0).ToList();
        double erster = wuensche.Count == 0 ? 0.0 : wuensche.Min();
        double letzter = wuensche.Count == 0 ? 0.0 : wuensche.Max();
        bool mitRueckrunde = optionen.Rundenplanung is not (Rundenplanung.Halbrunde or Rundenplanung.NurVorrunde or Rundenplanung.Corona);
        return new Diagrammdaten(
            plan.SpieleErlaubt.Exists(s => s.Datum != 0.0),
            DelphiDatum.ZuDateTime(erster),
            DelphiDatum.ZuDateTime(letzter),
            plan.Mannschaften.Select(m => Mannschaft(plan, m)).ToList(),
            Ueberlappungen(plan),
            plan.HatKoppeltermine() || plan.HatAuswaertsKoppeltermine(),
            mitRueckrunde,
            Wochen(plan, optionen, mitRueckrunde),
            plan.HatSetzliste(),
            plan.Runden.Count,
            plan.Mannschaften.Count == 0 ? 0 : plan.Mannschaften.Max(m => m.Spiele.Count));
    }

    /// <summary>Original <c>MondayBefore</c>.</summary>
    /// <param name="datum">Delphi-Datum.</param>
    /// <returns>Montag der Woche als Delphi-Datum.</returns>
    internal static double MontagDavor(double datum) => (((DelphiDatum.Trunc(datum) - 2) / 7) * 7) + 2;

    /// <summary>Original <c>PaintSpielAbstandMannschaft</c>: Farbe der Linie zwischen zwei Spielen gegen denselben Gegner.</summary>
    /// <param name="abstand">Anzahl Spiele vom ersten bis zum nächsten Spiel gegen den Gegner.</param>
    /// <param name="ideal">Idealer Abstand (Spiele der Mannschaft je Runde).</param>
    /// <returns>Rot- und Grünanteil.</returns>
    internal static (int Rot, int Gruen) Abstandsfarbe(int abstand, double ideal)
    {
        double wert = Math.Abs((abstand - ideal) / ideal);
        if (abstand > ideal)
        {
            // Spiele weiter auseinander als gefordert ist nicht so schlimm.
            wert /= 1.25;
        }

        int gruen = Math.Max(0, 255 - (int)Math.Truncate(wert * 255));
        int rot = 255 - gruen;
        return rot < gruen ? (rot, 255) : (255, gruen);
    }

    private static Diagrammmannschaft Mannschaft(RefPlan plan, RefMannschaft m)
    {
        int eigenerRang = plan.Setzlistenplaetze.IndexOf(m.Index);
        var spiele = new List<Diagrammspiel>();
        for (int i = 0; i < m.Spiele.Count; i++)
        {
            RefSpiel spiel = m.Spiele[i];
            int gegnerRang = plan.Setzlistenplaetze.IndexOf(Gegner(m, spiel).Index);
            spiele.Add(new Diagrammspiel(
                spiel.DatumGueltig ? DelphiDatum.ZuDateTime(spiel.Datum) : null,
                ReferenceEquals(spiel.Heim, m),
                m.GekoppeltesSpiel(i) is not null,
                spiel.DatumGueltig ? plan.RundeZuDatum(spiel.Datum) : -1,
                gegnerRang < 0 || eigenerRang < 0 ? 0 : Math.Abs(eigenerRang - gegnerRang)));
        }

        return new Diagrammmannschaft(m.TeamName, spiele, Abstaende(plan, m));
    }

    private static RefMannschaft Gegner(RefMannschaft m, RefSpiel spiel) => ReferenceEquals(spiel.Heim, m) ? spiel.Gast : spiel.Heim;

    /// <summary>Original <c>PaintSpielAbstandMannschaft</c>: je Gegner die Kette der Spiele gegen ihn.</summary>
    private static List<IReadOnlyList<Abstandslinie>> Abstaende(RefPlan plan, RefMannschaft m)
    {
        List<RefSpiel> spiele = m.Spiele;
        double ideal = (double)spiele.Count / Math.Max(1, plan.Runden.Count);
        var erledigt = new HashSet<string>(StringComparer.Ordinal);
        var zeilen = new List<IReadOnlyList<Abstandslinie>>();
        for (int index = 0; index < spiele.Count; index++)
        {
            string gegner = Gegner(m, spiele[index]).TeamId;
            if (erledigt.Contains(gegner) || !spiele[index].DatumGueltig)
            {
                continue;
            }

            erledigt.Add(gegner);
            var linien = new List<Abstandslinie>();
            int k = index;
            int j = spiele.FindIndex(k + 1, s => Gegner(m, s).TeamId == gegner);
            while (j >= 0)
            {
                (int rot, int gruen) = Abstandsfarbe(j - k, ideal);
                linien.Add(new Abstandslinie(DelphiDatum.ZuDateTime(spiele[k].Datum), DelphiDatum.ZuDateTime(spiele[j].Datum), rot, gruen));
                k = j;
                j = spiele.FindIndex(k + 1, s => Gegner(m, s).TeamId == gegner);
            }

            zeilen.Add(linien);
        }

        return zeilen;
    }

    /// <summary>Original <c>PaintSpielPlanOverlap</c>.</summary>
    private static List<Spieltagsueberlappung> Ueberlappungen(RefPlan plan)
    {
        int spieltage = plan.Mannschaften.Count == 0 ? 0 : plan.Mannschaften.Max(m => m.Spiele.Count);
        var maxWerte = new List<double>();
        var minWerte = new List<double>();
        for (int i = 0; i < spieltage; i++)
        {
            List<double> termine = plan.Mannschaften
                .Where(m => m.Spiele.Count > i && m.Spiele[i].Datum != 0.0)
                .Select(m => m.Spiele[i].Datum)
                .ToList();
            if (termine.Count > 0)
            {
                maxWerte.Add(termine.Max());
                minWerte.Add(termine.Min());
            }
        }

        var ergebnis = new List<Spieltagsueberlappung>();
        for (int abstand = 1; abstand < maxWerte.Count; abstand++)
        {
            for (int i = 0; i + abstand < minWerte.Count; i++)
            {
                int j = i + abstand;
                if (DelphiDatum.Trunc(maxWerte[i]) >= DelphiDatum.Trunc(minWerte[j]))
                {
                    int helligkeit = 255 - Math.Min(128, abstand * (128 / 5));
                    ergebnis.Add(new Spieltagsueberlappung(DelphiDatum.ZuDateTime(minWerte[j]), DelphiDatum.ZuDateTime(maxWerte[i]), helligkeit));
                }
            }
        }

        return ergebnis;
    }

    /// <summary>Original <c>PaintGamesPerWeek</c>.</summary>
    private static List<Wochenstatistik> Wochen(RefPlan plan, Berechnungsoptionen optionen, bool mitRueckrunde)
    {
        double bis = (optionen.Rundenplanung == Rundenplanung.Halbrunde ? plan.Ende : plan.RueckrundenbeginnXml) - Sekunde;
        var ergebnis = new List<Wochenstatistik> { Woche(plan, "Hinrunde", Montage(plan, 0.0, bis, bis)) };
        if (mitRueckrunde)
        {
            double ende = DelphiDatum.Wert(new DateOnly(2500, 1, 1));
            ergebnis.Add(Woche(plan, "Rückrunde", Montage(plan, plan.RueckrundenbeginnXml, ende, ende)));
        }

        return ergebnis;
    }

    /// <summary>Original <c>getSpieltagMondays</c> (nur Wunschtermine) plus Endwert.</summary>
    private static List<double> Montage(RefPlan plan, double von, double bis, double endwert)
    {
        List<double> montage = plan.Mannschaften
            .SelectMany(m => m.Wunschtermine)
            .Where(w => w.Datum >= von && w.Datum <= bis)
            .Select(w => MontagDavor(w.Datum))
            .Distinct()
            .Order()
            .ToList();
        montage.Add(endwert);
        return montage;
    }

    /// <summary>Original <c>PaintGamesPerWeekOneRound</c>.</summary>
    private static Wochenstatistik Woche(RefPlan plan, string titel, List<double> montage)
    {
        int wochen = montage.Count - 1;
        var spiele = plan.Mannschaften
            .Select(m => (IReadOnlyList<int>)Enumerable.Range(0, wochen).Select(j => Spielanzahl(m, montage[j], montage[j + 1] - Sekunde)).ToList())
            .ToList();
        var differenzen = new List<int>();
        for (int j = 0; j < wochen; j++)
        {
            List<int> bisher = plan.Mannschaften.Select(m => Spielanzahl(m, montage[0], montage[j + 1] - Sekunde)).ToList();
            differenzen.Add(bisher.Count == 0 ? 0 : bisher.Max() - bisher.Min());
        }

        List<DateOnly> tage = montage.Take(wochen).Select(d => DateOnly.FromDateTime(DelphiDatum.ZuDateTime(d))).ToList();
        return new Wochenstatistik(titel, tage, spiele, differenzen);
    }

    /// <summary>Original <c>TMannschaft.getGameCount</c>.</summary>
    private static int Spielanzahl(RefMannschaft m, double von, double bis) =>
        m.Spiele.Count(s => s.DatumGueltig && s.Datum >= von && s.Datum <= bis);
}
