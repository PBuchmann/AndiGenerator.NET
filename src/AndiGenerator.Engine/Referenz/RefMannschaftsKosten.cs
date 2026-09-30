// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Engine.Referenz;

/// <summary>Original <c>TMannschaftsKosten</c>.</summary>
internal sealed class RefMannschaftsKosten
{
    public Kostenwert[] Werte { get; } = new Kostenwert[Berechnungsoptionen.AnzahlKostenarten];

    public List<string> Meldungen { get; } = [];

    /// <summary>Original <c>ClearValues</c>.</summary>
    public void Leeren(MannschaftsKostenart art)
    {
        Werte[(int)art].Anzahl = 0;
        Werte[(int)art].AllCount = -1;
        Werte[(int)art].GesamtKosten = 0;
    }

    /// <summary>Original <c>getGesamtKosten</c>.</summary>
    public double Gesamt()
    {
        double summe = 0.0;
        foreach (Kostenwert wert in Werte)
        {
            summe += wert.GesamtKosten;
        }

        return summe;
    }
}
