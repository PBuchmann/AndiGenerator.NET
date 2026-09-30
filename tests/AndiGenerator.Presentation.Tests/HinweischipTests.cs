// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation.Tests;

/// <summary>Hinweise zu einem Spiel als farbige Chips (Terminplanansicht).</summary>
public sealed class HinweischipTests
{
    [Fact]
    public void Hinweise_werden_zerlegt_und_eingeordnet()
    {
        IReadOnlyList<Hinweischip> chips = Hinweischip.Aus("Heimkoppelspiel/Doppelspieltag, Ausweichtermin, Spiellokal: Halle Nord, Eingang B, kein Wunschtermin");

        Assert.Equal(
            [
                new Hinweischip("Heimkoppelspiel/Doppelspieltag", Hinweisart.Info),
                new Hinweischip("Ausweichtermin", Hinweisart.Warnung),
                new Hinweischip("Spiellokal: Halle Nord, Eingang B", Hinweisart.Neutral),
                new Hinweischip("kein Wunschtermin", Hinweisart.Problem),
            ],
            chips);
        Assert.NotEmpty(chips[3].Erklaerung);
        Assert.Empty(chips[2].Erklaerung);
        Assert.Empty(Hinweischip.Aus(string.Empty));
    }
}
