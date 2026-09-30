// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Kostenkachel: prozentuale Verbesserung seit dem ersten gültigen Plan bzw. seit der letzten Kostenanpassung.</summary>
public sealed class GenerierungsanzeigeTests
{
    [Fact]
    public void Verbesserung_bezieht_sich_nach_einer_Kostenanpassung_auf_den_ersten_Plan_danach()
    {
        var anzeige = new Generierungsanzeige();

        anzeige.Uebernehmen(Stand(30_000_000_000));
        Assert.Equal("Plan hat noch harte Fehler", anzeige.KostenHinweis);

        anzeige.Uebernehmen(Stand(1000));
        Assert.Equal("seit dem ersten gültigen Plan unverändert", anzeige.KostenHinweis);

        anzeige.Uebernehmen(Stand(500));
        Assert.Equal("−50 % seit dem ersten gültigen Plan", anzeige.KostenHinweis);

        anzeige.KostenAngepasst();
        anzeige.Uebernehmen(Stand(2000));
        Assert.Equal("seit der letzten Kostenanpassung unverändert", anzeige.KostenHinweis);

        anzeige.Uebernehmen(Stand(1500));
        Assert.Equal("−25 % seit der letzten Kostenanpassung", anzeige.KostenHinweis);
    }

    private static Optimierungsstand Stand(double kosten) =>
        new(true, false, 100, 10, kosten, 1, TimeSpan.Zero, null, []);
}
