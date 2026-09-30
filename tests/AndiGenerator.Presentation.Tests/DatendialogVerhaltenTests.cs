// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Bedienverhalten des Dialogs „Spielplandaten bearbeiten“ und seiner Seiten.</summary>
public sealed class DatendialogVerhaltenTests : IDisposable
{
    // R2 ohne Änderungsdatei: Alle Seiten zeigen anfangs den click-TT-Stand.
    private readonly Arbeitsplatz a = new("datendialoge/D01_Allgemein.xml");

    public void Dispose() => a.Dispose();

    [Fact]
    public void Alle_Seiten_in_der_Reihenfolge_des_Originals()
    {
        string[] titel =
        [
            "Allgemein", "Mannschaften", "Setzliste", "Spiellokale (Kompaktansicht)", "Heimkoppel/Doppelspieltage (Kompakt)",
            "Auswärtskoppeltermine", "Wunschtermine", "Spiele der Nachbarmannschaften", "60km Regel", "Spielfreie Tage", "Pflichtspieltage",
            "Manuell festgelegte Begegnungen", "Heimrecht",
        ];
        Assert.Equal(titel, a.Dialog.Seiten.Select(s => s.Titel));
        Assert.IsType<LigadatenSeiteViewModel>(a.Dialog.Seite);
        Assert.True(a.Dialog.StandardSichtbar);
    }

    [Fact]
    public void Ungueltige_Eingaben_verhindern_Seitenwechsel_und_OK()
    {
        LigadatenSeiteViewModel liga = a.Seite<LigadatenSeiteViewModel>();
        liga.Ende = liga.Beginn!.Value.AddDays(-1);

        a.Dialog.Seite = a.Dialog.Seiten[1];

        Assert.Same(liga, a.Dialog.Seite);
        Assert.True(a.Dialog.HatMeldung);
        Assert.False(a.Dialog.Bestaetigen());

        liga.Standard();
        a.Dialog.Seite = a.Dialog.Seiten[1];
        Assert.Same(a.Dialog.Seiten[1], a.Dialog.Seite);
        Assert.False(a.Dialog.HatMeldung);
    }

    [Fact]
    public void Standard_wiederherstellen_wirkt_nur_auf_die_aktuelle_Seite()
    {
        LigadatenSeiteViewModel liga = a.Seite<LigadatenSeiteViewModel>();
        string name = liga.Name;
        Assert.False(a.Dialog.StandardCommand.CanExecute(null));

        liga.Name = "Anderer Name";
        Assert.True(a.Dialog.StandardCommand.CanExecute(null));
        SetzlistenSeiteViewModel setzliste = a.Seite<SetzlistenSeiteViewModel>();
        setzliste.Aktiv = true;
        a.Dialog.StandardCommand.Execute(null);

        Assert.False(setzliste.Aktiv);
        Assert.Equal("Anderer Name", a.Bearbeitung.Ligadaten.Name);
        a.Seite<LigadatenSeiteViewModel>();
        a.Dialog.StandardCommand.Execute(null);
        Assert.Equal(name, liga.Name);
        Assert.False(a.Dialog.StandardCommand.CanExecute(null));
    }

    [Fact]
    public async Task Loeschen_fragt_nach_und_Abbrechen_aendert_nichts()
    {
        MannschaftenSeiteViewModel seite = a.Seite<MannschaftenSeiteViewModel>();
        Assert.False(seite.LoeschenCommand.CanExecute(null));
        seite.Auswahl = Namen.WsvWorms;

        a.Oberflaeche.Antwort = false;
        await seite.LoeschenCommand.ExecuteAsync(null);
        Assert.Contains(Namen.WsvWorms, seite.Namen);
        Assert.Contains("Mannschaft löschen", a.Oberflaeche.Aufrufe);

        a.Oberflaeche.Mannschaft = d =>
        {
            d.Name = "Egal";
            return false;
        };
        await seite.BearbeitenCommand.ExecuteAsync(null);
        Assert.True(seite.IstStandard);

        a.Oberflaeche.Antwort = true;
        await seite.LoeschenCommand.ExecuteAsync(null);
        Assert.DoesNotContain(Namen.WsvWorms, seite.Namen);
        Assert.False(seite.IstStandard);
    }

    [Fact]
    public async Task Mannschaftsdialog_meldet_doppelten_Namen()
    {
        MannschaftenSeiteViewModel seite = a.Seite<MannschaftenSeiteViewModel>();
        seite.Auswahl = Namen.WsvWorms;
        string? meldung = null;
        a.Oberflaeche.Mannschaft = d =>
        {
            d.Name = Namen.HohenSuelzenIV;
            bool ok = d.Bestaetigen();
            meldung = d.Meldung;
            return ok;
        };

        await seite.BearbeitenCommand.ExecuteAsync(null);

        Assert.Equal("Mannschaftsname ist nicht eindeutig", meldung);
        Assert.True(seite.IstStandard);
    }

    [Fact]
    public void Gewaehlte_Mannschaft_gilt_fuer_alle_Seiten_mit_Mannschaftsliste()
    {
        a.Seite<SpiellokaleSeiteViewModel>().Mannschaft = Namen.HohenSuelzenIV;

        Assert.Equal(Namen.HohenSuelzenIV, a.Seite<HeimrechtSeiteViewModel>().Mannschaft);
        Assert.Equal(Namen.HohenSuelzenIV, a.Seite<WochenendeSeiteViewModel>().Mannschaft);
    }

    [Fact]
    public void Doppelspieltag_ohne_Folgetag_verhindert_Mannschaftswechsel()
    {
        WunschtermineSeiteViewModel seite = a.Seite<WunschtermineSeiteViewModel>();
        var detail = Arbeitsplatz.Detail<WunschterminDetailViewModel>(seite, Namen.RheinduerkheimIV);
        List<WunschterminZelle> zellen = detail.Wochen.SelectMany(w => w.Tage).ToList();
        int i = Enumerable.Range(1, zellen.Count - 2).First(k => !zellen[k - 1].HatTermin && !zellen[k].HatTermin && !zellen[k + 1].HatTermin);
        zellen[i].Text = "20:00 D0";
        Assert.True(zellen[i].HatTermin);

        seite.Mannschaft = Namen.WsvWorms;

        Assert.Equal(Namen.RheinduerkheimIV, seite.Mannschaft);
        Assert.StartsWith("Am ", seite.Meldung, StringComparison.Ordinal);
        zellen[i].Text = "20:00";
        seite.Mannschaft = Namen.WsvWorms;
        Assert.Equal(Namen.WsvWorms, seite.Mannschaft);
    }

    [Fact]
    public void Ungueltige_Uhrzeit_im_Wunschtermin_wird_verworfen()
    {
        var detail = Arbeitsplatz.Detail<WunschterminDetailViewModel>(a.Seite<WunschtermineSeiteViewModel>(), Namen.RheinduerkheimIV);
        WunschterminZelle zelle = detail.Wochen.SelectMany(w => w.Tage).First(z => !z.HatTermin);

        zelle.Text = "25:00";

        Assert.False(zelle.HatTermin);
        Assert.True(detail.IstStandard);
    }

    [Fact]
    public void Setzliste_verschieben_nur_innerhalb_der_Liste()
    {
        SetzlistenSeiteViewModel seite = a.Seite<SetzlistenSeiteViewModel>();
        seite.Auswahl = 0;
        Assert.False(seite.HochCommand.CanExecute(null));
        Assert.True(seite.RunterCommand.CanExecute(null));
        string erster = seite.Reihenfolge[0];

        seite.RunterCommand.Execute(null);

        Assert.Equal(erster, seite.Reihenfolge[1]);
        Assert.Equal(1, seite.Auswahl);
        seite.Auswahl = seite.Reihenfolge.Count - 1;
        Assert.False(seite.RunterCommand.CanExecute(null));
    }

    [Fact]
    public void Heimrecht_Standard_bedeutet_Automatisch_und_egal()
    {
        var detail = Arbeitsplatz.Detail<HeimrechtDetailViewModel>(a.Seite<HeimrechtSeiteViewModel>(), Namen.WsvWorms);
        Assert.True(detail.IstStandard);
        Assert.Equal(a.Bearbeitung.Mannschaftsnamen().Count + 1, detail.Anzahlen.Count);

        detail.AnzahlIndex = 2;
        detail.AnzahlIndex = -1;
        detail.Gegner[0].Index = 2;
        Assert.Equal(2, detail.AnzahlIndex);
        Assert.Equal(Heimrecht.Rueckrunde, detail.Gegner[0].Wert);
        Assert.False(detail.IstStandard);

        a.Dialog.StandardCommand.Execute(null);

        Assert.True(detail.IstStandard);
        Assert.Equal(0, detail.AnzahlIndex);
    }

    [Fact]
    public async Task Festgelegte_Begegnungen_neu_bearbeiten_loeschen()
    {
        VorgabespieleSeiteViewModel seite = a.Seite<VorgabespieleSeiteViewModel>();
        string? meldung = null;
        a.Oberflaeche.Vorgabespiel = d =>
        {
            Assert.Equal(a.Bearbeitung.Ligadaten.Beginn.ToDateTime(TimeOnly.MinValue), d.Datum);
            d.Heim = Namen.WsvWorms;
            d.Gast = Namen.WsvWorms;
            Assert.False(d.Bestaetigen());
            meldung = d.Meldung;
            d.Gast = Namen.HohenSuelzenIV;
            d.Uhrzeit = new TimeSpan(19, 30, 0);
            return d.Bestaetigen();
        };
        await seite.NeuCommand.ExecuteAsync(null);
        Assert.Equal("Die Mannschaft kann nicht gegen sich selbst spielen", meldung);
        VorgabespielZeile zeile = Assert.Single(seite.Zeilen);
        Assert.Equal("19:30", zeile.Uhrzeit);

        seite.Auswahl = zeile;
        a.Oberflaeche.Vorgabespiel = d =>
        {
            Assert.Equal(Namen.WsvWorms, d.Heim);
            d.Spiellokal = "3";
            return d.Bestaetigen();
        };
        await seite.BearbeitenCommand.ExecuteAsync(null);
        Assert.Equal("3", Assert.Single(seite.Zeilen).Spiellokal);

        seite.Auswahl = seite.Zeilen[0];
        a.Oberflaeche.Antwort = true;
        await seite.LoeschenCommand.ExecuteAsync(null);
        Assert.Empty(seite.Zeilen);

        // Wie im Original (NodesAreTheSame): Die nun leere Liste gilt nicht als Standard, solange der click-TT-Stand keine hat.
        Assert.False(seite.IstStandard);
        a.Dialog.StandardCommand.Execute(null);
        Assert.True(seite.IstStandard);
    }

    [Fact]
    public async Task Pflichtspielzeit_Pruefung_und_Loeschen()
    {
        PflichtspieltageSeiteViewModel seite = a.Seite<PflichtspieltageSeiteViewModel>();
        int vorher = seite.Zeilen.Count;
        string? meldung = null;
        a.Oberflaeche.Pflichtspielzeit = d =>
        {
            d.Von = new DateTime(2027, 3, 7, 0, 0, 0, DateTimeKind.Unspecified);
            d.Bis = new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Unspecified);
            Assert.False(d.Bestaetigen());
            meldung = d.Meldung;
            return false;
        };
        await seite.NeuCommand.ExecuteAsync(null);
        Assert.Equal("Das \"bis\" Datum darf nicht kleiner als das \"von\" Datum sein", meldung);
        Assert.Equal(vorher, seite.Zeilen.Count);

        seite.Auswahl = seite.Zeilen[0];
        seite.LoeschenCommand.Execute(null);
        Assert.Equal(vorher - 1, seite.Zeilen.Count);
        Assert.False(seite.IstStandard);
        a.Dialog.StandardCommand.Execute(null);
        Assert.Equal(vorher, seite.Zeilen.Count);
    }

    [Fact]
    public async Task Nachbarspiel_ohne_Datum_wird_abgelehnt()
    {
        var detail = Arbeitsplatz.Detail<NachbarmannschaftDetailViewModel>(a.Seite<NachbarmannschaftenSeiteViewModel>(), Namen.RheinduerkheimIV);
        string? meldung = null;
        a.Oberflaeche.Nachbarspiel = d =>
        {
            d.Datum = null;
            d.Gegner = "X";
            bool ok = d.Bestaetigen();
            meldung = d.Meldung;
            return ok;
        };
        a.Oberflaeche.Nachbarmannschaft = async d =>
        {
            d.Name = "Nachbar";
            d.Art = "Herren";
            await d.NeuCommand.ExecuteAsync(null);
            return false;
        };
        int vorher = detail.Eintraege.Count;

        await detail.NeuCommand.ExecuteAsync(null);

        Assert.Equal("Bitte geben Sie ein Datum ein", meldung);
        Assert.Equal(vorher, detail.Eintraege.Count);
        Assert.True(detail.IstStandard);
    }

    [Fact]
    public async Task Auswaertskoppel_neu_und_loeschen()
    {
        var detail = Arbeitsplatz.Detail<AuswaertskoppelDetailViewModel>(a.Seite<AuswaertskoppelnSeiteViewModel>(), Namen.DornDuerkheimIV);
        a.Oberflaeche.Auswaertskoppel = d =>
        {
            d.MannschaftA = Namen.WsvWorms;
            d.MannschaftB = Namen.WsvWorms;
            Assert.False(d.Bestaetigen());
            d.MannschaftB = Namen.HohenSuelzenIV;
            d.Art = 1;
            return d.Bestaetigen();
        };
        await detail.NeuCommand.ExecuteAsync(null);
        Auswaertskoppeldaten koppel = Assert.Single(detail.Koppeln);
        Assert.Equal(1, koppel.Art);

        detail.Auswahl = koppel;
        a.Oberflaeche.Antwort = true;
        await detail.LoeschenCommand.ExecuteAsync(null);
        Assert.Empty(detail.Koppeln);
    }
}
