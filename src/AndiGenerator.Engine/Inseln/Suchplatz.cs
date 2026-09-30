// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Kern;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Inseln;

/// <summary>
/// Ein Suchplatz entspricht einem Worker des Originals (<c>TPlanCalcThread</c>): eigener Plan, eigene Zufallsfolge, eine
/// Tauschstrategie. Anders als im Original ist er kein eigener Thread; die Rechen-Threads arbeiten ihre Suchplätze reihum ab.
/// Den Plan fasst nur der ausführende Thread an; Austausch mit der Insel über <see cref="Auftragen"/> und <see cref="ErgebnisHolen"/>.
/// </summary>
internal sealed class Suchplatz
{
    /// <summary>Iterationen seit dem Neustart, ab denen Raster- und 100-%-Plätze gedrosselt werden (Original 10 000).</summary>
    private const int DrosselnAb = 10_000;

    /// <summary>Gedrosselte Plätze rechnen nur bei jedem 50. Aufruf (Original: 50 ms Pause je Iteration).</summary>
    private const int DrosselTakt = 50;

    private readonly Lock sperre = new();
    private readonly Zufall zufall;
    private readonly Loesung ergebnis;
    private KernPlan plan;
    private Auftrag? auftrag;
    private Tauschstrategie strategie;
    private double ergebnisKosten;
    private double beste;
    private long durchlaeufe;
    private long seitNeustart;
    private int uebersprungen;
    private bool aktiv;

    /// <summary>Legt den Platz an; ein inaktiver Platz (Spezial-Insel vor ihrem Start) rechnet erst nach einem Auftrag mit neuem Plan.</summary>
    public Suchplatz(KernPlan plan, Zufall zufall, Tauschstrategie strategie, bool dynamisch, double startKosten, bool aktiv = true)
    {
        this.aktiv = aktiv;
        this.plan = plan;
        this.zufall = zufall;
        this.strategie = strategie;
        Dynamisch = dynamisch;
        beste = startKosten;
        ergebnisKosten = startKosten;
        ergebnis = new Loesung(plan.AnzahlErlaubt);
        plan.LoesungSchreiben(ergebnis);
    }

    /// <summary>Gehört zu den dynamischen Plätzen, deren Strategie die Insel anpasst.</summary>
    public bool Dynamisch { get; }

    /// <summary>Aktuelle Tauschstrategie (von der Insel änderbar).</summary>
    public Tauschstrategie Strategie
    {
        get => Volatile.Read(ref strategie);
        set => Volatile.Write(ref strategie, value);
    }

    /// <summary>Bisher gerechnete Durchläufe (kumulativ).</summary>
    public long Durchlaeufe => Volatile.Read(ref durchlaeufe);

    /// <summary>Stellt einen Neustart-Auftrag zu; ein noch nicht ausgeführter wird ersetzt.</summary>
    public void Auftragen(Auftrag neu) => Volatile.Write(ref auftrag, neu);

    /// <summary>Holt die beste Lösung dieses Platzes, falls sie besser als <paramref name="schwelle"/> ist (−1 = jede).</summary>
    /// <returns>Ihre Kosten, sonst −1.</returns>
    public double ErgebnisHolen(Loesung ziel, double schwelle)
    {
        lock (sperre)
        {
            if (ergebnisKosten < 0 || (schwelle >= 0 && ergebnisKosten >= schwelle))
            {
                return -1;
            }

            ergebnis.KopierenNach(ziel);
            return ergebnisKosten;
        }
    }

    /// <summary>Ein Durchlauf (Original <c>TPlanCalcThread.Execute</c>, eine Iteration).</summary>
    /// <returns><c>false</c>, wenn der Platz gedrosselt ist und diesmal nicht gerechnet hat.</returns>
    public bool Rechnen()
    {
        Auftrag? neu = Interlocked.Exchange(ref auftrag, null);
        if (neu is not null)
        {
            Uebernehmen(neu);
        }

        if (!aktiv)
        {
            return false;
        }

        Tauschstrategie aktuell = Strategie;
        if (seitNeustart >= DrosselnAb && IstGedrosselt(aktuell))
        {
            uebersprungen++;
            if (uebersprungen < DrosselTakt)
            {
                return false;
            }
        }

        uebersprungen = 0;
        plan.Zuruecksetzen();
        plan.VorgegebeneSpieleSetzen();
        plan.NeuWuerfeln(aktuell, zufall);
        plan.TermineFuellen(zufall);
        double kosten = plan.Kosten();
        if (kosten >= 0 && (kosten < beste || beste < 0))
        {
            plan.Merken();
            beste = kosten;
            lock (sperre)
            {
                plan.LoesungSchreiben(ergebnis);
                ergebnisKosten = kosten;
            }
        }

        seitNeustart++;
        Volatile.Write(ref durchlaeufe, durchlaeufe + 1);
        return true;
    }

    /// <summary>Original: Worker mit Raster oder ab 30 % neu gewürfelter Spiele laufen mit niedriger Priorität.</summary>
    private static bool IstGedrosselt(Tauschstrategie s) => s.Typ == Tauschtyp.Raster || (s.Typ == Tauschtyp.Normal && s.Prozent >= 30);

    private void Uebernehmen(Auftrag neu)
    {
        if (neu.NeuerPlan is not null)
        {
            plan = neu.NeuerPlan;
            aktiv = true;
        }

        plan.LoesungUebernehmen(neu.Loesung);
        beste = neu.Kosten;
        lock (sperre)
        {
            neu.Loesung.KopierenNach(ergebnis);
            ergebnisKosten = neu.Kosten;
        }

        seitNeustart = 0;
        uebersprungen = 0;
    }
}
