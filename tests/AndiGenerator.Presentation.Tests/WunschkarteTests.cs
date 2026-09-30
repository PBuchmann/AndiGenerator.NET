// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Terminwünsche je Mannschaft als Karten: Abschnitte, Status und Reihenfolge.</summary>
public sealed class WunschkarteTests
{
    [Fact]
    public void Karten_gruppieren_nach_Ueberschriften_und_zeigen_Probleme_zuerst()
    {
        Terminwunschtext[] zeilenA =
        [
            new("Heimspieltermine:", Terminwunschfarbe.Normal, true),
            new("Sa 19.09.2026 18:00", Terminwunschfarbe.Normal, false),
            new("12 Termine, 11 benötigt", Terminwunschfarbe.Gruen, false),
        ];
        Terminwunschtext[] zeilenB =
        [
            new("vorab", Terminwunschfarbe.Normal, false),
            new("Heimspieltermine:", Terminwunschfarbe.Normal, true),
            new("Sa 26.09.2026 spielfreier Tag", Terminwunschfarbe.Rot, false),
        ];
        var gut = new MannschaftsTerminwuensche("TTC A", [], zeilenA);
        var schlecht = new MannschaftsTerminwuensche("SV B", [], zeilenB);
        var uebersicht = new Terminwunschuebersicht(null, null, false, [], [gut, schlecht]);

        IReadOnlyList<Wunschkarte> karten = Wunschkarte.Bilden(uebersicht);

        Assert.Equal(["SV B", "TTC A"], karten.Select(k => k.Name));
        Assert.Equal(("Probleme", Terminwunschfarbe.Rot), (karten[0].Status, karten[0].StatusFarbe));
        Assert.Equal(("genügend Termine", Terminwunschfarbe.Gruen), (karten[1].Status, karten[1].StatusFarbe));
        Assert.Equal([string.Empty, "Heimspieltermine"], karten[0].Abschnitte.Select(a => a.Titel));
        Assert.False(karten[0].Abschnitte[0].HatTitel);
        Assert.True(karten[0].Abschnitte[1].Eintraege[0].Hervorgehoben);
        Assert.Equal(2, karten[1].Abschnitte[0].Eintraege.Count);
        Assert.Empty(Wunschkarte.Bilden(null));
    }
}
