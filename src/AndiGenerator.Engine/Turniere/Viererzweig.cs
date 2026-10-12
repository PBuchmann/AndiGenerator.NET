// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Stand eines Zweigs der vollständigen Suche (je Thread einer): gewählte Spieltage, gedeckte Paarungen, Gastgeber, Gäste.</summary>
internal sealed class Viererzweig
{
    private readonly int n;

    /// <summary>Initialisiert einen leeren Zweig.</summary>
    public Viererzweig(int n)
    {
        this.n = n;
        Gast = new int[n];
        Besucht = new bool[n * n];
    }

    /// <summary>Holt die gedeckten Paarungen.</summary>
    public ulong Gedeckt { get; private set; }

    /// <summary>Holt die Mannschaften, die schon einen 4er-Spieltag ausrichten.</summary>
    public int Ausrichter { get; private set; }

    /// <summary>Holt die Mannschaften mit Einzelbegegnung.</summary>
    public int Einzel { get; private set; }

    /// <summary>Holt die Kilometer der gewählten Spieltage.</summary>
    public double Km { get; private set; }

    /// <summary>Holt je Mannschaft, wie oft sie als Gast zu einem 4er-Spieltag fährt.</summary>
    public int[] Gast { get; }

    /// <summary>Holt je Mannschaft und Gastgeber, ob sie schon dorthin fährt (Index Gast · n + Gastgeber).</summary>
    public bool[] Besucht { get; }

    /// <summary>Holt die gewählten Spieltage.</summary>
    public List<Viererblock> Bloecke { get; } = [];

    /// <summary>Holt oder setzt die Zahl der in diesem Zweig geprüften Teilpläne seit der letzten Meldung.</summary>
    public long Knoten { get; set; }

    /// <summary>Eine unabhängige Kopie (für einen weiteren Thread).</summary>
    public Viererzweig Kopie()
    {
        var kopie = new Viererzweig(n);
        foreach (Viererblock b in Bloecke)
        {
            kopie.Setzen(b, 1);
        }

        return kopie;
    }

    /// <summary>Nimmt einen Spieltag auf (schritt 1) oder wieder heraus (schritt -1, nur den zuletzt aufgenommenen).</summary>
    public void Setzen(Viererblock b, int schritt)
    {
        bool an = schritt > 0;
        Gedeckt = an ? Gedeckt | b.Paare : Gedeckt & ~b.Paare;
        Km += schritt * b.Km;
        if (b.IstEinzel)
        {
            Einzel = an ? Einzel | b.TeamMaske : Einzel & ~b.TeamMaske;
        }
        else
        {
            int bit = 1 << b.Ausrichter;
            Ausrichter = an ? Ausrichter | bit : Ausrichter & ~bit;
            for (int i = 1; i < b.Mannschaften.Length; i++)
            {
                int g = b.Mannschaften[i];
                Gast[g] += schritt;
                Besucht[(g * n) + b.Ausrichter] = an;
            }
        }

        if (an)
        {
            Bloecke.Add(b);
        }
        else
        {
            Bloecke.RemoveAt(Bloecke.Count - 1);
        }
    }
}
