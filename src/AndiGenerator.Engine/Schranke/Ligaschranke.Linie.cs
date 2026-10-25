// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Kern;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Engine.Schranke;

/// <summary>Eine HiGHS-Linie: ein langer Lauf für A und B zusammen gleich nach A1.</summary>
public sealed partial class Ligaschranke
{
    /// <summary>So viel wiegt ein A-Verstoß im Ziel einer Linie gegenüber einem B-Verstoß.</summary>
    private const double LinieGewichtA = 100;

    /// <summary>
    /// Eine HiGHS-Linie (Verfahren „mehrfach“ im Vergleich mit HiGHS): ein langer Lauf gleich nach A1, ohne erst A zu beweisen,
    /// Ziel 100 · A + B; jede bessere Lösung geht sofort hinaus. Mehrere Linien mit verschiedenen Startwerten finden Pläne in
    /// verschiedenen Gegenden, die die Teilneuplanung dann verfeinert (Verbandsoberliga, 5 Minuten: B 24 statt 33 bis 37 mit
    /// einem oder vielen Läufen allein, schwerer B 34 statt 52 bis 66).
    /// </summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="optionen">Die Optionen.</param>
    /// <param name="loeser">Der Löser (mit eigenem Startwert je Linie).</param>
    /// <param name="einteilung">Die Einteilung der Kriterien.</param>
    /// <param name="startplan">Ein Plan als Startlösung (wenn er besser ist als der aus A1), oder <c>null</c>.</param>
    /// <param name="plan">Bekommt jeden besseren Plan.</param>
    /// <param name="dauer">Zeitgrenze des Laufs.</param>
    /// <param name="aufhoeren">Bricht ab (auch mitten im Lauf).</param>
    /// <returns>
    /// <c>true</c>, wenn nichts mehr zu holen ist: HiGHS hat den Plan als bestmöglich bewiesen (kleine Staffeln) oder die Staffel
    /// lässt sich nicht als Modell fassen. Ein weiterer Lauf lohnt dann nicht.
    /// </returns>
    public static bool Linie(Staffel staffel, Berechnungsoptionen optionen, ISchrankenloeser loeser, Stufeneinteilung einteilung, Func<IReadOnlyList<Spiel>?> startplan, Action<IReadOnlyList<Spiel>> plan, TimeSpan dauer, Func<bool> aufhoeren)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        ArgumentNullException.ThrowIfNull(loeser);
        ArgumentNullException.ThrowIfNull(einteilung);
        ArgumentNullException.ThrowIfNull(startplan);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(aufhoeren);
        RefPlan referenz = RefPlan.Laden(optionen.Rundenplanung == Rundenplanung.NurRueckrunde ? staffel : staffel with { BestehenderSpielplan = [] }, optionen);
        var d = new KernDefinition(referenz);
        return Grund(d) is not null || new Ligaschranke(d).LinieRechnen(loeser, einteilung.Vervollstaendigt(), startplan, plan, dauer, aufhoeren);
    }

    private bool LinieRechnen(ISchrankenloeser loeser, Stufeneinteilung einteilung, Func<IReadOnlyList<Spiel>?> startplan, Action<IReadOnlyList<Spiel>> plan, TimeSpan dauer, Func<bool> aufhoeren)
    {
        List<(int Spalte, double Faktor)> a1 = ohneTermin.Select(u => (u.Spalte, 1.0)).ToList();
        (_, int? ohne, double[]? planA1, _) = Minimieren(modell, a1, loeser, dauer, aufhoeren: aufhoeren);
        if (ohne is not int h || planA1 is null || aufhoeren())
        {
            return false;
        }

        modell.Zeile(a1, double.NegativeInfinity, h);
        List<(int Spalte, double Faktor)> ziel = Zusammenfassen(Stufensumme(einteilung.B).Concat(Stufensumme(einteilung.A).Select(e => (e.Spalte, e.Faktor * LinieGewichtA))));
        double[]? start = new[] { Plan(planA1), startplan() }
            .OfType<IReadOnlyList<Spiel>>()
            .Where(p => p.Count > 0)
            .Select(p => Einbetten(modell, p, ziel, loeser))
            .OfType<double[]>()
            .MinBy(w => Wert(ziel, w));
        void Melden(double[]? werte)
        {
            if (werte is not null)
            {
                plan(Plan(werte));
            }
        }

        (int schranke, int? gefunden, double[]? ende, _) = Minimieren(modell, ziel, loeser, dauer, start, aufhoeren, zwischen: Melden);
        Melden(ende);
        return gefunden is int g && schranke >= g && !aufhoeren();
    }

    /// <summary>Die Summe der Verstöße einer Stufe im Modell (Kriterien, die das Modell nicht kennt, fehlen).</summary>
    private List<(int Spalte, double Faktor)> Stufensumme(IEnumerable<Kostenkriterium> kriterien) =>
        Zusammenfassen(kriterien.Select(k => Ziel(k)).OfType<List<(int Spalte, double Faktor)>>().SelectMany(z => z));

    /// <summary>Ein Plan als Lösung des Modells (Ansetzungen fest, Hilfsspalten von HiGHS ergänzt); <c>null</c>, wenn er nicht passt.</summary>
    private double[]? Einbetten(Ganzzahlmodell basis, IReadOnlyList<Spiel> plan, List<(int Spalte, double Faktor)> ziel, ISchrankenloeser loeser)
    {
        double[] x = Abbilden(plan, out _);
        Ganzzahlmodell kopie = basis.Kopie();
        foreach (int spalte in termine.Select(t => t.Spalte).Concat(ohneTermin.Select(u => u.Spalte)))
        {
            kopie.Zeile([spalte], x[spalte], x[spalte]);
        }

        return Minimieren(kopie, ziel, loeser, TimeSpan.FromSeconds(30)).Werte;
    }
}
