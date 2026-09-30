// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Bearbeitungsdialoge und die übrigen Funktionen der Detailseiten (Referenzfall R2 bzw. R26 ohne Änderungen).</summary>
public sealed class DialogeTests : IDisposable
{
    private readonly Arbeitsplatz a = new("datendialoge/D01_Allgemein.xml");

    public void Dispose() => a.Dispose();

    [Fact]
    public void Wunschtermindialog_liest_und_schreibt_alle_Angaben()
    {
        var h = new Heimtag(new DateTime(2026, 9, 12, 19, 30, 0, DateTimeKind.Unspecified), false, true, false, 2, false, new TimeOnly(14, 0), 3, true, "2");
        string text = Heimtagtext.Text(h);

        var dialog = new WunschterminDialogViewModel(new DateOnly(2026, 9, 12), text);

        Assert.Equal("Datum: 12.09.2026", dialog.Datum);
        Assert.Equal(1, dialog.Art);
        Assert.True(dialog.IstHeimspiel);
        Assert.Equal(new TimeSpan(19, 30, 0), dialog.Uhrzeit);
        Assert.Equal(3, dialog.MaxSpiele);
        Assert.True(dialog.Ausweich);
        Assert.Equal("2", dialog.Spiellokal);
        Assert.Equal(1, dialog.Koppel);
        Assert.Equal(2, dialog.Prio);
        Assert.True(dialog.PrioAktiv);
        Assert.True(dialog.KoppelzeitAktiv);
        Assert.False(dialog.AuswaertskoppelMoeglich);
        Assert.Equal(new TimeSpan(14, 0, 0), dialog.Koppelzeit);
        Assert.True(dialog.Bestaetigen());
        Assert.Equal(text, dialog.Ergebnis);
    }

    [Fact]
    public void Wunschtermindialog_ohne_Termin_Sperrtermin_und_Doppelspieltag()
    {
        var leer = new WunschterminDialogViewModel(new DateOnly(2026, 9, 12), string.Empty);
        Assert.Equal(0, leer.Art);
        Assert.True(leer.Bestaetigen());
        Assert.Equal(string.Empty, leer.Ergebnis);

        var frei = new WunschterminDialogViewModel(new DateOnly(2026, 9, 12), "FREI");
        Assert.Equal(2, frei.Art);
        Assert.True(frei.Bestaetigen());
        Assert.Equal("FREI", frei.Ergebnis);

        leer.Art = 1;
        leer.Uhrzeit = new TimeSpan(15, 0, 0);
        leer.Koppel = 2;
        leer.Prio = 1;
        Assert.True(leer.Bestaetigen());
        Heimtag gelesen = Heimtagtext.Lesen(new DateOnly(2026, 9, 12), leer.Ergebnis)!;
        Assert.True(gelesen.Doppeltermin);
        Assert.Equal(1, gelesen.KoppelPrio);
    }

    [Fact]
    public void Wunschtermindialog_prueft_Zeiten_und_Auswaertskoppel()
    {
        var dialog = new WunschterminDialogViewModel(new DateOnly(2026, 9, 12), string.Empty) { Art = 1 };
        Assert.False(dialog.Bestaetigen());
        Assert.Equal("Bitte geben Sie eine Zeit ein", dialog.Meldung);

        dialog.Uhrzeit = new TimeSpan(18, 0, 0);
        dialog.Auswaertskoppel = true;
        Assert.False(dialog.Bestaetigen());
        Assert.Equal("Bitte geben Sie eine zweite Zeit ein", dialog.Meldung);

        dialog.Auswaertszeit = new TimeSpan(15, 0, 0);
        Assert.False(dialog.Bestaetigen());
        Assert.Equal("Zwischen dem ersten und zweiten Spiel müssen mindestens 4 Stunden liegen", dialog.Meldung);

        dialog.Auswaertszeit = new TimeSpan(14, 0, 0);
        Assert.True(dialog.Bestaetigen());
        Assert.True(Heimtagtext.Lesen(new DateOnly(2026, 9, 12), dialog.Ergebnis)!.AuswaertsKoppelZweitzeit);

        dialog.Koppel = 1;
        Assert.False(dialog.Auswaertskoppel);
        Assert.False(dialog.Bestaetigen());
        dialog.Koppelzeit = new TimeSpan(13, 0, 0);
        Assert.True(dialog.Bestaetigen());
    }

    [Fact]
    public async Task Wunschtermine_per_Dialog_bearbeiten_loeschen_und_zuruecksetzen()
    {
        var detail = Arbeitsplatz.Detail<WunschterminDetailViewModel>(a.Seite<WunschtermineSeiteViewModel>(), Namen.RheinduerkheimIV);
        WunschterminZelle zelle = detail.Wochen.SelectMany(w => w.Tage).First(z => z.HatTermin && z.Text != "FREI");
        string vorher = zelle.Text;

        a.Oberflaeche.Wunschtermin = d =>
        {
            d.Uhrzeit = new TimeSpan(19, 0, 0);
            return d.Bestaetigen();
        };
        await detail.BearbeitenCommand.ExecuteAsync(zelle);
        Assert.StartsWith("19:00", zelle.Text, StringComparison.Ordinal);
        Assert.False(detail.IstStandard);

        Assert.True(detail.LoeschenCommand.CanExecute(zelle));
        detail.LoeschenCommand.Execute(zelle);
        Assert.False(zelle.HatTermin);
        Assert.Contains("Wunschtermin", a.Oberflaeche.Aufrufe);

        a.Dialog.StandardCommand.Execute(null);
        Assert.True(detail.IstStandard);
        Assert.Equal(vorher, detail.Wochen.SelectMany(w => w.Tage).Single(z => z.Tag == zelle.Tag).Text);
    }

    [Fact]
    public async Task Nachbarmannschaft_aus_den_Spiellokalen_bearbeiten()
    {
        var detail = Arbeitsplatz.Detail<SpiellokalDetailViewModel>(a.Seite<SpiellokaleSeiteViewModel>(), Namen.RheinduerkheimIV);
        SpiellokalZeile zeile = detail.Nachbarn.Single(n => n.Beschriftung == Namen.RheinduerkheimDamen);
        string? meldung = null;
        a.Oberflaeche.Nachbarspiel = d =>
        {
            d.Uhrzeit = new TimeSpan(18, 0, 0);
            return d.Bestaetigen();
        };
        a.Oberflaeche.Nachbarmannschaft = async d =>
        {
            int spiele = d.Spiele.Count;
            string name = d.Name;
            d.Name = string.Empty;
            Assert.False(d.Bestaetigen());
            meldung = d.Meldung;
            d.Name = name;
            d.Spiellokal = "4";
            d.KeineParallelenSpiele = !d.KeineParallelenSpiele;
            d.ParalleleHeimspiele = true;
            Assert.All(d.Spiele, s => Assert.NotEmpty(s.Datum + s.Uhrzeit + s.Heim + s.Gast + s.Spiellokal));

            d.Auswahl = d.Spiele[0];
            await d.BearbeitenCommand.ExecuteAsync(null);
            Assert.Contains(d.Spiele, s => s.Uhrzeit == "18:00");

            d.Auswahl = d.Spiele[^1];
            a.Oberflaeche.Antwort = false;
            await d.LoeschenCommand.ExecuteAsync(null);
            Assert.Equal(spiele, d.Spiele.Count);
            a.Oberflaeche.Antwort = true;
            await d.LoeschenCommand.ExecuteAsync(null);
            Assert.Equal(spiele - 1, d.Spiele.Count);
            return d.Bestaetigen();
        };

        await detail.NachbarBearbeitenCommand.ExecuteAsync(zeile);

        Assert.False(string.IsNullOrEmpty(meldung));
        Assert.Equal("4", detail.Nachbarn.Single(n => n.Beschriftung == Namen.RheinduerkheimDamen).Spiellokal);
        Assert.False(detail.IstStandard);
        a.Dialog.StandardCommand.Execute(null);
        Assert.Equal(string.Empty, detail.Standardlokal);
    }

    [Fact]
    public async Task Nachbarmannschaften_bearbeiten_loeschen_und_zuruecksetzen()
    {
        var detail = Arbeitsplatz.Detail<NachbarmannschaftDetailViewModel>(a.Seite<NachbarmannschaftenSeiteViewModel>(), Namen.RheinduerkheimIV);
        int anzahl = detail.Eintraege.Count;
        detail.Auswahl = detail.Eintraege[0];
        a.Oberflaeche.Nachbarmannschaft = d =>
        {
            d.Nummer = 7;
            return Task.FromResult(d.Bestaetigen());
        };
        await detail.BearbeitenCommand.ExecuteAsync(null);
        Assert.False(detail.IstStandard);
        Assert.Equal(detail.Eintraege[0], detail.Auswahl);

        a.Oberflaeche.Antwort = true;
        await detail.LoeschenCommand.ExecuteAsync(null);
        Assert.Equal(anzahl - 1, detail.Eintraege.Count);

        a.Dialog.StandardCommand.Execute(null);
        Assert.Equal(anzahl, detail.Eintraege.Count);
        Assert.True(detail.IstStandard);
    }

    [Fact]
    public async Task Auswaertskoppel_bearbeiten_und_zweite_Anfangszeiten()
    {
        AuswaertskoppelnSeiteViewModel seite = a.Seite<AuswaertskoppelnSeiteViewModel>();
        var dorn = Arbeitsplatz.Detail<AuswaertskoppelDetailViewModel>(seite, Namen.DornDuerkheimIV);
        Assert.NotEmpty(AuswaertskoppelDetailViewModel.Erklaerung);
        a.Oberflaeche.Auswaertskoppel = d =>
        {
            Assert.NotEmpty(d.Partner);
            d.MannschaftA = Namen.RheinduerkheimIV;
            d.MannschaftB = Namen.WsvWorms;
            return d.Bestaetigen();
        };
        await dorn.NeuCommand.ExecuteAsync(null);
        dorn.Auswahl = Assert.Single(dorn.Koppeln);
        a.Oberflaeche.Auswaertskoppel = d =>
        {
            Assert.Equal(Namen.RheinduerkheimIV, d.MannschaftA);
            d.Art = 2;
            return d.Bestaetigen();
        };
        await dorn.BearbeitenCommand.ExecuteAsync(null);
        Assert.Equal(2, Assert.Single(dorn.Koppeln).Art);

        // Der Partner bekommt ein Heimspiel am selben Tag und fast zur selben Zeit: Dann braucht es eine zweite Anfangszeit.
        DateOnly tag = DateOnly.FromDateTime(a.Bearbeitung.Heimtage(Namen.RheinduerkheimIV).First(h => h.IstHeimtag).Datum);
        var wsv = Arbeitsplatz.Detail<WunschterminDetailViewModel>(a.Seite<WunschtermineSeiteViewModel>(), Namen.WsvWorms);
        wsv.Wochen.SelectMany(w => w.Tage).Single(z => z.Tag == tag).Text = "20:00";

        seite = a.Seite<AuswaertskoppelnSeiteViewModel>();
        var detail = Arbeitsplatz.Detail<AuswaertskoppelDetailViewModel>(seite, Namen.RheinduerkheimIV);
        Assert.False(detail.KeineZweitzeiten);
        detail.AllesSetzenCommand.Execute(null);
        Assert.All(detail.Zweitzeiten, z => Assert.Matches(@"^\d\d:\d\d$", z.Text));
        Assert.All(detail.Zweitzeiten, z => Assert.NotNull(z.Wert.Zeit));
        Assert.NotEmpty(detail.Zweitzeiten[0].Beschriftung);
        Assert.False(detail.IstStandard);
        detail.AlleLoeschenCommand.Execute(null);
        Assert.All(detail.Zweitzeiten, z => Assert.Null(z.Wert.Zeit));
        Assert.True(detail.IstStandard);

        dorn = Arbeitsplatz.Detail<AuswaertskoppelDetailViewModel>(seite, Namen.DornDuerkheimIV);
        dorn.Auswahl = dorn.Koppeln[0];
        a.Oberflaeche.Antwort = true;
        await dorn.LoeschenCommand.ExecuteAsync(null);
        Assert.Empty(dorn.Koppeln);
        a.Dialog.StandardCommand.Execute(null);
        Assert.True(dorn.IstStandard);
    }

    [Fact]
    public async Task Pflichtspielzeit_bearbeiten()
    {
        PflichtspieltageSeiteViewModel seite = a.Seite<PflichtspieltageSeiteViewModel>();
        Assert.False(seite.Keine);
        Assert.NotEmpty(PflichtspieltageSeiteViewModel.Erklaerung);
        seite.Auswahl = seite.Zeilen[0];
        a.Oberflaeche.Pflichtspielzeit = d =>
        {
            Assert.Contains(3, PflichtspielzeitdialogViewModel.Anzahlen);
            d.Anzahl = 3;
            return d.Bestaetigen();
        };

        await seite.BearbeitenCommand.ExecuteAsync(null);

        Assert.Equal(3, seite.Zeilen[0].Anzahl);
        Assert.NotEmpty(seite.Zeilen[0].Bezeichnung);
        seite.Zeilen[0].Anzahl = 0;
        Assert.Equal(3, seite.Zeilen[0].Anzahl);
    }

    [Fact]
    public void Setzliste_per_Ziehen_ordnen()
    {
        SetzlistenSeiteViewModel seite = a.Seite<SetzlistenSeiteViewModel>();
        List<string> vorher = [.. seite.Reihenfolge];

        seite.Ziehen(0, 3);
        Assert.Equal(vorher[0], seite.Reihenfolge[2]);
        Assert.Equal(2, seite.Auswahl);

        seite.Ziehen(4, 0);
        Assert.Equal(vorher[4], seite.Reihenfolge[0]);

        List<string> danach = [.. seite.Reihenfolge];
        seite.Ziehen(-1, 2);
        seite.Ziehen(1, 1);
        seite.Ziehen(0, 99);
        Assert.Equal(danach, seite.Reihenfolge);
        Assert.NotEmpty(SetzlistenSeiteViewModel.Erklaerung);
    }

    [Fact]
    public void Heimkoppeln_zuruecksetzen()
    {
        using var r26 = new Arbeitsplatz("datendialoge/D06_Heimkoppeln.xml");
        HeimkoppelnSeiteViewModel seite = r26.Seite<HeimkoppelnSeiteViewModel>();
        Assert.True(seite.HatKopftext);
        var detail = Arbeitsplatz.Detail<HeimkoppelDetailViewModel>(seite, Namen.HanauFc);
        Assert.False(detail.Keine);
        HeimkoppelZeile zeile = detail.Zeilen[0];
        Assert.NotEmpty(zeile.Beschriftung);
        Assert.True(zeile.Auswahl.Count > 1);
        int bisher = zeile.Index;

        zeile.Index = bisher == 0 ? 1 : 0;
        Assert.False(detail.IstStandard);
        r26.Dialog.StandardCommand.Execute(null);

        Assert.True(detail.IstStandard);
        Assert.Equal(bisher, detail.Zeilen[0].Index);
    }

    [Fact]
    public void Spielfreie_Tage_und_60km_zuruecksetzen()
    {
        SpielfreieTageSeiteViewModel frei = a.Seite<SpielfreieTageSeiteViewModel>();
        frei.Zeilen[0].Gewaehlt = true;
        Assert.False(frei.IstStandard);
        a.Dialog.StandardCommand.Execute(null);
        Assert.True(frei.IstStandard);

        var km = Arbeitsplatz.Detail<WochenendeDetailViewModel>(a.Seite<WochenendeSeiteViewModel>(), Namen.RheinduerkheimIV);
        km.Gegner[0].Gewaehlt = true;
        Assert.False(km.IstStandard);
        a.Dialog.StandardCommand.Execute(null);
        Assert.True(km.IstStandard);
        Assert.False(km.Gegner[0].Gewaehlt);
    }

    [Fact]
    public async Task Ligadaten_und_Mannschaften_zuruecksetzen()
    {
        LigadatenSeiteViewModel liga = a.Seite<LigadatenSeiteViewModel>();
        Assert.Contains("Herren", LigadatenSeiteViewModel.Arten);
        liga.Id = "999";
        liga.Art = "Damen";
        liga.Rueckrundenbeginn = liga.Rueckrundenbeginn!.Value.AddDays(7);
        Assert.False(liga.IstStandard);
        a.Dialog.StandardCommand.Execute(null);
        Assert.True(liga.IstStandard);

        MannschaftenSeiteViewModel mannschaften = a.Seite<MannschaftenSeiteViewModel>();
        mannschaften.Auswahl = Namen.WsvWorms;
        a.Oberflaeche.Antwort = true;
        await mannschaften.LoeschenCommand.ExecuteAsync(null);
        a.Dialog.StandardCommand.Execute(null);
        Assert.Contains(Namen.WsvWorms, mannschaften.Namen);
        Assert.True(mannschaften.IstStandard);
    }
}
