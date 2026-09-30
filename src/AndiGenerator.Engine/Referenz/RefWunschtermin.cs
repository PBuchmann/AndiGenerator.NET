// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Original <c>TWunschTermin</c>.</summary>
internal sealed class RefWunschtermin
{
    public double Datum { get; set; }

    public int ParalleleSpiele { get; set; }

    public Wunschterminoptionen Optionen { get; set; }

    public double KoppelZweitzeit { get; set; }

    public RefSpiel? ZugeordnetesSpiel { get; set; }

    public string Spiellokal { get; set; } = string.Empty;

    public RefWunschtermin Kopie() => new()
    {
        Datum = Datum,
        ParalleleSpiele = ParalleleSpiele,
        KoppelZweitzeit = KoppelZweitzeit,
        Spiellokal = Spiellokal,
        Optionen = Optionen,
    };
}
