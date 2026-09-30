// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.IO.Compression;
using System.Xml.Linq;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Rendering;
using Dock.Model.Core;

namespace AndiGenerator.Presentation.Tests;

/// <summary>
/// Abläufe des Hauptfensters mit einer Kopie von Referenzfall R2 (ohne Änderungsdatei, also mit Erststart). Staffelordner
/// und Optionen liegen in einem temporären Ordner, nicht unter <c>%LOCALAPPDATA%</c>.
/// </summary>
public sealed class HauptfensterTests : IDisposable
{
    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-hf-" + Guid.NewGuid().ToString("N"));
    private readonly string datei;
    private readonly TestOberflaeche o = new();
    private readonly HauptfensterViewModel hf;

    public HauptfensterTests()
    {
        Directory.CreateDirectory(Path.Combine(ordner, "daten"));
        datei = Path.Combine(ordner, "daten", "staffel.xml");
        File.Copy(Path.Combine(Testdaten.Referenz, "datendialoge", "D01_Allgemein.xml"), datei);
        o.DateiZumOeffnen = datei;
        hf = new HauptfensterViewModel(o, () => throw new InvalidOperationException("Keine Fenster im Test"), Path.Combine(ordner, "basis"), null);
    }

    public void Dispose()
    {
        hf.Dispose();
        try
        {
            Directory.Delete(ordner, recursive: true);
        }
        catch (IOException)
        {
            // Aufräumen ist nicht Teil des Tests.
        }
    }

    [Fact]
    public async Task Anleitung_oeffnet_die_PDF_neben_dem_Programm_oder_meldet_ihr_Fehlen()
    {
        string pfad = Path.Combine(AppContext.BaseDirectory, HauptfensterViewModel.Anleitungsdatei);
        File.Delete(pfad);
        await hf.AnleitungCommand.ExecuteAsync(null);
        Assert.Contains(o.Meldungen, m => m.StartsWith("Anleitung: ", StringComparison.Ordinal));
        Assert.Empty(o.Angezeigt);

        await File.WriteAllTextAsync(pfad, "%PDF-1.4");
        try
        {
            await hf.AnleitungCommand.ExecuteAsync(null);
            Assert.Equal(pfad, Assert.Single(o.Angezeigt));
        }
        finally
        {
            File.Delete(pfad);
        }
    }

    [Fact]
    public async Task Oeffnen_zeigt_die_Einrichtung_und_Uebernehmen_speichert_die_Aenderungen()
    {
        Assert.False(hf.IstGeoeffnet);
        Assert.False(hf.StartenCommand.CanExecute(null));
        Assert.False(hf.ZeigtEinrichtung);

        await hf.OeffnenCommand.ExecuteAsync(null);

        Assert.True(hf.IstGeoeffnet);
        Assert.Contains("4. Kreisklasse Gruppe A", hf.Titel, StringComparison.Ordinal);
        Assert.True(hf.ZeigtEinrichtung);
        Assert.False(hf.ZeigtArbeitsbereich);
        EinrichtungViewModel einrichtung = hf.Einrichtung!;
        Assert.EndsWith(" einrichten", einrichtung.Titel, StringComparison.Ordinal);
        Assert.Contains(einrichtung.Schritte, s => s.Titel == "Setzliste");
        Assert.Equal(Enumerable.Range(1, einrichtung.Schritte.Count), einrichtung.Schritte.Select(s => s.Nummer));
        Assert.Equal(["Staffel öffnen"], o.Aufrufe);

        // Spiellokale: eingebettete Seite bearbeiten und übernehmen, dann geht es zum nächsten offenen Punkt.
        Einrichtungsschritt lokale = einrichtung.Schritte.Single(s => s.Titel == "Spiellokale");
        lokale.WaehlenCommand.Execute(null);
        Assert.Same(lokale, einrichtung.Aktiv);
        Assert.Equal("jetzt dran", lokale.Status);
        Assert.Equal("Übernehmen und weiter", einrichtung.UebernehmenText);
        SpiellokaleSeiteViewModel seite = Assert.IsType<SpiellokaleSeiteViewModel>(Assert.Single(lokale.Seite!.Seiten));
        seite.Mannschaft = Namen.WsvWorms;
        Assert.IsType<SpiellokalDetailViewModel>(seite.Detail).Standardlokal = "2";
        await einrichtung.UebernehmenCommand.ExecuteAsync(null);

        Assert.Equal(Schrittzustand.Erledigt, lokale.Zustand);
        Assert.Equal("✓", lokale.Marke);
        Assert.Null(lokale.Seite);
        Assert.NotSame(lokale, einrichtung.Aktiv);
        Assert.True(File.Exists(Path.ChangeExtension(datei, ".modifications")));
        Assert.StartsWith("1 von ", einrichtung.FortschrittText, StringComparison.Ordinal);

        // Alle übrigen überspringen: nichts ändert sich, die Liste ist komplett bearbeitet.
        while (einrichtung.Schritte.Any(s => s.Zustand == Schrittzustand.Offen))
        {
            einrichtung.UeberspringenCommand.Execute(null);
        }

        Assert.Equal(100, einrichtung.Fortschritt);
        Assert.Contains(einrichtung.Schritte, s => s.Marke == "–");

        await einrichtung.AbschliessenCommand.ExecuteAsync(null);
        Assert.False(hf.ZeigtEinrichtung);
        Assert.True(hf.ZeigtArbeitsbereich);
        Assert.Contains(PlanQuelle.ClickTt, hf.Planquellen);
        Assert.True(hf.StartenCommand.CanExecute(null));
        Assert.True(hf.DatenCommand.CanExecute(null));
        Assert.False(hf.Laeuft);
    }

    [Fact]
    public async Task Einrichtung_abschliessen_und_generieren_startet_die_Generierung()
    {
        await hf.OeffnenCommand.ExecuteAsync(null);
        EinrichtungViewModel einrichtung = hf.Einrichtung!;
        Assert.False(hf.KostenansichtCommand.CanExecute(null));
        Assert.False(hf.DatenCommand.CanExecute(null));
        Assert.False(hf.DruckenCommand.CanExecute(null));
        Assert.Equal(string.Empty, hf.AktiveAnsicht);
        Assert.True(einrichtung.Aktiv.IstAktiv);
        einrichtung.Schritte[^1].WaehlenCommand.Execute(null);
        Assert.Same(einrichtung.Schritte[^1], einrichtung.Aktiv);
        Assert.False(einrichtung.HatMeldung);

        await einrichtung.AbschliessenUndGenerierenCommand.ExecuteAsync(null);
        Assert.False(hf.ZeigtEinrichtung);
        Assert.True(hf.Laeuft);
        hf.StoppenCommand.Execute(null);
    }

    [Fact]
    public async Task Neu_legt_eine_leere_Plandatei_an_und_zeigt_die_Spielplandaten()
    {
        o.DateiZumSpeichern = null;
        await hf.NeuCommand.ExecuteAsync(null);
        Assert.False(hf.IstGeoeffnet);

        o.DateiZumSpeichern = Path.Combine(ordner, "gibt-es-nicht", "neu.xml");
        await hf.NeuCommand.ExecuteAsync(null);
        Assert.False(hf.IstGeoeffnet);
        Assert.Contains(o.Meldungen, m => m.StartsWith("Neue Datei zur komplett manuellen Eingabe erstellen:", StringComparison.Ordinal));

        string neu = Path.Combine(ordner, "daten", "neu.xml");
        o.DateiZumSpeichern = neu;
        int seiten = 0;
        o.Datendialog = d =>
        {
            seiten = d.Seiten.Count;
            return Task.FromResult(false);
        };

        await hf.NeuCommand.ExecuteAsync(null);

        Assert.True(hf.IstGeoeffnet);
        Assert.True(seiten > 1);
        Assert.Contains("Daten", o.Aufrufe);
        Assert.Contains("<andigenerator>", await File.ReadAllTextAsync(neu), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.ChangeExtension(neu, ".modifications")));
        Assert.Equal("neu", hf.Staffelname);

        hf.GenerierungCommand.Execute(null);
        Assert.False(hf.Laeuft);
        Assert.Contains(o.Meldungen, m => m.StartsWith("Generierung starten: Die Staffel enthält weniger als zwei Mannschaften", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Oeffnen_abbrechen_oder_ungueltige_Datei()
    {
        o.DateiZumOeffnen = null;
        await hf.OeffnenCommand.ExecuteAsync(null);
        Assert.False(hf.IstGeoeffnet);

        string kaputt = Path.Combine(ordner, "daten", "kaputt.xml");
        await File.WriteAllTextAsync(kaputt, "<kein xml");
        o.DateiZumOeffnen = kaputt;
        await hf.OeffnenCommand.ExecuteAsync(null);

        Assert.False(hf.IstGeoeffnet);
        Assert.Contains(o.Meldungen, m => m.StartsWith("Staffel öffnen:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Datendialog_uebernimmt_nur_nach_OK()
    {
        await OeffnenAsync();
        string aenderungen = Path.ChangeExtension(datei, ".modifications");

        o.Datendialog = d =>
        {
            Assert.IsType<LigadatenSeiteViewModel>(d.Seite).Name = "Verworfen";
            return Task.FromResult(false);
        };
        await hf.DatenCommand.ExecuteAsync(null);
        Assert.False(File.Exists(aenderungen));

        o.Datendialog = d =>
        {
            Assert.Equal(13, d.Seiten.Count);
            Assert.IsType<LigadatenSeiteViewModel>(d.Seite).Name = "Umbenannte Staffel";
            return Task.FromResult(d.Bestaetigen());
        };
        await hf.DatenCommand.ExecuteAsync(null);

        Assert.True(File.Exists(aenderungen));
        Assert.EndsWith("Umbenannte Staffel", hf.Titel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ansichten_zeigen_den_click_TT_Plan()
    {
        Assert.False(hf.HatPlan);
        Assert.False(hf.KostenansichtCommand.CanExecute(null));
        Assert.False(hf.TerminwunschansichtCommand.CanExecute(null));
        Assert.Equal(string.Empty, hf.AktiveAnsicht);
        await OeffnenAsync();
        Assert.True(hf.HatPlan);
        Assert.True(hf.KostenansichtCommand.CanExecute(null));
        Assert.True(hf.TerminwunschansichtCommand.CanExecute(null));
        Assert.Equal("Kosten", hf.AktiveAnsicht);
        Assert.Single(Ansichten<KostenAnsichtViewModel>());
        Assert.Empty(Ansichten<TerminplanAnsichtViewModel>());
        Assert.Empty(Ansichten<QualitaetAnsichtViewModel>());

        KostenAnsichtViewModel kosten = Ansichten<KostenAnsichtViewModel>()[0];
        kosten.Quelle = PlanQuelle.ClickTt;
        Assert.InRange(kosten.Kennzahlen.Count, 5, 9);
        Assert.Equal("Gesamtkosten", kosten.Kennzahlen[0].Name);
        Assert.Contains(kosten.Kennzahlen, k => k.Name == "Länge letzter Spieltag");
        string[] nurMitKosten = ["Spiele nicht terminiert", "Ungültige Spiele", "Spiele an spielfreien Tagen", "Vereinsinterne Spiele am Anfang"];
        Assert.All(kosten.Kennzahlen.Where(k => nurMitKosten.Contains(k.Name)), k => Assert.True(k.Wert.Anteil > 0));
        Assert.Equal(7, kosten.Tabelle.Zeilen.Count);

        hf.TerminplanansichtCommand.Execute(null);
        TerminplanAnsichtViewModel plan = Assert.Single(Ansichten<TerminplanAnsichtViewModel>());
        plan.Quelle = PlanQuelle.ClickTt;
        for (int i = 0; i < TerminplanAnsichtViewModel.Darstellungen.Count; i++)
        {
            plan.Darstellung = i;
            Assert.NotEmpty(plan.Zeilen);
        }

        plan.IstSpielplan = true;
        Assert.Equal(0, plan.Darstellung);
        Assert.NotEmpty(plan.Wochen);
        Assert.Empty(plan.Plaene);
        int spiele = plan.Zeilen.Count(z => z.Art == Planzeilenart.Spiel);
        Assert.Equal(spiele, plan.Wochen.Sum(w => w.Spiele.Count) + plan.OhneTermin.Count);
        Assert.True(plan.Wochen[0].HatRundenwechsel);
        plan.IstMannschaften = true;
        Assert.Equal(1, plan.Darstellung);
        Assert.Empty(plan.Wochen);
        Assert.Equal(7, plan.Plaene.Count);
        Assert.All(plan.Plaene, m => Assert.Equal(m.Spiele.Count(s => s.Spiel.Zeit.Length > 0), m.Folge.Count));
        Assert.All(plan.Plaene.SelectMany(m => m.Spiele), s => Assert.Empty(s.Nachbarn));
        plan.IstMannschaften = false;
        Assert.True(plan.IstMannschaften);
        plan.IstMitNachbarn = true;
        Assert.Equal(2, plan.Darstellung);
        plan.SpielWaehlenCommand.Execute(plan.Plaene[0].Spiele[0].Spiel);
        Assert.True(plan.HatAuswahl);
        plan.AuswahlSchliessenCommand.Execute(null);
        Assert.False(plan.HatAuswahl);

        hf.QualitaetsansichtCommand.Execute(null);
        QualitaetAnsichtViewModel qualitaet = Assert.Single(Ansichten<QualitaetAnsichtViewModel>());
        qualitaet.Quelle = PlanQuelle.ClickTt;
        Assert.NotEmpty(qualitaet.Zeilen);

        hf.DiagrammansichtCommand.Execute(null);
        DiagrammAnsichtViewModel diagramm = Assert.Single(Ansichten<DiagrammAnsichtViewModel>());
        diagramm.Quelle = PlanQuelle.ClickTt;
        Assert.NotNull(diagramm.Daten);
        Assert.Equal([Diagrammteil.Spieltage], diagramm.Teile);
        Assert.True(diagramm.HatBeschreibung);
        Assert.Equal(Diagrammteil.Spieltage, Assert.Single(diagramm.Auswahl, w => w.Aktiv).Teil);
        Assert.DoesNotContain(diagramm.Auswahl, w => w.Titel == "Alle");
        diagramm.WaehlenCommand.Execute(diagramm.Auswahl[1]);
        Assert.Equal([diagramm.Auswahl[1].Teil], diagramm.Teile);
        Assert.True(diagramm.Auswahl[1].Aktiv);
        Assert.True(diagramm.HatBeschreibung);

        hf.TerminwunschansichtCommand.Execute(null);
        hf.TerminwunschansichtCommand.Execute(null);
        TerminwunschAnsichtViewModel wuensche = Assert.Single(Ansichten<TerminwunschAnsichtViewModel>());
        Assert.NotEmpty(wuensche.Zeilen);
        Assert.Equal(7, wuensche.Karten.Count);
        Assert.True(wuensche.IstUebersicht);
        wuensche.IstKarten = true;
        Assert.False(wuensche.IstUebersicht);
        wuensche.IstKarten = false;
        Assert.True(wuensche.IstKarten);
        wuensche.IstUebersicht = true;
        Assert.False(wuensche.IstKarten);

        hf.NachbarterminansichtCommand.Execute(null);
        hf.NachbarterminansichtCommand.Execute(null);
        NachbarterminAnsichtViewModel nachbarn = Assert.Single(Ansichten<NachbarterminAnsichtViewModel>());
        Assert.NotEmpty(nachbarn.Zeilen);
        Assert.False(nachbarn.KeineTermine);
        Assert.Equal(
            nachbarn.Zeilen.Count(z => z.Art is Planzeilenart.Spiel or Planzeilenart.EchtesNachbarspiel),
            nachbarn.Karten.Sum(k => k.Tage.Sum(t => t.Spiele.Count)));
        Assert.All(nachbarn.Karten, k => Assert.Contains(" an ", k.Bilanz, StringComparison.Ordinal));

        // Ein weiterer Klick holt die offene Ansicht nach vorn, statt eine zweite zu öffnen.
        hf.KostenansichtCommand.Execute(null);
        hf.TerminplanansichtCommand.Execute(null);
        hf.QualitaetsansichtCommand.Execute(null);
        Assert.Single(Ansichten<KostenAnsichtViewModel>());
        Assert.Single(Ansichten<TerminplanAnsichtViewModel>());
        Assert.Single(Ansichten<QualitaetAnsichtViewModel>());
        Assert.Equal("Qualitaet", hf.AktiveAnsicht);
    }

    [Fact]
    public async Task Kostenmatrix_Details_und_Gewichtung_ohne_Dialog()
    {
        await OeffnenAsync();
        KostenAnsichtViewModel kosten = Ansichten<KostenAnsichtViewModel>()[0];
        kosten.Quelle = PlanQuelle.ClickTt;
        Kostenmatrix matrix = kosten.Matrix;
        Assert.True(kosten.HatMatrix);
        Assert.Equal(7, matrix.Zeilen.Count);
        Assert.All(matrix.Spalten, s => Assert.True(s.HatKosten));
        Assert.Equal(matrix.Zeilen.OrderByDescending(z => z.Balken).Select(z => z.Name), matrix.Zeilen.Select(z => z.Name));
        Assert.NotEmpty(matrix.Verteilung);
        Assert.True(kosten.OhneDetail);
        Assert.Null(kosten.Detail);

        kosten.LeereUmschaltenCommand.Execute(null);
        Assert.Equal(matrix.Spalten.Count + matrix.OhneKosten, kosten.Matrix.Spalten.Count);
        kosten.LeereUmschaltenCommand.Execute(null);
        kosten.ZeigtVerstoesse = true;
        Assert.False(kosten.ZeigtKosten);
        Assert.DoesNotContain(kosten.Matrix.Zeilen.SelectMany(z => z.Zellen), z => z.Text.Contains('(', StringComparison.Ordinal));
        kosten.ZeigtKosten = true;

        Matrixzelle zelle = kosten.Matrix.Zeilen.SelectMany(z => z.Zellen).OrderByDescending(z => z.Anteil).First();
        kosten.ZelleWaehlenCommand.Execute(zelle);
        Kostendetail detail = Assert.IsType<Kostendetail>(kosten.Detail);
        Assert.True(detail.IstZelle);
        Assert.Equal(zelle.Mannschaft, detail.Unterzeile);
        Assert.Equal(7, detail.Stufen.Count);
        Assert.Single(detail.Stufen, st => st.Aktiv);
        Assert.Single(kosten.Matrix.Zeilen.SelectMany(z => z.Zellen), z => z.Gewaehlt);

        kosten.StufeWaehlenCommand.Execute(detail.Stufen.First(st => st.Wert == Gewichtung.ExtremHoch));
        Assert.Single(Optionsdateien());
        Assert.DoesNotContain(o.Aufrufe, a => a.StartsWith("Gewichtung", StringComparison.Ordinal));
        detail = Assert.IsType<Kostendetail>(kosten.Detail);
        Assert.Equal(Gewichtung.ExtremHoch, Assert.Single(detail.Stufen, st => st.Aktiv).Wert);
        Assert.Contains(kosten.Matrix.Zeilen.SelectMany(z => z.Zellen), z => z.Gewaehlt && z.Geaendert);

        kosten.ZurKostenartCommand.Execute(null);
        Assert.Equal("KOSTENART", kosten.Detail?.Art);
        Assert.Single(kosten.Matrix.Spalten, s => s.Gewaehlt);
        kosten.ZeileWaehlenCommand.Execute(kosten.Matrix.Zeilen[0]);
        Assert.Equal("MANNSCHAFT", kosten.Detail?.Art);
        kosten.AnteilWaehlenCommand.Execute(kosten.Matrix.Verteilung[0]);
        Assert.Equal(kosten.Matrix.Verteilung[0].Name, kosten.Detail?.Titel);
        Kennzahl kennzahl = kosten.Kennzahlen.First(k => k.Wert.Ziel is not null);
        kosten.KennzahlWaehlenCommand.Execute(kennzahl);
        Assert.Equal(kennzahl.Name, kosten.Detail?.Titel);
        Assert.True(kosten.Detail?.HatGewichtung);

        kosten.DetailSchliessenCommand.Execute(null);
        Assert.False(kosten.HatDetail);
        kosten.LeereUmschaltenCommand.Execute(null);
        Assert.True(kosten.LeereZeigen);
    }

    [Fact]
    public async Task Gewichtung_und_Einstellungen_werden_gespeichert()
    {
        await OeffnenAsync();
        KostenAnsichtViewModel kosten = Ansichten<KostenAnsichtViewModel>()[0];
        kosten.Quelle = PlanQuelle.ClickTt;
        Kostenfeld feld = kosten.Tabelle.Zeilen.SelectMany(z => z.Zellen).First(z => z.Ziel is not null);

        await kosten.GewichtungAendernCommand.ExecuteAsync(feld);
        Assert.Empty(Optionsdateien());

        o.GewaehlteGewichtung = Gewichtung.NichtBeruecksichtigen;
        await kosten.GewichtungAendernCommand.ExecuteAsync(feld);
        Assert.Single(Optionsdateien());
        Assert.Contains(o.Aufrufe, a => a.StartsWith("Gewichtung der Mannschaft", StringComparison.Ordinal));

        hf.EinstellungenCommand.Execute(null);
        OptionenAnsichtViewModel optionen = Assert.Single(Ansichten<OptionenAnsichtViewModel>());
        Assert.False(optionen.Doppelrunde);
        optionen.Doppelrunde = true;
        Assert.True(optionen.Doppelrunde);
        Assert.False(optionen.MittenBearbeitbar);
        optionen.AutomatischeMitte = false;
        Assert.True(optionen.MittenBearbeitbar);
        Assert.NotNull(optionen.Mitte1);
        optionen.PlanGewichte[0].Stufe = 0;
        optionen.KostenartGewichte[0].Stufe = 1;
        optionen.MannschaftsGewichte[0].Stufe = 2;
        Assert.Equal(2, optionen.MannschaftsGewichte[0].Stufe);
        Assert.NotEmpty(optionen.MannschaftsGewichte[0].Name);
        Assert.NotEmpty(GewichtungsEintrag.Stufen);
        optionen.FreitagZaehltZumWochenende = !optionen.FreitagZaehltZumWochenende;
        optionen.Mitte1 = optionen.Mitte1.Value.AddDays(7);
        optionen.Mitte2 = optionen.Mitte2!.Value.AddDays(7);
        optionen.RundenplanungIndex = 1;
        Assert.Equal(1, optionen.RundenplanungIndex);
        Assert.NotEmpty(OptionenAnsichtViewModel.Rundenplanungen);

        o.Antwort = false;
        await optionen.StandardCommand.ExecuteAsync(null);
        Assert.True(optionen.Doppelrunde);
        o.Antwort = true;
        await optionen.StandardCommand.ExecuteAsync(null);
        Assert.False(optionen.Doppelrunde);
        Assert.True(optionen.AutomatischeMitte);
    }

    [Fact]
    public async Task Generierung_Pause_Plan_merken_exportieren_und_entfernen()
    {
        Assert.False(hf.GenerierungCommand.CanExecute(null));
        await OeffnenAsync();
        Assert.False(hf.PlanMerkenCommand.CanExecute(null));
        Assert.Equal("Generierung starten", hf.GenerierungText);
        Assert.True(hf.ZeigtStart);
        Assert.Equal("–", hf.Anzeige.Kosten);

        // Meldungen der Generierung kommen aus einem Hintergrund-Thread; wie auf dem UI-Thread laufen sie nur hier.
        o.Sammeln = true;
        hf.GenerierungCommand.Execute(null);
        Assert.True(hf.Laeuft);
        Assert.Equal("Pausieren", hf.GenerierungText);
        Assert.True(hf.ZeigtPause);
        for (int i = 0; i < 300 && !hf.PlanMerkenCommand.CanExecute(null); i++)
        {
            await Task.Delay(100);
            o.Abarbeiten();
        }

        Assert.True(hf.PlanMerkenCommand.CanExecute(null));
        Assert.NotEqual("–", hf.Anzeige.Pflicht);
        Assert.NotEmpty(hf.Anzeige.PflichtHinweis);
        hf.GenerierungCommand.Execute(null);
        Assert.True(hf.Pausiert);
        Assert.Equal("Fortsetzen", hf.GenerierungText);
        Assert.True(hf.ZeigtStart);
        hf.PauseCommand.Execute(null);
        Assert.False(hf.Pausiert);
        Assert.Equal("Pausieren", hf.GenerierungText);

        o.Texteingabe = "Testplan";
        await hf.PlanMerkenCommand.ExecuteAsync(null);
        PlanQuelle gemerkt = Assert.Single(hf.Planquellen, q => q.Art == PlanQuellenArt.GemerkterPlan);
        Assert.Equal("Testplan", gemerkt.Eintrag!.Name);

        string csv = Path.Combine(ordner, "export.csv");
        o.DateiZumSpeichern = csv;
        o.Antwort = true;
        await hf.CsvExportierenCommand.ExecuteAsync(null);
        Assert.True(File.Exists(csv));

        hf.StoppenCommand.Execute(null);
        Assert.False(hf.Laeuft);
        Assert.Contains(hf.Startplaene, p => p.Quelle == PlanQuelle.Laufend);
        Assert.Contains(hf.Startplaene, p => p.Quelle == gemerkt);

        hf.Startplan = hf.Startplaene.Single(p => p.Quelle == gemerkt);
        hf.StartenCommand.Execute(null);
        Assert.True(hf.Laeuft);
        hf.StoppenCommand.Execute(null);

        KostenAnsichtViewModel kosten = Ansichten<KostenAnsichtViewModel>()[0];
        kosten.Quelle = gemerkt;
        Assert.NotEmpty(kosten.Tabelle.Zeilen);
        Assert.True(kosten.EntfernenCommand.CanExecute(null));
        await kosten.EntfernenCommand.ExecuteAsync(null);
        Assert.DoesNotContain(hf.Planquellen, q => q.Art == PlanQuellenArt.GemerkterPlan);
    }

    [Fact]
    public async Task Drucken_baut_den_Ausdruck_aus_dem_gewaehlten_Plan_und_zeigt_die_PDF_Datei()
    {
        Assert.False(hf.DruckenCommand.CanExecute(null));
        await hf.DruckenCommand.ExecuteAsync(null);
        Assert.Empty(o.Aufrufe);
        await OeffnenAsync();
        Assert.True(hf.DruckenCommand.CanExecute(null));
        DruckauswahlViewModel? gezeigt = null;
        o.Druckauswahl = a =>
        {
            gezeigt = a;
            a.Querformat = true;
            return true;
        };
        o.DateiZumSpeichern = Path.Combine(ordner, "druck.pdf");

        await hf.DruckenCommand.ExecuteAsync(null);

        Assert.NotNull(gezeigt);
        Assert.Equal(PlanQuelle.ClickTt, Assert.Single(gezeigt.Quellen));
        Druckdokument d = Assert.IsType<Druckdokument>(o.Ausdruck);
        string[] titel = ["Terminwünsche", "Termine der Nachbarmannschaften", "Kosten", "Spielplan", "Mannschaftspläne", "Mannschaftspläne mit Nachbarmannschaften", "Diagramme"];
        Assert.Equal(titel, d.Abschnitte.Select(a => a.Titel));
        Assert.Equal(PlanQuelle.ClickTt.Name, d.Plan);
        Assert.Contains("4. Kreisklasse Gruppe A", d.Staffel, StringComparison.Ordinal);
        Assert.Same(Seitenformat.A4Quer, o.Format);
        Assert.Equal(o.DateiZumSpeichern, Assert.Single(o.Angezeigt));

        Assert.IsType<Grafik>(d.Abschnitte[0].Bausteine[0]);
        List<Textzeile> wuensche = d.Abschnitte[0].Bausteine.OfType<Textzeile>().ToList();
        Assert.Contains(wuensche, t => t.Fett && t.Einzug < 1 && t.Text.EndsWith(':'));
        Assert.Contains(wuensche, t => !t.Fett && t.Einzug > 31);

        Grafik kennzahlen = Assert.IsType<Grafik>(d.Abschnitte[2].Bausteine[0]);
        var kacheln = new Messflaeche();
        kennzahlen.Zeichnung(kacheln);
        Assert.Contains("Gesamtkosten", kacheln.Texte);
        Tabelle kosten = Assert.IsType<Tabelle>(d.Abschnitte[2].Bausteine[2]);
        Assert.True(kosten.Matrix);
        Assert.Equal("Mannschaft", kosten.Kopf![0]);
        Assert.Equal("Gesamt", kosten.Kopf[^1]);
        Assert.Equal(8, kosten.Zeilen.Count);
        Assert.True(kosten.Zeilen[^1].Fett);
        Assert.Equal("Summe", kosten.Zeilen[^1].Zellen[0].Text);
        Assert.All(kosten.Zeilen, z => Assert.Equal(kosten.Kopf.Count, z.Zellen.Count));
        Assert.Contains(kosten.Zeilen.SelectMany(z => z.Zellen), z => z.Hintergrund is Farbe h && h.R > h.G && z.Farbe is not null);

        Tabelle spielplan = Assert.IsType<Tabelle>(Assert.Single(d.Abschnitte[3].Bausteine));
        Assert.True(spielplan.LetzteSpalteUmbrechen);
        Assert.Contains(spielplan.Zeilen, z => z.Art == Zeilenart.Abstand);
        Assert.Contains(spielplan.Zeilen, z => z.Art == Zeilenart.Normal && z.Zellen[3].Text == "-");
        Tabelle mannschaften = Assert.IsType<Tabelle>(Assert.Single(d.Abschnitte[4].Bausteine));
        Assert.Equal(7, mannschaften.Zeilen.Count(z => z.Art == Zeilenart.Ueberschrift));
        Tabelle mitNachbarn = Assert.IsType<Tabelle>(Assert.Single(d.Abschnitte[5].Bausteine));
        Assert.True(mitNachbarn.Zeilen.Count >= mannschaften.Zeilen.Count);
        Tabelle nachbarn = Assert.IsType<Tabelle>(Assert.Single(d.Abschnitte[1].Bausteine));
        Assert.Contains(nachbarn.Zeilen, z => z.Art == Zeilenart.Ueberschrift);
        List<Grafik> diagramme = d.Abschnitte[6].Bausteine.Cast<Grafik>().ToList();
        Assert.True(diagramme.Count >= 4);
        Assert.All(diagramme, g => Assert.True(g.Breite > 300 && g.Hoehe > 0));

        // Alle Seiten wie beim PDF umbrechen und zeichnen (Diagramme und Terminwunschraster über dieselbe Routine).
        var flaeche = new Messflaeche();
        IReadOnlyList<Druckseite> seiten = Seitenumbruch.Umbrechen(d, Seitenformat.A4Quer, flaeche);
        for (int i = 0; i < seiten.Count; i++)
        {
            seiten[i].Zeichnen(flaeche, d, Seitenformat.A4Quer, i + 1, seiten.Count);
        }

        Assert.Contains("Spieltage", flaeche.Texte);
        Assert.Contains("Wechsel Heim/Auswärts", flaeche.Texte);
        Assert.Contains("Anzahl Spiele pro Woche", flaeche.Texte);
        Assert.Contains("Sperrtermin", flaeche.Texte);
        Assert.Contains(flaeche.Punkte, p => p.Art == "Senkrecht");
        Assert.Contains(flaeche.Punkte, p => p.Art == "Kreis");
        Assert.StartsWith("Seite ", flaeche.Texte[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Meldungen_je_Mannschaft_und_Auswahl_einer_Mannschaft()
    {
        await OeffnenAsync();
        hf.MeldungsansichtCommand.Execute(null);
        MeldungenAnsichtViewModel meldungen = Assert.Single(Ansichten<MeldungenAnsichtViewModel>());
        Assert.Empty(meldungen.Zeilen);
        Assert.Equal([MeldungenAnsichtViewModel.AlleMannschaften], meldungen.Mannschaften);

        meldungen.Quelle = PlanQuelle.ClickTt;

        Assert.Equal(8, meldungen.Mannschaften.Count);
        Assert.Contains(" Mannschaften", meldungen.Zusammenfassung, StringComparison.Ordinal);
        Assert.True(meldungen.Zeilen[0].Ueberschrift);
        Assert.Contains(meldungen.Zeilen, z => !z.Ueberschrift);
        string erste = meldungen.Zeilen[0].Text.TrimEnd(':');
        int alle = meldungen.Zeilen.Count;

        Assert.NotEmpty(meldungen.Gruppen);
        Assert.False(meldungen.KeineMeldungen);
        Assert.Equal(meldungen.Gruppen.OrderByDescending(g => g.Meldungen.Count).Select(g => g.Name), meldungen.Gruppen.Select(g => g.Name));
        Assert.Equal(meldungen.Zeilen.Count(z => !z.Ueberschrift), meldungen.Gruppen.Sum(g => g.Meldungen.Count));

        meldungen.Mannschaft = erste;
        Assert.Equal(erste + ":", Assert.Single(meldungen.Zeilen, z => z.Ueberschrift).Text);
        Assert.Equal(erste, Assert.Single(meldungen.Gruppen).Name);

        meldungen.Mannschaft = null;
        Assert.Equal(MeldungenAnsichtViewModel.AlleMannschaften, meldungen.Mannschaft);
        Assert.Equal(alle, meldungen.Zeilen.Count);

        meldungen.Mannschaft = erste;
        meldungen.Quelle = PlanQuelle.Laufend;
        Assert.Empty(meldungen.Zeilen);
        Assert.Equal(MeldungenAnsichtViewModel.AlleMannschaften, meldungen.Mannschaft);
        Assert.Equal(string.Empty, meldungen.Zusammenfassung);
    }

    [Fact]
    public async Task Oeffnen_fuellt_Kopf_und_Zuletzt_geoeffnet()
    {
        Assert.False(hf.HatZuletzt);
        await OeffnenAsync();

        Assert.Contains("4. Kreisklasse Gruppe A", hf.Staffelname, StringComparison.Ordinal);
        Assert.StartsWith("click-TT-Datei · ", hf.Staffelinfo, StringComparison.Ordinal);
        Assert.EndsWith("staffel.xml", hf.Staffelinfo, StringComparison.Ordinal);
        Zuletztzeile zeile = Assert.Single(hf.Zuletzt);
        Assert.Equal(Path.GetFullPath(datei), zeile.Pfad);
        Assert.Equal(hf.Staffelname, zeile.Name);
        Assert.StartsWith("heute, ", zeile.Zeit, StringComparison.Ordinal);
        Assert.True(hf.HatZuletzt);

        using var zweites = new HauptfensterViewModel(o, () => throw new InvalidOperationException("Keine Fenster im Test"), Path.Combine(ordner, "basis"), null);
        Assert.Equal(zeile.Pfad, Assert.Single(zweites.Zuletzt).Pfad);
        await zweites.ZuletztOeffnenCommand.ExecuteAsync(zeile.Pfad);
        Assert.True(zweites.IstGeoeffnet);
        await zweites.ZuletztOeffnenCommand.ExecuteAsync(null);

        zweites.ZuletztEntfernenCommand.Execute(null);
        Assert.True(zweites.HatZuletzt);
        zweites.ZuletztEntfernenCommand.Execute(zeile.Pfad.ToUpperInvariant());
        Assert.False(zweites.HatZuletzt);
        Assert.True(File.Exists(zeile.Pfad));
    }

    [Fact]
    public async Task Excel_Export_speichert_den_Plan_der_Ansicht()
    {
        await OeffnenAsync();
        KostenAnsichtViewModel kosten = Ansichten<KostenAnsichtViewModel>()[0];
        Assert.False(kosten.ExcelCommand.CanExecute(null));
        kosten.Quelle = PlanQuelle.ClickTt;
        Assert.True(kosten.ExcelCommand.CanExecute(null));

        o.DateiZumSpeichern = null;
        await kosten.ExcelCommand.ExecuteAsync(null);
        Assert.Empty(o.Angezeigt);

        string pfad = Path.Combine(ordner, "plan.xlsx");
        o.DateiZumSpeichern = pfad;
        await kosten.ExcelCommand.ExecuteAsync(null);

        Assert.Equal(pfad, Assert.Single(o.Angezeigt));
        using (ZipArchive zip = await ZipFile.OpenReadAsync(pfad))
        {
            XNamespace haupt = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XDocument mappe = Xml(zip, "xl/workbook.xml");
            string[] blaetter = ["Übersicht", "Spielplan", "Mannschaftspläne", "Kosten"];
            Assert.Equal(blaetter, mappe.Descendants(haupt + "sheet").Select(b => (string)b.Attribute("name")!));
            XDocument spielplan = Xml(zip, "xl/worksheets/sheet2.xml");
            int spiele = spielplan.Descendants(haupt + "row").Count() - 1;
            Assert.True(spiele > 0);
            XDocument mannschaften = Xml(zip, "xl/worksheets/sheet3.xml");
            Assert.Equal(2 * spiele, mannschaften.Descendants(haupt + "row").Count() - 1);
            XDocument kostenblatt = Xml(zip, "xl/worksheets/sheet4.xml");
            Assert.Contains(kostenblatt.Descendants(haupt + "t"), t => t.Value == "Summe");
        }

        o.DateiZumSpeichern = Path.Combine(ordner, "gibt-es-nicht", "plan.xlsx");
        await kosten.ExcelCommand.ExecuteAsync(null);
        Assert.Contains(o.Meldungen, m => m.StartsWith("Nach Excel exportieren:", StringComparison.Ordinal));
        Assert.Single(o.Angezeigt);
    }

    [Fact]
    public async Task Oeffnen_beendet_die_Generierung_der_bisherigen_Staffel()
    {
        Assert.False(hf.StartplanWaehlbar);
        await OeffnenAsync();
        Assert.True(hf.StartplanWaehlbar);
        hf.GenerierungCommand.Execute(null);
        Assert.False(hf.StartplanWaehlbar);
        hf.GenerierungCommand.Execute(null);
        Assert.True(hf.Pausiert);
        Assert.False(hf.StartplanWaehlbar);
        Assert.Equal("Fortsetzen", hf.GenerierungText);

        await OeffnenAsync();

        Assert.False(hf.Laeuft);
        Assert.False(hf.Pausiert);
        Assert.Equal("Generierung starten", hf.GenerierungText);
        Assert.False(hf.StoppenCommand.CanExecute(null));
        Assert.True(hf.StartplanWaehlbar);
    }

    [Fact]
    public async Task Schliessen_fuehrt_zur_Startseite_zurueck()
    {
        Assert.False(hf.SchliessenCommand.CanExecute(null));
        await OeffnenAsync();
        Assert.True(hf.SchliessenCommand.CanExecute(null));
        hf.EinstellungenCommand.Execute(null);

        await hf.SchliessenCommand.ExecuteAsync(null);

        Assert.False(hf.IstGeoeffnet);
        Assert.False(hf.ZeigtArbeitsbereich);
        Assert.Null(hf.Einrichtung);
        Assert.Equal(string.Empty, hf.Staffelname);
        Assert.Equal(UeberViewModel.Programm, hf.Titel);
        Assert.Equal(string.Empty, hf.AktiveAnsicht);
        Assert.False(hf.SchliessenCommand.CanExecute(null));
        Assert.False(hf.GenerierungCommand.CanExecute(null));
        Assert.Single(Ansichten<KostenAnsichtViewModel>());
        Assert.Empty(Ansichten<OptionenAnsichtViewModel>());
        Assert.DoesNotContain("Staffel schließen", o.Aufrufe);

        await OeffnenAsync();
        Assert.True(hf.IstGeoeffnet);
        Assert.Equal("Kosten", hf.AktiveAnsicht);
    }

    [Fact]
    public async Task Drucken_abbrechen_ohne_Plan_und_Fehler()
    {
        await OeffnenAsync();
        await hf.DruckenCommand.ExecuteAsync(null);
        o.Druckauswahl = a =>
        {
            a.Terminwuensche = false;
            a.Nachbartermine = false;
            a.Quelle = null;
            return true;
        };
        await hf.DruckenCommand.ExecuteAsync(null);
        o.Druckauswahl = _ => true;
        o.DateiZumSpeichern = null;
        await hf.DruckenCommand.ExecuteAsync(null);
        Assert.DoesNotContain("PDF", o.Aufrufe);
        Assert.Equal(3, o.Aufrufe.Count(a => a == "Druckauswahl"));

        o.DateiZumSpeichern = Path.Combine(ordner, "ohne.pdf");
        o.Druckauswahl = a =>
        {
            a.Quelle = null;
            return true;
        };
        await hf.DruckenCommand.ExecuteAsync(null);
        Druckdokument d = Assert.IsType<Druckdokument>(o.Ausdruck);
        Assert.Equal(["Terminwünsche", "Termine der Nachbarmannschaften"], d.Abschnitte.Select(a => a.Titel));
        Assert.Equal(string.Empty, d.Plan);
        Assert.Same(Seitenformat.A4Hoch, o.Format);

        o.Angezeigt.Clear();
        o.PdfFehler = new IOException("Datei gesperrt");
        await hf.DruckenCommand.ExecuteAsync(null);
        Assert.Contains("Drucken: Datei gesperrt", o.Meldungen);
        Assert.Empty(o.Angezeigt);
    }

    private static XDocument Xml(ZipArchive zip, string name)
    {
        using Stream strom = zip.GetEntry(name)!.Open();
        return XDocument.Load(strom);
    }

    private static void Sammeln<T>(IDockable dockable, List<T> liste)
        where T : class
    {
        if (dockable is T gesucht)
        {
            liste.Add(gesucht);
        }

        if (dockable is IDock dock && dock.VisibleDockables is { } kinder)
        {
            foreach (IDockable kind in kinder)
            {
                Sammeln(kind, liste);
            }
        }
    }

    /// <summary>Öffnet die Testdatei und schließt die Einrichtung ohne Änderungen ab.</summary>
    private async Task OeffnenAsync()
    {
        await hf.OeffnenCommand.ExecuteAsync(null);
        Assert.True(hf.IstGeoeffnet);
        if (hf.Einrichtung is EinrichtungViewModel einrichtung)
        {
            await einrichtung.AbschliessenCommand.ExecuteAsync(null);
        }

        Assert.True(hf.ZeigtArbeitsbereich);
    }

    private string[] Optionsdateien()
    {
        string basis = Path.Combine(ordner, "basis");
        return Directory.Exists(basis) ? Directory.GetFiles(basis, "*.options", SearchOption.AllDirectories) : [];
    }

    private List<T> Ansichten<T>()
        where T : class
    {
        var liste = new List<T>();
        Sammeln(hf.Layout, liste);
        return liste;
    }
}
