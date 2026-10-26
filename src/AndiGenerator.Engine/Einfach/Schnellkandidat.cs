// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Engine.Einfach;

/// <summary>
/// Ein Kandidat im Rennen des einfachen Modus: die Grundeinteilung oder der Fokus auf eine Gruppe (ihre Kriterien stehen
/// zusätzlich in B), mit allen Plänen, die er gefunden hat. Welcher davon der beste ist, entscheidet der Maßstab des
/// Augenblicks – der Klassenleiter kann ihn ändern. Zusammen zeigen die Kandidaten, was das Verbessern einer Gruppe bei den
/// anderen kostet (später die „Wippe“ der Oberfläche).
/// </summary>
public sealed class Schnellkandidat
{
    private readonly Lock sperre = new();
    private readonly List<(IReadOnlyList<Spiel> Plan, Planguete Guete)> plaene = [];

    /// <summary>Initialisiert den Kandidaten.</summary>
    /// <param name="name">Kurzname, z. B. „Grundeinteilung“ oder „Fokus Heim/Auswärts“.</param>
    /// <param name="gruppe">Die Gruppe im Fokus (Index in <see cref="Massstab.Gruppen"/>), oder <c>null</c>.</param>
    /// <param name="einteilung">Die Einteilung.</param>
    /// <param name="plan">Sein Startplan.</param>
    /// <param name="guete">Dessen Verstöße.</param>
    /// <param name="strikt">
    /// Der Hebel des Klassenleiters: die Gruppe strikt verbessern (ihre Kriterien in B); sonst nur in ihre Richtung neigen
    /// (dreifaches Gewicht in der Güte), damit seine Pläne als Vorschläge taugen.
    /// </param>
    public Schnellkandidat(string name, int? gruppe, Stufeneinteilung einteilung, IReadOnlyList<Spiel> plan, Planguete guete, bool strikt = false)
    {
        Strikt = strikt;
        Name = name;
        Gruppe = gruppe;
        Einteilung = einteilung;
        Fortsetzung = plan;
        plaene.Add((plan, guete));
    }

    /// <summary>Gets den Kurznamen.</summary>
    public string Name { get; }

    /// <summary>Gets die Gruppe im Fokus, oder <c>null</c> (Grundeinteilung).</summary>
    public int? Gruppe { get; }

    /// <summary>Gets a value indicating whether er der Hebel des Klassenleiters ist (die Gruppe strikt verbessern).</summary>
    public bool Strikt { get; }

    /// <summary>Gets die Einteilung (die Reihenfolge in C ordnet der Schnellplaner je Runde nach den Gewichten).</summary>
    public Stufeneinteilung Einteilung { get; }

    /// <summary>
    /// Gets den Plan, mit dem die nächste Runde beginnt: der beste nach der eigenen Einteilung (er verfolgt seine Richtung, auch
    /// wenn die Güte dabei zunächst schlechter ist – erst die Gruppe im Fokus senken, dann den Rest glätten).
    /// </summary>
    public IReadOnlyList<Spiel> Fortsetzung { get; private set; }

    /// <summary>Gets die Zahl der gerechneten Runden.</summary>
    public int Runden { get; private set; }

    /// <summary>Gets die Zahl der gerechneten Runden in der aktuellen Stufe.</summary>
    public int RundenInStufe { get; private set; }

    /// <summary>Gets die gesamte Rechenzeit in Sekunden.</summary>
    public double Sekunden { get; private set; }

    /// <summary>Gets a value indicating whether der Kandidat in der letzten Runde gerechnet hat.</summary>
    public bool Aktiv { get; internal set; } = true;

    /// <summary>
    /// Gets die Erfolge: ein neuer bester Plan zählt 2, ein anderer Plan an der Grenze 0,5; ältere zählen mit jeder Runde
    /// weniger. Danach verteilt der Schnellplaner die Rechenzeit, wenn nicht alle Kandidaten gleichzeitig rechnen können.
    /// </summary>
    public double Erfolge { get; private set; }

    /// <summary>Gets seinen letzten Plan mit Verstößen.</summary>
    public (IReadOnlyList<Spiel> Plan, Planguete Guete) Letzter
    {
        get
        {
            lock (sperre)
            {
                return plaene[^1];
            }
        }
    }

    /// <summary>Sein bester Plan nach einem Maßstab.</summary>
    /// <param name="massstab">Der Maßstab.</param>
    /// <returns>Plan und Verstöße.</returns>
    public (IReadOnlyList<Spiel> Plan, Planguete Guete) Bester(Massstab massstab)
    {
        ArgumentNullException.ThrowIfNull(massstab);
        lock (sperre)
        {
            (IReadOnlyList<Spiel> Plan, Planguete Guete) bester = plaene[0];
            foreach ((IReadOnlyList<Spiel> Plan, Planguete Guete) p in plaene.Skip(1).Where(p => massstab.IstBesser(p.Guete, bester.Guete)))
            {
                bester = p;
            }

            return bester;
        }
    }

    /// <summary>Übernimmt das Ergebnis einer Runde.</summary>
    /// <param name="plan">Der beste Plan der Runde nach der eigenen Einteilung.</param>
    /// <param name="guete">Seine Verstöße.</param>
    /// <param name="sekunden">Die Dauer der Runde.</param>
    internal void Runde(IReadOnlyList<Spiel> plan, Planguete guete, double sekunden)
    {
        lock (sperre)
        {
            Runden++;
            RundenInStufe++;
            Sekunden += sekunden;
            Fortsetzung = plan;
            plaene.Add((plan, guete));
        }
    }

    /// <summary>Zählt einen Erfolg.</summary>
    /// <param name="wert">0,5 für einen Plan an der Grenze, 2 für einen neuen besten.</param>
    internal void Erfolg(double wert)
    {
        lock (sperre)
        {
            Erfolge += wert;
        }
    }

    /// <summary>Ältere Erfolge zählen weniger (nach jeder Runde).</summary>
    /// <param name="faktor">Der Faktor (z. B. 0,7).</param>
    internal void Vergessen(double faktor)
    {
        lock (sperre)
        {
            Erfolge *= faktor;
        }
    }

    /// <summary>Neue Stufe: Er beginnt beim gewählten Plan, das Bisherige zählt nicht mehr.</summary>
    /// <param name="plan">Der gewählte Plan.</param>
    /// <param name="guete">Seine Verstöße.</param>
    internal void Neubeginnen(IReadOnlyList<Spiel> plan, Planguete guete)
    {
        lock (sperre)
        {
            plaene.Clear();
            plaene.Add((plan, guete));
            Fortsetzung = plan;
            Erfolge = 0;
            RundenInStufe = 0;
        }
    }
}
