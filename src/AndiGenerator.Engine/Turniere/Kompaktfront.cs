// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Die besten Pläne aller Suchen (threadsicher): zuerst zählen wenige Mannschaften ohne Heimspieltag, wenige
/// Verstöße und wenige 3er-Spieltage (geht es nur mit 4er-Spieltagen, dann so); unter den Plänen mit den wenigsten bleiben die, die kein anderer in allen Kennzahlen übertrifft
/// (Kilometer, Stand, gemeinsamer Spieltag, Verteilung der Heimspiele).
/// </summary>
internal sealed class Kompaktfront
{
    private const int Hoechstzahl = 400;

    private readonly Lock sperre = new();
    private readonly List<Kompaktloesung> loesungen = [];

    /// <summary>Holt einen Wert, der angibt, ob noch kein Plan gefunden wurde.</summary>
    public bool Leer
    {
        get
        {
            lock (sperre)
            {
                return loesungen.Count == 0;
            }
        }
    }

    /// <summary>Nimmt einen Plan auf, wenn ihn keiner übertrifft.</summary>
    /// <returns><c>true</c>, wenn der Plan aufgenommen wurde.</returns>
    public bool Hinzufuegen(Kompaktloesung neu)
    {
        lock (sperre)
        {
            if (loesungen.Count > 0)
            {
                int vergleich = Grundwertung(neu).CompareTo(Grundwertung(loesungen[0]));
                if (vergleich > 0)
                {
                    return false;
                }

                if (vergleich < 0)
                {
                    loesungen.Clear();
                }
            }

            if (loesungen.Count >= Hoechstzahl || loesungen.Exists(l => Uebertrifft(l, neu) || l.Schluessel == neu.Schluessel))
            {
                return false;
            }

            loesungen.RemoveAll(l => Uebertrifft(neu, l));
            loesungen.Add(neu);
            return true;
        }
    }

    /// <summary>Alle Pläne.</summary>
    public List<Kompaktloesung> Alle()
    {
        lock (sperre)
        {
            return loesungen.ToList();
        }
    }

    private static (int OhneHeimspieltag, int Verstoesse, int Dreier) Grundwertung(Kompaktloesung l) => (l.OhneHeimspieltag, l.Verstoesse, l.Dreier);

    /// <summary>Ob a in keiner Kennzahl schlechter und in mindestens einer besser ist als b.</summary>
    private static bool Uebertrifft(Kompaktloesung a, Kompaktloesung b)
    {
        bool nichtSchlechter = a.Km <= b.Km && a.Abstand <= b.Abstand && a.Gemeinsam >= b.Gemeinsam && a.Heimspiele <= b.Heimspiele;
        bool besser = a.Km < b.Km || a.Abstand < b.Abstand || a.Gemeinsam > b.Gemeinsam || a.Heimspiele < b.Heimspiele;
        return nichtSchlechter && besser;
    }
}
