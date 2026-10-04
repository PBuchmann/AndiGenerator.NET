// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Kern;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Inseln;

/// <summary>
/// Eine Insel (Original <c>TPlanMainThread</c>): feste und dynamische Suchplätze mit gemeinsamer bester Lösung.
/// Beim Abgleich wird die beste Lösung aller Plätze übernommen und bei einer Verbesserung an alle Plätze verteilt;
/// die dynamischen Plätze wechseln zur zuletzt erfolgreichen Strategie. Wird nur vom Koordinator aufgerufen.
/// </summary>
internal sealed class Insel
{
    private readonly List<Suchplatz> dynamische;
    private readonly Loesung kandidat;
    private long durchlaeufe;
    private long verbesserungBei;
    private long neustartBei;

    public Insel(int nummer, Suchplatz[] plaetze, Loesung start, double startKosten)
    {
        Nummer = nummer;
        Plaetze = plaetze;
        dynamische = plaetze.Where(p => p.Dynamisch).ToList();
        Loesung = start.Kopie();
        Kosten = startKosten;
        kandidat = new Loesung(start.Datum.Length);
    }

    public int Nummer { get; }

    public Suchplatz[] Plaetze { get; }

    /// <summary>Beste Lösung der Insel (Original <c>TPlanMainThread.Plan</c>).</summary>
    public Loesung Loesung { get; }

    /// <summary>Ihre Kosten; −1 direkt nach einem Neustart (Original <c>RetKosten</c>).</summary>
    public double Kosten { get; private set; }

    /// <summary>Durchläufe aller Plätze der Insel seit dem Start.</summary>
    public long Durchlaeufe => durchlaeufe;

    /// <summary>Durchläufe seit der letzten Verbesserung oder dem letzten Neustart.</summary>
    public long OhneVerbesserung => durchlaeufe - verbesserungBei;

    /// <summary>Durchläufe seit dem letzten Neustart (für die Spezial-Insel: seit dem letzten Wechsel der Kostenart).</summary>
    public long SeitNeustart => durchlaeufe - neustartBei;

    /// <summary>Holt einen Zähler, der sich bei jeder neuen Lösung der Insel (Verbesserung oder Neustart) erhöht (Automodus).</summary>
    public int Stand { get; private set; }

    /// <summary>Anzahl der Neustarts dieser Insel.</summary>
    public int Neustarts { get; private set; }

    /// <summary>Original <c>TPlanMainThread.Execute</c>, ein Poll: Ernte, Verteilung, Anpassung der dynamischen Plätze.</summary>
    public void Abgleichen()
    {
        long summe = 0;
        foreach (Suchplatz platz in Plaetze)
        {
            summe += platz.Durchlaeufe;
        }

        durchlaeufe = summe;
        Tauschstrategie? besteStrategie = null;
        foreach (Suchplatz platz in Plaetze)
        {
            double kosten = platz.ErgebnisHolen(kandidat, Kosten);
            if (kosten >= 0)
            {
                kandidat.KopierenNach(Loesung);
                Kosten = kosten;
                Stand++;
                besteStrategie = platz.Strategie;
            }
        }

        if (besteStrategie is null)
        {
            return;
        }

        verbesserungBei = durchlaeufe;
        Verteilen(new Auftrag(Loesung.Kopie(), Kosten));
        DynamischeAnpassen(besteStrategie);
    }

    /// <summary>
    /// Neustart mit einer Ausgangslösung (Original <c>ReInitAll</c> mit leerer Lösung bzw. <c>ReInit</c> mit der Lösung der
    /// Spezial-Insel). <paramref name="kosten"/> = −1, wenn unbekannt.
    /// </summary>
    public void Neustarten(Loesung start, double kosten)
    {
        Zuruecksetzen(start, kosten);
        Verteilen(new Auftrag(start.Kopie(), kosten));
    }

    /// <summary>Neustart mit neuen Plänen (andere Gewichtungen, Spezial-Insel); jeder Platz bekommt einen eigenen Plan.</summary>
    public void NeuAufsetzen(Loesung start, double kosten, Func<KernPlan> neuerPlan)
    {
        Zuruecksetzen(start, kosten);
        foreach (Suchplatz platz in Plaetze)
        {
            platz.Auftragen(new Auftrag(start.Kopie(), kosten, neuerPlan()));
        }
    }

    private void Zuruecksetzen(Loesung start, double kosten)
    {
        start.KopierenNach(Loesung);
        Kosten = kosten;
        Stand++;
        verbesserungBei = durchlaeufe;
        neustartBei = durchlaeufe;
        Neustarts++;
    }

    private void Verteilen(Auftrag auftrag)
    {
        foreach (Suchplatz platz in Plaetze)
        {
            platz.Auftragen(auftrag);
        }
    }

    /// <summary>
    /// Dynamische Strategie-Anpassung. Korrigiert gegenüber dem Original (Befund #9, <c>DynamicThreads[0]</c> statt <c>[i]</c>):
    /// Es wird wirklich gezählt, wie viele dynamische Plätze die erfolgreiche Strategie schon haben; nur unter einem Drittel wird
    /// der am längsten nicht umgestellte Platz umgestellt.
    /// </summary>
    private void DynamischeAnpassen(Tauschstrategie erfolgreich)
    {
        if (dynamische.Count == 0)
        {
            return;
        }

        int gleich = dynamische.Count(p => p.Strategie == erfolgreich);
        if (gleich >= dynamische.Count / 3)
        {
            return;
        }

        Suchplatz aeltester = dynamische[0];
        aeltester.Strategie = erfolgreich;
        dynamische.RemoveAt(0);
        dynamische.Add(aeltester);
    }
}
