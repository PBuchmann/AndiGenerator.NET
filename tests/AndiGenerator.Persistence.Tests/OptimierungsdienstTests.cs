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

    /// <summary>
    /// Obergrenze für das Warten auf die erste Meldung – nur gegen Hängen: Gewartet wird, bis sie kommt (meist nach einer
    /// Sekunde). Im vollen Testlauf unter Last dauerte es auch schon über 10 s.
    /// </summary>
    private static readonly TimeSpan Hoechstens = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Verbesserungen_werden_gemeldet_und_Stoppen_beendet()
    {
        Plansitzung sitzung = Sitzung();
        using var dienst = new Optimierungsdienst(ZweiKerne);
        var gemeldet = new TaskCompletionSource<Optimierungsstand>(TaskCreationOptions.RunContinuationsAsynchronously);
        dienst.Verbessert += (_, stand) => gemeldet.TrySetResult(stand);

        dienst.Starten(sitzung.Staffel, sitzung.Optionen);
        Optimierungsstand erster = await gemeldet.Task.WaitAsync(Hoechstens);

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
            ausgangsplan = (await gefunden.Task.WaitAsync(Hoechstens)).Spiele;
        }

        using var dienst = new Optimierungsdienst(ZweiKerne);
        var gemeldet = new TaskCompletionSource<Optimierungsstand>(TaskCreationOptions.RunContinuationsAsynchronously);
        dienst.Verbessert += (_, stand) => gemeldet.TrySetResult(stand);
        dienst.Pausiert = true;
        dienst.Starten(sitzung.Staffel, sitzung.Optionen, ausgangsplan);

        Optimierungsstand stand = await gemeldet.Task.WaitAsync(Hoechstens);
        Assert.True(stand.Kosten >= 0);
        Assert.Equal(ausgangsplan.Count, stand.Spiele.Count);
    }

    /// <summary>
    /// Nur Rückrunde mit leerem Plan: Die Vorrunde aus click-TT bleibt fest. Vorher begann die Suche ohne sie und setzte
    /// Paarungen in der Rückrunde in derselben Richtung an (die untere Schranke rechnete dagegen mit der Vorrunde).
    /// </summary>
    /// <returns>Der Testlauf.</returns>
    [Fact]
    public async Task Rueckrunde_mit_leerem_Plan_haelt_die_Vorrunde_fest()
    {
        Plansitzung sitzung = Sitzung();
        Berechnungsoptionen optionen = sitzung.Optionen with { Rundenplanung = Rundenplanung.NurRueckrunde };
        IReadOnlyList<Spiel> vorrunde = Engine.Referenz.Vorrunde.Festhalten(sitzung.Staffel, optionen, null);
        Assert.NotEmpty(vorrunde);
        using var dienst = new Optimierungsdienst(ZweiKerne);
        var gemeldet = new TaskCompletionSource<Optimierungsstand>(TaskCreationOptions.RunContinuationsAsynchronously);
        dienst.Verbessert += (_, stand) => gemeldet.TrySetResult(stand);

        dienst.Starten(sitzung.Staffel, optionen);
        Optimierungsstand stand = await gemeldet.Task.WaitAsync(Hoechstens);

        Assert.All(vorrunde, v => Assert.Contains(v, stand.Spiele));
        Assert.DoesNotContain(stand.Spiele, s => s.Zeitpunkt is not null && !vorrunde.Contains(s)
            && vorrunde.Any(v => v.Heim == s.Heim && v.Gast == s.Gast));
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

        // Warten, bis der beste Plan alle Spiele terminiert hat (unter Last dauert das länger als die üblichen 1,5 s).
        Optimierungsstand vorher = dienst.Stand();
        for (int i = 0; i < 100 && (i < 15 || vorher.Spiele.Any(s => s.Zeitpunkt is null)); i++)
        {
            await Task.Delay(100);
            vorher = dienst.Stand();
        }

        dienst.DatenAendern(sitzung.Staffel, sitzung.Optionen);
        Optimierungsstand nachher = dienst.Stand();

        // Die Rechnung geht vom besten Plan aus weiter: nicht schlechter, keine Termine verloren (zwischen den beiden Abfragen
        // kann die Suche weitergekommen sein).
        Assert.True(nachher.Kosten <= vorher.Kosten, $"{nachher.Kosten} > {vorher.Kosten}");
        Assert.True(nachher.Durchlaeufe >= vorher.Durchlaeufe);
        Assert.True(
            nachher.Spiele.Count(s => s.Zeitpunkt is not null) >= vorher.Spiele.Count(s => s.Zeitpunkt is not null),
            $"{nachher.Spiele.Count(s => s.Zeitpunkt is not null)} < {vorher.Spiele.Count(s => s.Zeitpunkt is not null)} terminierte Spiele");
    }

    private static Plansitzung Sitzung()
    {
        string pfad = Path.Combine(Testdaten.Referenz, "eingabe", "R2_4__Kreisklasse_Gruppe_A (2).xml");
        return Plansitzung.Oeffnen(pfad, Path.Combine(Path.GetTempPath(), "andigen-test-basis-unbenutzt"));
    }
}
