// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Engine.Einfach;

/// <summary>
/// Die Steuerung des einfachen Modus von außen (Oberfläche, Kommandozeile), auch während er rechnet: Welche Gruppe soll besser
/// werden (Fokus), wie wichtig ist jede Gruppe (Gewicht)? Der Schnellplaner liest den <see cref="Massstab"/> vor jeder Runde
/// und bei jedem Vergleich neu. Wer mitbestimmt, wählt einen der Vorschläge und einen Hebel (<see cref="Waehlen"/>): Dann
/// beginnt die nächste <see cref="Stufe"/> – gesucht wird nur noch nahe am gewählten Plan, je Stufe enger. Ohne Wahl läuft
/// der einfache Modus allein zum ausgewogenen Optimum. Threadsicher.
/// </summary>
public sealed class Schnellsteuerung
{
    private readonly Lock sperre = new();
    private Massstab massstab = Massstab.Standard;
    private int version;
    private int stufe = 1;
    private (Grenzplan Plan, int? Hebel)? wahl;

    /// <summary>Gets den aktuellen Maßstab.</summary>
    public Massstab Massstab
    {
        get
        {
            lock (sperre)
            {
                return massstab;
            }
        }
    }

    /// <summary>Gets die Zahl der Änderungen (der Schnellplaner erkennt daran neue Wünsche des Klassenleiters).</summary>
    public int Version
    {
        get
        {
            lock (sperre)
            {
                return version;
            }
        }
    }

    /// <summary>Gets die Stufe: 1 bis zur ersten Wahl, mit jeder Wahl eine mehr.</summary>
    public int Stufe
    {
        get
        {
            lock (sperre)
            {
                return stufe;
            }
        }
    }

    /// <summary>Gets a value indicating whether eine Wahl noch nicht abgeholt ist (die laufende Runde endet dann vorzeitig).</summary>
    internal bool WahlOffen
    {
        get
        {
            lock (sperre)
            {
                return wahl is not null;
            }
        }
    }

    /// <summary>
    /// Der Klassenleiter wählt einen Plan (meist einen der Vorschläge) und optional einen Hebel: Von diesem Plan aus geht es
    /// weiter, nur noch in seiner Nähe (die übrigen Gruppen dürfen höchstens um die Toleranz der Stufe schlechter werden); mit
    /// Hebel wird dessen Gruppe strikt verbessert, ohne Hebel wird ausgewogen verfeinert.
    /// </summary>
    /// <param name="plan">Der gewählte Plan.</param>
    /// <param name="hebel">Die Gruppe, die besser werden soll (Index in <see cref="Massstab.Gruppen"/>), oder <c>null</c>.</param>
    public void Waehlen(Grenzplan plan, int? hebel)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (hebel is int h)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(h);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(h, Massstab.Gruppen.Count);
        }

        lock (sperre)
        {
            wahl = (plan, hebel);
            massstab = massstab with { Fokus = hebel };
            stufe++;
            version++;
        }
    }

    /// <summary>Eine Gruppe soll besser werden – strikt, auch wenn andere dafür schlechter werden; <c>null</c> hebt das auf.</summary>
    /// <param name="gruppe">Index in <see cref="Massstab.Gruppen"/>, oder <c>null</c>.</param>
    public void Fokus(int? gruppe)
    {
        if (gruppe is int g)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(g);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(g, Massstab.Gruppen.Count);
        }

        lock (sperre)
        {
            massstab = massstab with { Fokus = gruppe };
            version++;
        }
    }

    /// <summary>Wie wichtig eine Gruppe in der Güte ist (Standard nach Auffälligkeit: Heim/Auswärts 3, Abstände 2, sonst 1).</summary>
    /// <param name="gruppe">Index in <see cref="Massstab.Gruppen"/>.</param>
    /// <param name="gewicht">Das Gewicht (≥ 0).</param>
    public void Gewicht(int gruppe, double gewicht)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gewicht);
        lock (sperre)
        {
            massstab = massstab.MitGewicht(gruppe, gewicht);
            version++;
        }
    }

    /// <summary>
    /// Wie wichtig ein einzelnes Kriterium ist (Faktor auf sein Gewicht): kleiner – es darf steigen, wenn dafür andere besser
    /// werden; größer – es wird möglichst gut optimiert; 1 – normal (hebt beides wieder auf).
    /// </summary>
    /// <param name="kriterium">Das Kriterium.</param>
    /// <param name="faktor">Der Faktor (≥ 0).</param>
    public void Wichtigkeit(Kostenkriterium kriterium, double faktor)
    {
        lock (sperre)
        {
            massstab = massstab.MitFaktor(kriterium, faktor);
            version++;
        }
    }

    /// <summary>Der Bezugsplan der Güte (der Grundplan nach A und B); setzt der Schnellplaner.</summary>
    /// <param name="bezug">Verstöße je Kriterium.</param>
    internal void BezugSetzen(IReadOnlyList<int> bezug)
    {
        lock (sperre)
        {
            massstab = massstab with { Bezug = bezug };
        }
    }

    /// <summary>Holt die letzte Wahl ab (einmal; danach <c>null</c> bis zur nächsten).</summary>
    /// <returns>Plan und Hebel, oder <c>null</c>.</returns>
    internal (Grenzplan Plan, int? Hebel)? WahlAbholen()
    {
        lock (sperre)
        {
            (Grenzplan Plan, int? Hebel)? w = wahl;
            wahl = null;
            return w;
        }
    }
}
