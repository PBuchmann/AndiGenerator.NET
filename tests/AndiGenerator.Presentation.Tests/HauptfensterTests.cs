// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Schranke;
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
    public async Task Einrichtung_bietet_die_gespeicherte_Kriterienreihenfolge_an_und_Standard_gilt_nur_fuer_diese_Sitzung()
    {
        string basis = Path.Combine(ordner, "basis");
        string eigene = Stufeneinteilung.Standard.Einstufen(Kostenkriterium.Hallenbelegung, Stufe.B).Text();
        Plansitzung.Oeffnen(datei, basis).KriterienstufenSpeichern(eigene);
        string halle = Planqualitaet.Name(Kostenkriterium.Hallenbelegung);

        await hf.OeffnenCommand.ExecuteAsync(null);
        EinrichtungViewModel einrichtung = hf.Einrichtung!;
        Einrichtungsschritt stufen = einrichtung.Schritte.Single(s => s.Titel == "Gespeicherte Kriterienreihenfolge");
        Assert.Contains(stufen.Details, d => d.Text == "B1 " + halle);
        stufen.WaehlenCommand.Execute(null);
        Assert.Same(stufen, einrichtung.Aktiv);
        stufen.Auswahl = 1;
        await einrichtung.UebernehmenCommand.ExecuteAsync(null);
        Assert.Equal(Schrittzustand.Erledigt, stufen.Zustand);
        await einrichtung.AbschliessenCommand.ExecuteAsync(null);

        // In dieser Sitzung gilt der Standard (Hallenbelegung wieder A3 nach den vereinsinternen Spielen), gespeichert bleibt die
        // eigene Einteilung.
        hf.QualitaetsansichtCommand.Execute(null);
        QualitaetAnsichtViewModel qualitaet = Assert.Single(Ansichten<QualitaetAnsichtViewModel>());
        qualitaet.Quelle = PlanQuelle.ClickTt;
        Assert.Equal(halle, qualitaet.Zeilen.First(z => z.Stufe == "A3").Name);
        Assert.Equal(eigene, Plansitzung.Oeffnen(datei, basis).Staffel.Kriterienstufen);

        // Beim nächsten Öffnen mit „Diese Reihenfolge verwenden“ gilt die eigene Einteilung.
        await hf.OeffnenCommand.ExecuteAsync(null);
        einrichtung = hf.Einrichtung!;
        stufen = einrichtung.Schritte.Single(s => s.Titel == "Gespeicherte Kriterienreihenfolge");
        stufen.WaehlenCommand.Execute(null);
        await einrichtung.UebernehmenCommand.ExecuteAsync(null);
        await einrichtung.AbschliessenCommand.ExecuteAsync(null);
        hf.QualitaetsansichtCommand.Execute(null);
        qualitaet = Assert.Single(Ansichten<QualitaetAnsichtViewModel>());
        qualitaet.Quelle = PlanQuelle.ClickTt;
        Assert.Equal(halle, qualitaet.Zeilen.First(z => z.Stufe == "B1").Name);
    }

    [Fact]
    public async Task Gewaehlter_Ausgangsplan_wird_vor_dem_Start_in_allen_Ansichten_und_Kacheln_gezeigt()
    {
        await OeffnenAsync();
        KostenAnsichtViewModel kosten = Assert.Single(Ansichten<KostenAnsichtViewModel>());
        Assert.Equal("–", hf.Anzeige.Kosten);

        hf.Startplan = hf.Startplaene.Single(p => p.Quelle == PlanQuelle.ClickTt);

        Assert.False(hf.Laeuft);
        Assert.Equal(PlanQuelle.ClickTt, kosten.Quelle);
        Assert.NotEqual("–", hf.Anzeige.Kosten);
        Assert.Equal("Ausgangsplan: " + PlanQuelle.ClickTt.Name, hf.Anzeige.KostenHinweis);

        // Eine danach geöffnete Ansicht zeigt ihn ebenfalls.
        hf.QualitaetsansichtCommand.Execute(null);
        Assert.Equal(PlanQuelle.ClickTt, Assert.Single(Ansichten<QualitaetAnsichtViewModel>()).Quelle);
    }

    /// <summary>Im Automodus nennt die Karte einer Insel alle Verstöße in A und B mit Stufe und ihre harten Fehler.</summary>
    /// <returns>Der Testlauf.</returns>
    [Fact]
    public async Task Leitstand_zeigt_Verstoesse_in_A_und_B_und_harte_Fehler_der_Inseln()
    {
        await OeffnenAsync();
        hf.LeitstandCommand.Execute(null);
        LeitstandAnsichtViewModel raum = Assert.Single(Ansichten<LeitstandAnsichtViewModel>());
        Inselstand[] inseln =
        [
            new(1, false, true, 900, 500_000, 50_000, 2, ["2"], new Stufenwert(3, 7, 44, 0, 347, 0), Kostentreiber:
            [
                new Kostenverursacher(Kostenkriterium.ParalleleSpiele, 1, 7, Stufe.A),
                new Kostenverursacher(Kostenkriterium.Sperrtermine, 0.5, 30, Stufe.B),
                new Kostenverursacher(Kostenkriterium.Spielverteilung, 0.3, 20, Stufe.C),
            ]),
            new(2, false, true, 1200, 400_000, 100_000, 0, ["2"], new Stufenwert(0, 10, 36, 0, 365, 0)),
        ];
        raum.Uebernehmen(new Optimierungsstand(true, false, 900_000, 5000, 900, 3, TimeSpan.Zero, null, [], null, new Maschinenraum([600_000, 300_000], inseln, 100_000, null, 0)));

        Assert.True(raum.Inseln[0].HatHarte);
        Assert.StartsWith("3 Spiele ohne gültigen Termin", raum.Inseln[0].Harte, StringComparison.Ordinal);
        Assert.False(raum.Inseln[1].HatHarte);
        Assert.Equal(["A · " + Planqualitaet.Name(Kostenkriterium.ParalleleSpiele), "B · " + Planqualitaet.Name(Kostenkriterium.Sperrtermine), "C · " + Planqualitaet.Name(Kostenkriterium.Spielverteilung)], raum.Inseln[0].Kostentreiber.Select(k => k.Name));
        Assert.Equal("7", raum.Inseln[0].Kostentreiber[0].Prozent);
    }

    [Fact]
    public async Task Leitstand_zeigt_Inseln_mit_bestem_Plan_und_Spezial_Insel()
    {
        await OeffnenAsync();
        hf.LeitstandCommand.Execute(null);
        LeitstandAnsichtViewModel raum = Assert.Single(Ansichten<LeitstandAnsichtViewModel>());
        Assert.Equal("Keine Generierung aktiv", raum.Ueberschrift);
        Assert.Empty(raum.Inseln);

        Inselstand[] inseln =
        [
            new(1, false, true, 900, 500_000, 50_000, 2, ["R", "15"], null, Kostentreiber: [new Kostenverursacher(Kostenkriterium.Sperrtermine, 0.5), new Kostenverursacher(Kostenkriterium.Spielverteilung, 0.25)]),
            new(2, false, true, 1200, 400_000, 100_000, 0, ["S1,25"]),
            new(0, true, false, -1, 0, 0, 0, ["2"]),
        ];
        var stand = new Optimierungsstand(true, false, 900_000, 5000, 900, 3, TimeSpan.Zero, null, [], null, new Maschinenraum([600_000, 300_000], inseln, 100_000, null, 0));
        raum.Uebernehmen(stand);

        Assert.StartsWith("2 Rechenkerne der Suche · 2 Inseln und Spezial-Insel", raum.Ueberschrift, StringComparison.Ordinal);
        Assert.Equal("Koordinator", raum.Schaubild.Lenkung);
        Assert.Equal(3, raum.Inseln.Count);
        Assert.True(raum.Inseln[0].IstBeste);
        Assert.False(raum.Inseln[1].IstBeste);
        Assert.Equal(50, raum.Inseln[0].Fortschritt);
        Assert.StartsWith("Seit 50.000 Plänen nichts Besseres – Neustart frühestens nach", raum.Inseln[0].FortschrittText, StringComparison.Ordinal);
        Assert.EndsWith("R · 15", raum.Inseln[0].Strategien, StringComparison.Ordinal);
        Assert.Equal(2, raum.Inseln[0].Kostentreiber.Count);
        Assert.Equal(Planqualitaet.Name(Kostenkriterium.Sperrtermine), raum.Inseln[0].Kostentreiber[0].Name);
        Assert.Equal(45, raum.Inseln[0].Kostentreiber[0].Breite);
        Assert.Equal("2 Neustarts", raum.Inseln[0].Neustarts);
        Assert.True(raum.Inseln[2].IstSpezial);
        Assert.StartsWith("Wartet", raum.Inseln[2].Hinweis, StringComparison.Ordinal);
        Assert.False(raum.Automodus);

        // Automodus: Verstöße statt Kosten, beste Insel nach Stufen, die geänderten Gewichte als Stufenleisten.
        Inselstand[] auto =
        [
            new(1, false, true, 900, 500_000, 50_000, 2, ["R"], new Stufenwert(0, 1, 4, 0, 30, 1000)),
            new(2, false, true, 800, 400_000, 10_000, 0, ["15"], new Stufenwert(0, 2, 0, 0, 10, 500)),
        ];
        var lenkung = new Lenkungsstand(
            null,
            new Stufenwert(0, 1, 4, 0, 30, 1000),
            null,
            "Drängen",
            "Optimierung Stufe A",
            1,
            0,
            Berechnungsoptionen.Standard,
            [],
            [Kostenkriterium.Sperrtermine],
            [new Gewichtsanhebung(Kostenkriterium.Sperrtermine, "TSV A", Gewichtung.Normal, Gewichtung.SehrHoch)]);
        raum.Uebernehmen(stand with { Lenkung = lenkung, Maschinenraum = stand.Maschinenraum! with { Inseln = auto } });

        Assert.True(raum.Automodus);
        Assert.True(raum.Inseln[0].ZeigtVerstoesse);
        Assert.Equal("1", raum.Inseln[0].VerstossA);
        Assert.True(raum.Inseln[0].IstBeste);
        Assert.False(raum.Inseln[1].IstBeste);
        Gewichtsgruppe gruppe = Assert.Single(raum.Gewichte);
        Assert.Equal(Planqualitaet.Name(Kostenkriterium.Sperrtermine), gruppe.Kriterium);
        Gewichtszeile zeile = Assert.Single(gruppe.Zeilen);
        Assert.Equal("TSV A", zeile.Wer);

        // Ring bei „normal“ (Stufe 3), Punkt bei „sehr hoch“ (Stufe 5), Linie dazwischen.
        Assert.Equal(2, zeile.Segmente.ToList().FindIndex(x => x.Vorher));
        Assert.Equal(4, zeile.Segmente.ToList().FindIndex(x => x.Jetzt));
        bool[] linie = [false, false, true, true, false, false];
        Assert.Equal(linie, zeile.Segmente.Select(x => x.RechtsWeg).ToArray());
        Assert.False(raum.HatVerfahren);

        // Mit Schranke: Text dazu, letzte Schritte; dieselbe Änderung bei vielen Mannschaften als eine Zeile.
        var highs = lenkung with
        {
            SchrankeA = 1,
            SchrankeB = 4,
            Aenderungen = ["1 s C DreiTageAbstand angehoben"],
            Gewichte = [.. new[] { "A", "B", "C", "D", "E" }.Select(t => new Gewichtsanhebung(Kostenkriterium.Sperrtermine, "TSV " + t, Gewichtung.Normal, Gewichtung.ExtremHoch))],
        };
        raum.Uebernehmen(stand with { Lenkung = highs, Maschinenraum = stand.Maschinenraum with { Inseln = auto } });
        Assert.Equal("5 Mannschaften", Assert.Single(Assert.Single(raum.Gewichte).Zeilen).Wer);
        Assert.Contains("Untere Schranke aus HiGHS", raum.Verfahren, StringComparison.Ordinal);
        Assert.Contains("A ≥ 1 (bester Plan 1 – bestmöglich), B ≥ 4 (bester Plan 4 – bestmöglich)", raum.Verfahren, StringComparison.Ordinal);
        Assert.Equal("1 s C DreiTageAbstand angehoben", Assert.Single(raum.Protokoll));

        // Teilneuplanung mit HiGHS: im Schaubild (oben rechts); ihr Anteil am Prozessor (kein eigener Kern) in der Leiste.
        Assert.False(raum.ZeigtTeilneuplanung);
        Assert.False(raum.Schaubild.ZeigtHighs);
        Teilneuplanungsstand teil = new("plant Ausschnitte neu", 9, 3, 2, 1, Rechnet: true, Mannschaften: 2, Verlauf: [Ausschnittsergebnis.Besser, Ausschnittsergebnis.Ausgangsplan]);
        var last = new Prozessorlast(0.80, 0.0625, 0.02, 16);
        Optimierungsstand mitTeil = stand with { Lenkung = highs, Maschinenraum = stand.Maschinenraum with { Inseln = auto }, Teilneuplanung = teil, Last = last };
        hf.Anzeige.Uebernehmen(mitTeil);
        raum.Uebernehmen(mitTeil);
        Assert.True(raum.ZeigtTeilneuplanung);
        Assert.Same(teil, raum.Schaubild.Teilneuplanung);
        Assert.True(raum.Schaubild.HighsRechnet);
        Assert.Equal("Automodus", raum.Schaubild.Lenkung);
        Assert.StartsWith("Stufe A", raum.Schaubild.LenkungText, StringComparison.Ordinal);
        Assert.Contains("HiGHS 6 % des Prozessors", raum.Ueberschrift, StringComparison.Ordinal);
        Assert.Equal("88 %", hf.Anzeige.Auslastung);
        Assert.Equal(["Suche", "HiGHS", "Rest", "frei"], hf.Anzeige.Prozessorzeilen.Select(z => z.Name));
        Assert.Equal("1,0 Kern", hf.Anzeige.Prozessorzeilen[1].Kerne);
        Assert.Equal("12,8 Kerne", hf.Anzeige.Prozessorzeilen[0].Kerne);
        Assert.Equal("16 Kerne", hf.Anzeige.SkalaEnde);
        Assert.Single(hf.Anzeige.Lastverlauf);

        // Untere Schranke: zeigt ihren Schritt im Schaubild.
        raum.HighsZeigen(new Schrankenfortschritt("A3 Parallele Spiele", 2, 5, null));
        Assert.NotNull(raum.Schaubild.Schranke);
        raum.HighsZeigen(null);
        Assert.Null(raum.Schaubild.Schranke);
        raum.Uebernehmen(stand);
        Assert.False(raum.HatVerfahren);
    }

    [Fact]
    public async Task Nach_dem_Start_zeigen_die_Ansichten_des_Ausgangsplans_die_laufende_Generierung()
    {
        await OeffnenAsync();
        KostenAnsichtViewModel kosten = Assert.Single(Ansichten<KostenAnsichtViewModel>());
        hf.Startplan = hf.Startplaene.Single(p => p.Quelle == PlanQuelle.ClickTt);
        hf.QualitaetsansichtCommand.Execute(null);
        QualitaetAnsichtViewModel qualitaet = Assert.Single(Ansichten<QualitaetAnsichtViewModel>());

        // Wie auf dem UI-Thread: Statusabfragen der laufenden Suche nicht gleichzeitig mit dem Test (sonst Wettlauf mit Stoppen).
        o.Sammeln = true;
        await hf.StartenCommand.ExecuteAsync(null);
        try
        {
            Assert.Equal(PlanQuelle.Laufend, kosten.Quelle);
            Assert.Equal(PlanQuelle.Laufend, qualitaet.Quelle);
        }
        finally
        {
            hf.StoppenCommand.Execute(null);
        }
    }

    [Fact]
    public async Task Einrichtung_abschliessen_zeigt_den_Arbeitsbereich_ohne_zu_generieren()
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

        await einrichtung.AbschliessenCommand.ExecuteAsync(null);
        Assert.False(hf.ZeigtEinrichtung);
        Assert.False(hf.Laeuft);
        Assert.True(hf.StartenCommand.CanExecute(null));
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

        await hf.GenerierungCommand.ExecuteAsync(null);
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

        // Klick auf eine Zeile mit Verstößen zeigt sie je Mannschaft rechts daneben, ein zweiter Klick schließt.
        Qualitaetszeile verletzt = qualitaet.Zeilen.First(z => z.Zustand != Qualitaetszustand.Erfuellt && z.Kriterium is Kostenkriterium k
            && k < Kostenkriterium.VereinsinterneSpieleAmAnfang && k != Kostenkriterium.Spielverteilung);
        qualitaet.Waehlen(verletzt);
        Assert.True(verletzt.IstGewaehlt);
        Assert.True(qualitaet.ZeigtDetails, verletzt.Name);
        Assert.StartsWith(verletzt.Stufe + " · ", qualitaet.DetailTitel, StringComparison.Ordinal);
        Assert.All(qualitaet.Details, d => Assert.False(string.IsNullOrEmpty(d.Name)));
        qualitaet.Waehlen(verletzt);
        Assert.False(verletzt.IstGewaehlt);
        Assert.False(qualitaet.ZeigtDetails);

        // Ein Kriterium ohne Verstöße bleibt gewählt, die rechte Tabelle aber zu.
        Qualitaetszeile erfuellt = qualitaet.Zeilen.First(z => z.Kriterium is not null && z.Zustand == Qualitaetszustand.Erfuellt);
        qualitaet.Waehlen(erfuellt);
        Assert.True(erfuellt.IstGewaehlt);
        Assert.False(qualitaet.ZeigtDetails);
        qualitaet.DetailsSchliessenCommand.Execute(null);
        Assert.False(erfuellt.IstGewaehlt);

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
    public async Task Verfahren_wird_vor_dem_Start_gewaehlt_und_laesst_sich_waehrend_der_Generierung_wechseln()
    {
        Assert.False(hf.AutomodusCommand.CanExecute(null));
        await OeffnenAsync();
        Assert.Equal("Start", hf.GenerierungText);

        // Der Umschalter wählt nur; gestartet wird mit dem Startknopf.
        hf.AutomodusCommand.Execute(null);
        Assert.False(hf.Laeuft);
        Assert.True(hf.Automodus);
        Assert.Equal("Start", hf.GenerierungText);
        Assert.True(hf.AutomodusGewaehlt);
        Assert.False(hf.KostenoptimierungGewaehlt);

        // Ein Klick auf das schon gewählte Verfahren lässt es gewählt.
        hf.AutomodusGewaehlt = false;
        Assert.True(hf.Automodus);
        Assert.StartsWith("Lenkt sich selbst", hf.VerfahrenErklaerung, StringComparison.Ordinal);

        await hf.GenerierungCommand.ExecuteAsync(null);
        Assert.True(hf.Laeuft);
        Assert.Single(Ansichten<QualitaetAnsichtViewModel>());
        Assert.Equal("Qualitaet", hf.AktiveAnsicht);
        Assert.Equal("Pausieren", hf.GenerierungText);
        Assert.StartsWith("Wechsel jederzeit", hf.VerfahrenErklaerung, StringComparison.Ordinal);

        hf.PauseCommand.Execute(null);
        Assert.True(hf.Pausiert);
        hf.KostenoptimierungCommand.Execute(null);
        Assert.True(hf.Laeuft);
        Assert.False(hf.Pausiert);
        Assert.False(hf.Automodus);
        Assert.Equal("Kosten", hf.AktiveAnsicht);

        hf.StoppenCommand.Execute(null);
        Assert.Equal("Start", hf.GenerierungText);

        // Wach gehalten, solange gerechnet wird – nicht in der Pause und nicht danach.
        Assert.Equal([true, false, true, false], o.Wach);
    }

    [Fact]
    public async Task Vor_einem_Neustart_wird_der_Plan_gesichert_und_danach_gefragt()
    {
        await OeffnenAsync();
        await PlanErrechnenAsync();
        hf.StoppenCommand.Execute(null);

        // Noch kein gesicherter Plan: Er wird gesichert, und eine Meldung sagt es.
        await hf.GenerierungCommand.ExecuteAsync(null);
        Assert.True(hf.Laeuft);
        Assert.Contains(o.Meldungen, m => m.Contains("gesichert", StringComparison.Ordinal));
        Assert.Contains(hf.Planquellen, q => q.Art == PlanQuellenArt.GesicherterPlan);

        // Schon ein gesicherter Plan: Es wird gefragt, mit beiden im Vergleich; „Abbrechen“ verhindert den Neustart.
        await PlanErrechnenAsync(weiter: true);
        hf.StoppenCommand.Execute(null);
        o.Sicherung = Sicherungswahl.Abbrechen;
        await hf.GenerierungCommand.ExecuteAsync(null);
        Assert.False(hf.Laeuft);
        Assert.Contains("Plan sichern", o.Aufrufe);
        Assert.NotNull(o.Vergleich);
        Assert.Equal(4, o.Vergleich.Zeilen.Count);
        Assert.NotEmpty(o.Vergleich.Empfehlung);

        // „Gesicherten Plan behalten“: Der Neustart verwirft den aktuellen Plan, ohne nochmals zu fragen.
        o.Sicherung = Sicherungswahl.Behalten;
        await hf.GenerierungCommand.ExecuteAsync(null);
        Assert.True(hf.Laeuft);
        hf.StoppenCommand.Execute(null);
    }

    [Fact]
    public async Task Nach_einem_unerwarteten_Fehler_wird_der_Plan_als_Notfallsicherung_gemerkt()
    {
        Assert.Null(hf.NotfallSichern());
        await OeffnenAsync();
        Assert.Null(hf.NotfallSichern());

        await PlanErrechnenAsync();
        hf.StoppenCommand.Execute(null);
        Assert.Equal(HauptfensterViewModel.NotfallsicherungName, hf.NotfallSichern());
        Assert.Single(Directory.EnumerateFiles(Path.Combine(ordner, "basis"), "*" + HauptfensterViewModel.NotfallsicherungName + "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Generierung_Pause_Plan_merken_exportieren_und_entfernen()
    {
        Assert.False(hf.GenerierungCommand.CanExecute(null));
        await OeffnenAsync();
        Assert.False(hf.PlanMerkenCommand.CanExecute(null));
        Assert.Equal("Start", hf.GenerierungText);
        Assert.True(hf.ZeigtStart);
        Assert.Equal("–", hf.Anzeige.Kosten);
        Assert.False(hf.CsvExportierenCommand.CanExecute(null));
        Assert.StartsWith("Kein Plan", hf.CsvHinweis, StringComparison.Ordinal);

        // Meldungen der Generierung kommen aus einem Hintergrund-Thread; wie auf dem UI-Thread laufen sie nur hier.
        o.Sammeln = true;
        await hf.GenerierungCommand.ExecuteAsync(null);
        Assert.True(hf.Laeuft);
        Assert.Equal("Pausieren", hf.GenerierungText);
        Assert.True(hf.ZeigtPause);
        Assert.True(hf.ZeigtStopp);
        for (int i = 0; i < 300 && !hf.PlanMerkenCommand.CanExecute(null); i++)
        {
            await Task.Delay(100);
            o.Abarbeiten();
        }

        Assert.True(hf.PlanMerkenCommand.CanExecute(null));
        Assert.NotEqual("–", hf.Anzeige.Pflicht);
        Assert.NotEmpty(hf.Anzeige.PflichtHinweis);
        hf.PauseCommand.Execute(null);
        Assert.True(hf.Pausiert);
        Assert.Equal("Fortsetzen", hf.GenerierungText);
        Assert.True(hf.ZeigtFortsetzen);
        Assert.False(hf.ZeigtStart);
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

        // Exportiert wird der angezeigte Plan: hier der Stand der Generierung in der Kostenansicht.
        Assert.True(hf.CsvExportierenCommand.CanExecute(null));
        Assert.EndsWith(PlanQuelle.Laufend.Name, hf.CsvHinweis, StringComparison.Ordinal);
        await hf.CsvExportierenCommand.ExecuteAsync(null);
        Assert.True(File.Exists(csv));

        // Zeigt die Ansicht den gemerkten Plan, wird dieser exportiert.
        KostenAnsichtViewModel angezeigt = Ansichten<KostenAnsichtViewModel>()[0];
        angezeigt.Quelle = gemerkt;
        Assert.EndsWith(gemerkt.Name, hf.CsvHinweis, StringComparison.Ordinal);
        string gemerktCsv = Path.Combine(ordner, "gemerkt.csv");
        o.DateiZumSpeichern = gemerktCsv;
        await hf.CsvExportierenCommand.ExecuteAsync(null);
        Assert.True(File.Exists(gemerktCsv));
        angezeigt.Quelle = PlanQuelle.Laufend;

        hf.StoppenCommand.Execute(null);
        Assert.False(hf.Laeuft);
        Assert.Contains(hf.Startplaene, p => p.Quelle == PlanQuelle.Laufend);
        Assert.Contains(hf.Startplaene, p => p.Quelle == gemerkt);

        hf.Startplan = hf.Startplaene.Single(p => p.Quelle == gemerkt);
        await hf.StartenCommand.ExecuteAsync(null);
        Assert.True(hf.Laeuft);
        hf.StoppenCommand.Execute(null);

        KostenAnsichtViewModel kosten = Ansichten<KostenAnsichtViewModel>()[0];
        kosten.Quelle = gemerkt;
        Assert.NotEmpty(kosten.Tabelle.Zeilen);

        // Gelöscht wird oben am Platz von „Plan merken“, wenn vor dem Start ein gemerkter Plan gewählt ist.
        Assert.True(hf.ZeigtPlanLoeschen);
        Assert.True(hf.PlanLoeschenCommand.CanExecute(null));
        await hf.PlanLoeschenCommand.ExecuteAsync(null);
        Assert.DoesNotContain(hf.Planquellen, q => q.Art == PlanQuellenArt.GemerkterPlan);
        Assert.False(hf.ZeigtPlanLoeschen);
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
        Assert.Contains("Spiele pro Woche", flaeche.Texte);
        Assert.Contains("frei (Sperrtermin)", flaeche.Texte);
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
    public async Task Oeffnen_beendet_die_Generierung_der_bisherigen_Staffel()
    {
        Assert.False(hf.StartplanWaehlbar);
        await OeffnenAsync();
        Assert.True(hf.StartplanWaehlbar);
        await hf.GenerierungCommand.ExecuteAsync(null);
        Assert.False(hf.StartplanWaehlbar);
        hf.PauseCommand.Execute(null);
        Assert.True(hf.Pausiert);
        Assert.False(hf.StartplanWaehlbar);
        Assert.Equal("Fortsetzen", hf.GenerierungText);

        await OeffnenAsync();

        Assert.False(hf.Laeuft);
        Assert.False(hf.Pausiert);
        Assert.Equal("Start", hf.GenerierungText);
        Assert.False(hf.StoppenCommand.CanExecute(null));
        Assert.True(hf.StartplanWaehlbar);
    }

    [Fact]
    public async Task Oeffnen_mit_passender_Mannschaftsmeldung_uebernimmt_sie_gleich()
    {
        string csv = await MannschaftsmeldungAsync(alle: true);
        o.DateienZumOeffnen = [datei, csv];

        await hf.OeffnenCommand.ExecuteAsync(null);

        Assert.True(hf.IstGeoeffnet);
        EinrichtungViewModel einrichtung = Assert.IsType<EinrichtungViewModel>(hf.Einrichtung);
        Assert.Equal(string.Empty, einrichtung.Meldung);
        Assert.EndsWith("Daten aus click-TT übernommen.", hf.Statuszeile, StringComparison.Ordinal);
        Assert.Contains(Path.GetFileName(csv), Zusatzseite(einrichtung).AufstellungsVermerk, StringComparison.Ordinal);

        // Erledigt ist der Punkt erst mit beiden Dateien; ein unlesbarer Bericht bleibt offen und wird genannt.
        Assert.False(einrichtung.Schritte[0].IstErledigt);
    }

    [Fact]
    public async Task Oeffnen_mit_unpassenden_Dateien_uebernimmt_sie_nicht_und_nennt_sie()
    {
        string csv = await MannschaftsmeldungAsync(alle: false);
        string pdf = Path.Combine(ordner, "daten", "Terminmeldungen.pdf");
        await File.WriteAllTextAsync(pdf, "kein PDF");
        o.DateienZumOeffnen = [pdf, datei, csv];

        await hf.OeffnenCommand.ExecuteAsync(null);

        Assert.True(hf.IstGeoeffnet);
        EinrichtungViewModel einrichtung = Assert.IsType<EinrichtungViewModel>(hf.Einrichtung);
        Assert.Contains("Mannschaftsmeldung (meldung.csv) passt nicht zu dieser Staffel – ohne Aufstellung:", einrichtung.Meldung, StringComparison.Ordinal);
        Assert.Contains("Bericht „Terminwünsche“ (Terminmeldungen.pdf)", einrichtung.Meldung, StringComparison.Ordinal);
        Assert.Equal(string.Empty, Zusatzseite(einrichtung).AufstellungsVermerk);
        Assert.Equal(string.Empty, Zusatzseite(einrichtung).TerminwunschVermerk);
    }

    [Fact]
    public async Task Erkannte_Kompaktklasse_ist_in_der_Einrichtung_vorgewaehlt()
    {
        string kompakt = Path.Combine(ordner, "daten", "Jugend_19_Testliga_(HR).xml");
        File.Copy(Path.Combine(Testdaten.Referenz, "..", "kompakt", "Jugend_19_Testliga_(HR).xml"), kompakt);
        o.DateiZumOeffnen = kompakt;

        await hf.OeffnenCommand.ExecuteAsync(null);

        EinrichtungViewModel einrichtung = Assert.IsType<EinrichtungViewModel>(hf.Einrichtung);
        Einrichtungsschritt schritt = einrichtung.Schritte.Single(s => s.Titel == "Klasse mit Kompaktspieltagen");
        Assert.Equal(0, schritt.Auswahl);
        Assert.Equal("Mit Kompaktspieltagen planen", schritt.Optionen[0].Titel);

        // Gilt wie angeklickt: Abschließen übernimmt die Wahl auch ohne Klick.
        Assert.True(schritt.IstGewaehlt);
    }

    /// <summary>Klick auf eine Mannschaft über dem Raster: Ihre Halle lässt sich für alle Heimspiele umstellen.</summary>
    /// <returns>Der Testlauf.</returns>
    [Fact]
    public async Task Kompaktspieltage_Halle_fuer_alle_Heimspiele_einer_Mannschaft()
    {
        string kompakt = Path.Combine(ordner, "daten", "Jugend_19_Testliga_(HR).xml");
        File.Copy(Path.Combine(Testdaten.Referenz, "..", "kompakt", "Jugend_19_Testliga_(HR).xml"), kompakt);
        Plansitzung vorher = Plansitzung.Oeffnen(kompakt, Path.Combine(ordner, "basis-kompakt"));
        Datenbearbeitung b = vorher.DatenBearbeiten();
        b.KompaktspieltageSetzen(Kompaktmodus.Bestaetigt);
        vorher.DatenUebernehmen(b);
        o.DateiZumOeffnen = kompakt;
        await hf.OeffnenCommand.ExecuteAsync(null);
        if (hf.Einrichtung is { } einrichtung)
        {
            await einrichtung.AbschliessenCommand.ExecuteAsync(null);
        }

        hf.KompaktspieltageCommand.Execute(null);
        KompaktspieltageAnsichtViewModel ansicht = Assert.Single(Ansichten<KompaktspieltageAnsichtViewModel>());
        Assert.True(ansicht.Eingeschaltet);

        // Ohne Plan keine Spieltage: Die Legende zeigt keine ihrer Farben.
        Assert.False(ansicht.LegendeZweiter || ansicht.LegendeDritter || ansicht.LegendeEinzel);
        string team = ansicht.Spalten[0].Name;
        string anderes = ansicht.Spalten[1].Name;
        Assert.False(ansicht.ZeigtHalle);

        ansicht.MannschaftWaehlenCommand.Execute(team);
        Assert.True(ansicht.ZeigtHalle);
        Assert.Equal("Halle aller Heimspiele von " + team, ansicht.HalleTitel);
        Assert.NotEmpty(ansicht.HalleLokale);

        // Eine andere Halle als die Standardhalle wählen (gibt es nur eine, die erste).
        Kompaktlokal neu = ansicht.HalleLokale.FirstOrDefault(l => l != ansicht.HalleLokal) ?? ansicht.HalleLokale[0];
        ansicht.HalleLokal = neu;
        await ansicht.HalleFuerAlleCommand.ExecuteAsync(null);
        Assert.StartsWith($"„{neu}“ gilt jetzt für alle Heimtermine von {team}", ansicht.HalleMeldung, StringComparison.Ordinal);

        // Neu gewählt ist sie die Standardhalle der Mannschaft.
        ansicht.MannschaftWaehlenCommand.Execute(anderes);
        ansicht.MannschaftWaehlenCommand.Execute(team);
        Assert.Equal(neu.Nummer, ansicht.HalleLokal?.Nummer);

        ansicht.MannschaftWaehlenCommand.Execute(team);
        Assert.False(ansicht.ZeigtHalle);
    }

    [Fact]
    public async Task Oeffnen_mit_zwei_XML_Dateien_wird_abgelehnt()
    {
        string zweite = Path.Combine(ordner, "daten", "zweite.xml");
        File.Copy(datei, zweite);
        o.DateienZumOeffnen = [datei, zweite];

        await hf.OeffnenCommand.ExecuteAsync(null);

        Assert.False(hf.IstGeoeffnet);
        Assert.Contains("Staffel öffnen: Bitte nur eine XML-Datei auswählen – je Staffel eine.", o.Meldungen);
    }

    [Fact]
    public async Task Abbrechen_der_Einrichtung_schliesst_die_Staffel_und_Oeffnen_ist_solange_gesperrt()
    {
        await hf.OeffnenCommand.ExecuteAsync(null);
        EinrichtungViewModel einrichtung = Assert.IsType<EinrichtungViewModel>(hf.Einrichtung);
        Assert.Equal("Daten aus click-TT ergänzen", einrichtung.Schritte[0].Titel);
        Assert.False(hf.OeffnenCommand.CanExecute(null));
        Assert.False(hf.NeuCommand.CanExecute(null));

        await einrichtung.AbbrechenCommand.ExecuteAsync(null);

        Assert.False(hf.IstGeoeffnet);
        Assert.Null(hf.Einrichtung);
        Assert.True(hf.OeffnenCommand.CanExecute(null));
        Assert.True(hf.NeuCommand.CanExecute(null));
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
    /// <summary>Die Seite „Daten aus click-TT“ des ersten Einrichtungspunkts.</summary>
    private static ZusatzdatenSeiteViewModel Zusatzseite(EinrichtungViewModel einrichtung)
    {
        einrichtung.Aktiv = einrichtung.Schritte[0];
        return Assert.IsType<ZusatzdatenSeiteViewModel>(Assert.Single(einrichtung.Schritte[0].Seite!.Seiten));
    }

    /// <summary>Schreibt eine Mannschaftsmeldung (CSV) für alle Mannschaften der Staffel oder nur für die erste.</summary>
    private async Task<string> MannschaftsmeldungAsync(bool alle)
    {
        Plansitzung lesen = Plansitzung.Oeffnen(datei, Path.Combine(ordner, "basis-namen"), null);
        IEnumerable<string> namen = lesen.Staffel.Mannschaften.Select(m => m.Name).Take(alle ? int.MaxValue : 1);
        string csv = Path.Combine(ordner, "daten", "meldung.csv");
        string zeilen = string.Concat(namen.SelectMany(n => Enumerable.Range(1, 4).Select(i => $"{n};{Persistence.Aufstellungen.MannschaftsaufstellungenCsv.Nummer(n)};{i};{1500 + i};\n")));
        await File.WriteAllTextAsync(csv, "MannschaftName;SpielerMannschaftNr;SpielerRang;TTR;SpielerStatus\n" + zeilen);
        return csv;
    }

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

    /// <summary>
    /// Startet die Generierung bzw. lässt sie weiterlaufen, bis sie einen Plan gemeldet hat; mit <paramref name="weiter"/>
    /// zusätzlich bis zu einer weiteren Verbesserung (damit sich der Plan von einem früh gesicherten unterscheidet).
    /// </summary>
    private async Task PlanErrechnenAsync(bool weiter = false)
    {
        o.Sammeln = true;
        if (!hf.Laeuft)
        {
            await hf.GenerierungCommand.ExecuteAsync(null);
        }

        for (int i = 0; i < 300 && !hf.PlanMerkenCommand.CanExecute(null); i++)
        {
            await Task.Delay(100);
            o.Abarbeiten();
        }

        string verbesserungen = hf.Anzeige.Verbesserungen;
        for (int i = 0; weiter && i < 300 && hf.Anzeige.Verbesserungen == verbesserungen; i++)
        {
            await Task.Delay(100);
            o.Abarbeiten();
        }

        Assert.True(hf.PlanMerkenCommand.CanExecute(null));
    }
}
