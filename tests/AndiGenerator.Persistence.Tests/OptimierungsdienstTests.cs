// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Persistence.Tests;

public class OptimierungsdienstTests
{
    private static readonly Optimierungseinstellungen ZweiKerne = Optimierungseinstellungen.Standard with { Kerne = 2 };

    [Fact]
    public async Task Verbesserungen_werden_gemeldet_und_Stoppen_beendet()
    {
        Plansitzung sitzung = Sitzung();
        using var dienst = new Optimierungsdienst(ZweiKerne);
        var gemeldet = new TaskCompletionSource<Optimierungsstand>(TaskCreationOptions.RunContinuationsAsynchronously);
        dienst.Verbessert += (_, stand) => gemeldet.TrySetResult(stand);

        dienst.Starten(sitzung.Staffel, sitzung.Optionen);
        Optimierungsstand erster = await gemeldet.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(erster.Laeuft);
        Assert.True(erster.Kosten >= 0);
        Assert.Equal(sitzung.Staffel.Mannschaften.Count * (sitzung.Staffel.Mannschaften.Count - 1), erster.Spiele.Count);
        Assert.True(dienst.Stand().Durchlaeufe > 0);
        Assert.Null(dienst.Fehler);

        dienst.Stoppen();
        Assert.False(dienst.Laeuft);
        Assert.Same(Optimierungsstand.Leer, dienst.Stand());
    }

    [Fact]
    public async Task Start_mit_Ausgangsplan_meldet_diesen_sofort()
    {
        Plansitzung sitzung = Sitzung();
        IReadOnlyList<Spiel> ausgangsplan;
        using (var erster = new Optimierungsdienst(ZweiKerne))
        {
            var gefunden = new TaskCompletionSource<Optimierungsstand>(TaskCreationOptions.RunContinuationsAsynchronously);
            erster.Verbessert += (_, stand) => gefunden.TrySetResult(stand);
            erster.Starten(sitzung.Staffel, sitzung.Optionen);
            ausgangsplan = (await gefunden.Task.WaitAsync(TimeSpan.FromSeconds(10))).Spiele;
        }

        using var dienst = new Optimierungsdienst(ZweiKerne);
        var gemeldet = new TaskCompletionSource<Optimierungsstand>(TaskCreationOptions.RunContinuationsAsynchronously);
        dienst.Verbessert += (_, stand) => gemeldet.TrySetResult(stand);
        dienst.Pausiert = true;
        dienst.Starten(sitzung.Staffel, sitzung.Optionen, ausgangsplan);

        Optimierungsstand stand = await gemeldet.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(stand.Kosten >= 0);
        Assert.Equal(ausgangsplan.Count, stand.Spiele.Count);
    }

    [Fact]
    public void Anhalten_stellt_den_vorherigen_Zustand_wieder_her()
    {
        Plansitzung sitzung = Sitzung();
        using var dienst = new Optimierungsdienst(ZweiKerne);
        dienst.Starten(sitzung.Staffel, sitzung.Optionen);

        using (dienst.Anhalten())
        {
            Assert.True(dienst.Pausiert);
            Assert.True(dienst.Stand().Pausiert);
        }

        Assert.False(dienst.Pausiert);
    }

    [Fact]
    public async Task Datenaenderung_rechnet_vom_besten_Plan_weiter()
    {
        Plansitzung sitzung = Sitzung();
        using var dienst = new Optimierungsdienst(ZweiKerne);
        dienst.Starten(sitzung.Staffel, sitzung.Optionen);
        await Task.Delay(1500);
        Optimierungsstand vorher = dienst.Stand();

        dienst.DatenAendern(sitzung.Staffel, sitzung.Optionen);
        Optimierungsstand nachher = dienst.Stand();

        Assert.True(nachher.Kosten <= vorher.Kosten, $"{nachher.Kosten} > {vorher.Kosten}");
        Assert.True(nachher.Durchlaeufe >= vorher.Durchlaeufe);
        Assert.Equal(
            vorher.Spiele.Count(s => s.Zeitpunkt is not null),
            nachher.Spiele.Count(s => s.Zeitpunkt is not null));
    }

    private static Plansitzung Sitzung()
    {
        string pfad = Path.Combine(Testdaten.Referenz, "eingabe", "R2_4__Kreisklasse_Gruppe_A (2).xml");
        return Plansitzung.Oeffnen(pfad, Path.Combine(Path.GetTempPath(), "andigen-test-basis-unbenutzt"));
    }
}
