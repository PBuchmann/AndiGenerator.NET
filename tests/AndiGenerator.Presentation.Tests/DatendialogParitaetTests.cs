// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Presentation.Tests;

/// <summary>
/// Die 14 im Original gemachten Bearbeitungen (Referenz/datendialoge, ANLEITUNG.md), diesmal über die ViewModels
/// ausgeführt wie von einem Anwender: Seite wählen, Werte setzen, Dialoge über die Attrappe mit OK schließen, OK im
/// Datendialog. Die geschriebene <c>.modifications</c> muss inhaltlich der des Originals entsprechen.
/// </summary>
public class DatendialogParitaetTests
{
    [Fact]
    public Task D01_Allgemein() => VergleichenAsync("D01_Allgemein", a =>
    {
        LigadatenSeiteViewModel seite = a.Seite<LigadatenSeiteViewModel>();
        seite.Name = "4. Kreisklasse Gruppe A Test";
        seite.Ende = new DateTime(2027, 5, 16, 0, 0, 0, DateTimeKind.Unspecified);
        return Task.CompletedTask;
    });

    [Fact]
    public Task D02_Mannschaft_aendern() => VergleichenAsync("D02_Mannschaft_aendern", async a =>
    {
        MannschaftenSeiteViewModel seite = a.Seite<MannschaftenSeiteViewModel>();
        a.Oberflaeche.Mannschaft = d =>
        {
            d.Name = Namen.WsvWorms1;
            d.Spiellokal = "2";
            return d.Bestaetigen();
        };
        seite.Auswahl = Namen.WsvWorms;
        await seite.BearbeitenCommand.ExecuteAsync(null);
        Assert.Contains(Namen.WsvWorms1, seite.Namen);
    });

    [Fact]
    public Task D03_Mannschaft_neu_bis_auf_Befund_21() => VergleichenAsync(
        "D03_Mannschaft_neu",
        async a =>
        {
            MannschaftenSeiteViewModel seite = a.Seite<MannschaftenSeiteViewModel>();
            a.Oberflaeche.Mannschaft = d =>
            {
                d.Name = Namen.TtcTest;
                d.Id = "1234";
                d.VereinsId = "12345";
                d.Nummer = 1;
                return d.Bestaetigen();
            };
            await seite.NeuCommand.ExecuteAsync(null);
        },
        erwartet => erwartet.KinderMitNamen("team").Single(t => t.Lesen("teamname") == Namen.TtcTest).Setzen("homerights", -1));

    [Fact]
    public Task D04_Setzliste() => VergleichenAsync("D04_Setzliste", a =>
    {
        SetzlistenSeiteViewModel seite = a.Seite<SetzlistenSeiteViewModel>();
        seite.Aktiv = true;
        seite.Auswahl = seite.Reihenfolge.IndexOf(Namen.WsvWorms);
        while (seite.HochCommand.CanExecute(null))
        {
            seite.HochCommand.Execute(null);
        }

        Assert.Equal(Namen.WsvWorms, seite.Reihenfolge[0]);
        return Task.CompletedTask;
    });

    [Fact]
    public Task D05_Spiellokale() => VergleichenAsync("D05_Spiellokale", a =>
    {
        var detail = Arbeitsplatz.Detail<SpiellokalDetailViewModel>(a.Seite<SpiellokaleSeiteViewModel>(), Namen.RheinduerkheimIV);
        detail.Standardlokal = "2";
        detail.Nachbarn.Single(n => n.Beschriftung == Namen.RheinduerkheimDamen).Spiellokal = "3";
        return Task.CompletedTask;
    });

    [Fact]
    public Task D06_Heimkoppeln() => VergleichenAsync("D06_Heimkoppeln", a =>
    {
        var detail = Arbeitsplatz.Detail<HeimkoppelDetailViewModel>(a.Seite<HeimkoppelnSeiteViewModel>(), Namen.HanauFc);
        var tag = new DateOnly(2015, 10, 10);
        HeimkoppelZeile zeile = detail.Zeilen.Single(z => z.Wert.Tag1 == tag || z.Wert.Tag2 == tag);
        Heimkoppel ziel = zeile.Wert with
        {
            Option1 = zeile.Wert.Tag1 == tag ? AufMoeglich(zeile.Wert.Option1) : zeile.Wert.Option1,
            Option2 = zeile.Wert.Tag2 == tag ? AufMoeglich(zeile.Wert.Option2) : zeile.Wert.Option2,
        };
        zeile.Index = ziel.Index;
        return Task.CompletedTask;
    });

    [Fact]
    public Task D07_Auswaertskoppeln() => VergleichenAsync("D07_Auswaertskoppeln", async a =>
    {
        var detail = Arbeitsplatz.Detail<AuswaertskoppelDetailViewModel>(a.Seite<AuswaertskoppelnSeiteViewModel>(), Namen.DornDuerkheimIV);
        a.Oberflaeche.Auswaertskoppel = d =>
        {
            d.MannschaftA = Namen.RheinduerkheimIV;
            d.MannschaftB = Namen.WsvWorms;
            d.Art = 0;
            return d.Bestaetigen();
        };
        await detail.NeuCommand.ExecuteAsync(null);
        Assert.Single(detail.Koppeln);
    });

    [Fact]
    public Task D08_Wunschtermine() => VergleichenAsync("D08_Wunschtermine", a =>
    {
        var detail = Arbeitsplatz.Detail<WunschterminDetailViewModel>(a.Seite<WunschtermineSeiteViewModel>(), Namen.RheinduerkheimIV);
        WunschterminZelle zelle = detail.Wochen.SelectMany(w => w.Tage).Single(z => z.Tag == new DateOnly(2026, 9, 9));
        zelle.Text = zelle.Text.Replace("20:15", "20:00", StringComparison.Ordinal);
        return Task.CompletedTask;
    });

    [Fact]
    public Task D09_Nachbarmannschaften() => VergleichenAsync("D09_Nachbarmannschaften", async a =>
    {
        var detail = Arbeitsplatz.Detail<NachbarmannschaftDetailViewModel>(a.Seite<NachbarmannschaftenSeiteViewModel>(), Namen.RheinduerkheimIV);
        a.Oberflaeche.Nachbarspiel = d =>
        {
            d.Datum = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Unspecified);
            d.Uhrzeit = new TimeSpan(20, 0, 0);
            d.Art = 0;
            d.Gegner = "X";
            return d.Bestaetigen();
        };
        a.Oberflaeche.Nachbarmannschaft = async d =>
        {
            d.Name = Namen.TtvTest;
            d.Nummer = 1;
            d.Art = "Herren";
            await d.NeuCommand.ExecuteAsync(null);
            Assert.Single(d.Spiele);
            return d.Bestaetigen();
        };
        await detail.NeuCommand.ExecuteAsync(null);
    });

    [Fact]
    public Task D10_60km() => VergleichenAsync("D10_60km", a =>
    {
        var detail = Arbeitsplatz.Detail<WochenendeDetailViewModel>(a.Seite<WochenendeSeiteViewModel>(), Namen.RheinduerkheimIV);
        detail.Gegner.Single(g => g.Name == Namen.WsvWorms).Gewaehlt = true;
        return Task.CompletedTask;
    });

    [Fact]
    public Task D11_Spielfreie_Tage() => VergleichenAsync("D11_Spielfreie_Tage", a =>
    {
        SpielfreieTageSeiteViewModel seite = a.Seite<SpielfreieTageSeiteViewModel>();
        string[] ersteTage = ["Mo 17.08.2026", "Di 18.08.2026"];
        Assert.Equal(ersteTage, seite.Zeilen.Take(2).Select(z => z.Name));
        seite.Zeilen[0].Gewaehlt = true;
        seite.Zeilen[1].Gewaehlt = true;
        return Task.CompletedTask;
    });

    [Fact]
    public Task D12_Pflichtspieltage() => VergleichenAsync("D12_Pflichtspieltage", async a =>
    {
        PflichtspieltageSeiteViewModel seite = a.Seite<PflichtspieltageSeiteViewModel>();
        seite.Zeilen.Single().Anzahl = 2;
        a.Oberflaeche.Pflichtspielzeit = d =>
        {
            d.Von = new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Unspecified);
            d.Bis = new DateTime(2027, 3, 7, 0, 0, 0, DateTimeKind.Unspecified);
            d.Anzahl = 1;
            return d.Bestaetigen();
        };
        await seite.NeuCommand.ExecuteAsync(null);
        Assert.Equal(2, seite.Zeilen.Count);
    });

    [Fact]
    public Task D13_Begegnungen() => VergleichenAsync("D13_Begegnungen", async a =>
    {
        VorgabespieleSeiteViewModel seite = a.Seite<VorgabespieleSeiteViewModel>();
        a.Oberflaeche.Vorgabespiel = d =>
        {
            d.Datum = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Unspecified);
            d.Uhrzeit = new TimeSpan(20, 0, 0);
            d.Heim = Namen.WsvWorms;
            d.Gast = Namen.HohenSuelzenIV;
            return d.Bestaetigen();
        };
        await seite.NeuCommand.ExecuteAsync(null);
        Assert.Single(seite.Zeilen);
    });

    [Fact]
    public Task D14_Heimrecht() => VergleichenAsync("D14_Heimrecht", a =>
    {
        var detail = Arbeitsplatz.Detail<HeimrechtDetailViewModel>(a.Seite<HeimrechtSeiteViewModel>(), Namen.WsvWorms);
        detail.AnzahlIndex = detail.Anzahlen.ToList().FindIndex(t => t.StartsWith("Vorrunde: 3 ", StringComparison.Ordinal));
        detail.Gegner.Single(g => g.Gegner == Namen.HohenSuelzenIV).Index = 0;
        return Task.CompletedTask;
    });

    private static Koppelwunsch AufMoeglich(Koppelwunsch wunsch) => wunsch switch
    {
        Koppelwunsch.Gewuenscht or Koppelwunsch.Hoch => Koppelwunsch.Moeglich,
        Koppelwunsch.DoppelGewuenscht or Koppelwunsch.DoppelHoch => Koppelwunsch.DoppelMoeglich,
        _ => wunsch,
    };

    private static async Task VergleichenAsync(string name, Func<Arbeitsplatz, Task> bearbeiten, Action<DatenKnoten>? bewussteAbweichung = null)
    {
        using var arbeitsplatz = new Arbeitsplatz("datendialoge/" + name + ".xml");
        await bearbeiten(arbeitsplatz);
        arbeitsplatz.Uebernehmen();

        DatenKnoten erwartet = PlanDatenDatei.Laden(Path.Combine(Testdaten.Referenz, "datendialoge", name + ".modifications"));
        bewussteAbweichung?.Invoke(erwartet);
        Assert.Equal(erwartet.Kanonisch(), arbeitsplatz.Aenderungen.Kanonisch());
    }
}
