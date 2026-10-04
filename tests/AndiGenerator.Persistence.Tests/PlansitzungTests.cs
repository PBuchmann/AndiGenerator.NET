// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Persistence.Gemeinsam;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

public sealed class PlansitzungTests : IDisposable
{
    private const string R2 = "R2_4__Kreisklasse_Gruppe_A (2)";
    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-test-" + Guid.NewGuid().ToString("N"));

    public PlansitzungTests()
    {
        Directory.CreateDirectory(Path.Combine(ordner, "daten"));
        Directory.CreateDirectory(Path.Combine(ordner, "basis"));
    }

    public void Dispose() => Directory.Delete(ordner, recursive: true);

    [Fact]
    public void Fruehere_Daten_werden_einmalig_und_ohne_Ueberschreiben_uebernommen()
    {
        string frueher = Path.Combine(ordner, Plansitzung.FruehererOrdnername);
        string basis = Path.Combine(ordner, Plansitzung.Ordnername);
        string staffel = Path.Combine("Andi-Generator", "Staffel 2026");
        Directory.CreateDirectory(Path.Combine(frueher, staffel));
        Directory.CreateDirectory(Path.Combine(frueher, "artifacts"));
        File.WriteAllText(Path.Combine(frueher, staffel, "AndiGenerator.options"), "alt");
        File.WriteAllText(Path.Combine(frueher, staffel, "Plan A.xml"), "plan");
        File.WriteAllText(Path.Combine(frueher, "artifacts", "x.dll"), "build");
        Directory.CreateDirectory(Path.Combine(basis, staffel));
        File.WriteAllText(Path.Combine(basis, staffel, "AndiGenerator.options"), "neu");

        Assert.False(Plansitzung.FruehereDatenUebernehmen(Path.Combine(ordner, "gibt-es-nicht"), basis));
        Assert.True(Plansitzung.FruehereDatenUebernehmen(frueher, basis));

        Assert.Equal("neu", File.ReadAllText(Path.Combine(basis, staffel, "AndiGenerator.options")));
        Assert.Equal("plan", File.ReadAllText(Path.Combine(basis, staffel, "Plan A.xml")));
        Assert.False(Directory.Exists(Path.Combine(basis, "artifacts")));
        Assert.True(File.Exists(Path.Combine(frueher, staffel, "Plan A.xml")));

        // Ein danach gelöschter Plan kommt nicht wieder.
        File.Delete(Path.Combine(basis, staffel, "Plan A.xml"));
        Assert.False(Plansitzung.FruehereDatenUebernehmen(frueher, basis));
        Assert.False(File.Exists(Path.Combine(basis, staffel, "Plan A.xml")));
        Assert.EndsWith(Plansitzung.Ordnername, Plansitzung.EigeneBasis(), StringComparison.Ordinal);
    }

    [Fact]
    public void Oeffnen_liest_click_tt_mit_Aenderungen()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: true);

        Assert.True(sitzung.IstClickTtDatei);
        Assert.False(sitzung.ErsterStart);
        Assert.Equal(7, sitzung.Staffel.Mannschaften.Count);
        Assert.StartsWith(Path.Combine(ordner, "basis"), sitzung.Staffelordner, StringComparison.Ordinal);
        Assert.False(sitzung.OptionenWeichenVomStandardAb);
        Assert.False(Directory.Exists(sitzung.Staffelordner));
    }

    [Fact]
    public void Ohne_Aenderungsdatei_ist_es_der_erste_Start()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: false);

        Assert.True(sitzung.ErsterStart);
        Assert.True(sitzung.IstClickTtDatei);
    }

    [Fact]
    public void Optionen_werden_gespeichert_und_Standard_fuer_die_Sitzung_ueberschreibt_nichts()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: true);
        sitzung.OptionenSpeichern(Berechnungsoptionen.Standard with { Doppelrunde = true });

        Plansitzung wieder = Oeffnen(mitAenderungen: true);
        Assert.True(wieder.Optionen.Doppelrunde);
        Assert.True(wieder.OptionenWeichenVomStandardAb);

        wieder.StandardoptionenFuerDieseSitzung();
        Assert.False(wieder.Optionen.Doppelrunde);
        Assert.True(Oeffnen(mitAenderungen: true).Optionen.Doppelrunde);
    }

    [Fact]
    public void Gemerkte_Plaene_merken_laden_entfernen()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: true);
        List<Spiel> plan = sitzung.Staffel.Mannschaften
            .SelectMany(h => sitzung.Staffel.Mannschaften.Where(g => g.Name != h.Name).Select(g => new Spiel(null, h.Name, g.Name, string.Empty)))
            .ToList();
        plan[0] = plan[0] with { Zeitpunkt = new DateTime(2026, 9, 11, 20, 0, 0, DateTimeKind.Unspecified) };

        GemerkterPlanEintrag eintrag = sitzung.PlanMerken("Test / Entwurf", plan);

        GemerkterPlanEintrag gefunden = Assert.Single(sitzung.GemerktePlaene());
        Assert.Equal("Test / Entwurf", gefunden.Name);
        IReadOnlyList<Spiel> geladen = sitzung.GemerktenPlanLaden(eintrag);
        Assert.Equal(plan.OrderBy(Schluessel), geladen.OrderBy(Schluessel));

        sitzung.GemerktenPlanEntfernen(gefunden);
        Assert.Empty(sitzung.GemerktePlaene());
    }

    [Fact]
    public void Csv_Export_ist_byte_gleich_zum_Original()
    {
        Plansitzung sitzung = Plansitzung.Oeffnen(Testdaten.Datei("4__Kreisklasse_Gruppe_A (2).xml"), Path.Combine(ordner, "basis"));
        byte[] original = File.ReadAllBytes(Testdaten.Datei("4. Kreisklasse Gruppe A.csv"));
        List<Spiel> spiele = CsvSpiele(original);

        string ziel = Path.Combine(ordner, "export.csv");
        sitzung.CsvExportieren(ziel, spiele);

        Assert.Equal(DelphiKompatibel.Windows1252.GetString(original), DelphiKompatibel.Windows1252.GetString(File.ReadAllBytes(ziel)));
        Assert.Empty(sitzung.HarteFehler(spiele));
    }

    [Fact]
    public void Harte_Fehler_melden_nicht_terminierte_Spiele()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: true);
        List<Spiel> leer = sitzung.Staffel.Mannschaften
            .SelectMany(h => sitzung.Staffel.Mannschaften.Where(g => g.Name != h.Name).Select(g => new Spiel(null, h.Name, g.Name, string.Empty)))
            .ToList();

        string meldung = Assert.Single(sitzung.HarteFehler(leer));
        Assert.Equal($"{leer.Count} Spiel(e) ohne Termin", meldung);
    }

    [Fact]
    public void Rundenvorschlag_Rueckrunde_nur_kurz_vor_ihrem_Beginn()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: true);
        DateOnly rueckrunde = sitzung.Staffel.Rueckrundenbeginn!.Value;

        Assert.Equal(Rundenvorschlag.NurRueckrunde, sitzung.RundeVorschlagen(rueckrunde.AddDays(-30)));
        Assert.Equal(Rundenvorschlag.Keiner, sitzung.RundeVorschlagen(rueckrunde.AddDays(-120)));
    }

    [Fact]
    public void Meldungen_je_Kostenart_ergeben_zusammen_die_Meldungen_der_Mannschaft()
    {
        Plansitzung sitzung = Plansitzung.Oeffnen(Testdaten.Datei("4__Kreisklasse_Gruppe_A (2).xml"), Path.Combine(ordner, "basis"));
        List<Spiel> spiele = CsvSpiele(File.ReadAllBytes(Testdaten.Datei("4. Kreisklasse Gruppe A.csv")));
        Planbewertung bewertung = sitzung.Bewerten(spiele);

        foreach (Mannschaftsbewertung mannschaft in bewertung.Mannschaften)
        {
            List<string> jeKostenart = Enum.GetValues<MannschaftsKostenart>()
                .SelectMany(art => sitzung.Meldungen(spiele, mannschaft.Name, art))
                .ToList();
            Assert.Equal(mannschaft.Meldungen, jeKostenart);
        }

        Assert.Contains(bewertung.Mannschaften, m => m.Meldungen.Count > 0);
        Assert.Empty(sitzung.Meldungen(spiele, "gibt es nicht", MannschaftsKostenart.Sperrtermine));
    }

    [Fact]
    public void Gewichtung_aendern_speichert_die_Optionen()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: true);
        sitzung.GewichtungAendern(new KostenartGewichtungsziel(MannschaftsKostenart.Sperrtermine), Gewichtung.ExtremHoch);

        Assert.Equal(Gewichtung.ExtremHoch, sitzung.Optionen.Fuer(MannschaftsKostenart.Sperrtermine));
        Assert.Equal(Gewichtung.ExtremHoch, Oeffnen(mitAenderungen: true).Optionen.Fuer(MannschaftsKostenart.Sperrtermine));
    }

    [Fact]
    public void Erstes_Oeffnen_kopiert_Optionen_und_Plaene_aus_dem_Original_ohne_es_zu_aendern()
    {
        Plansitzung original = Oeffnen(mitAenderungen: true);
        original.OptionenSpeichern(Berechnungsoptionen.Standard with { Doppelrunde = true });
        original.PlanMerken("Aus dem Original", [new Spiel(null, original.Staffel.Mannschaften[0].Name, original.Staffel.Mannschaften[1].Name, string.Empty)]);
        string originalOptionen = File.ReadAllText(Path.Combine(original.Staffelordner, "AndiGenerator.options"));

        string neueBasis = Path.Combine(ordner, "neu");
        Plansitzung neu = Plansitzung.Oeffnen(original.Pfad, neueBasis, Path.Combine(ordner, "basis"));

        Assert.Equal(original.Staffelordner, neu.UebernommenAus);
        Assert.StartsWith(neueBasis, neu.Staffelordner, StringComparison.Ordinal);
        Assert.True(neu.Optionen.Doppelrunde);
        Assert.Equal("Aus dem Original", Assert.Single(neu.GemerktePlaene()).Name);

        neu.OptionenSpeichern(Berechnungsoptionen.Standard);
        Assert.Equal(originalOptionen, File.ReadAllText(Path.Combine(original.Staffelordner, "AndiGenerator.options")));
        Assert.Null(Plansitzung.Oeffnen(original.Pfad, neueBasis, Path.Combine(ordner, "basis")).UebernommenAus);
    }

    [Fact]
    public void Automatische_Rundenmitten_liegen_montags_in_Hin_und_Rueckrunde()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: true);
        DateOnly beginn = sitzung.Staffel.Beginn!.Value;
        DateOnly rueckrunde = sitzung.Staffel.Rueckrundenbeginn!.Value;

        (DateOnly mitte1, DateOnly mitte2) = sitzung.AutomatischeRundenmitten();

        Assert.Equal(DayOfWeek.Monday, mitte1.DayOfWeek);
        Assert.Equal(DayOfWeek.Monday, mitte2.DayOfWeek);
        Assert.InRange(mitte1, beginn, rueckrunde);
        Assert.True(mitte2 > rueckrunde);
    }

    [Fact]
    public void Rundenvorschlag_wird_uebernommen_und_gespeichert()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: true);
        sitzung.RundeUebernehmen(Rundenvorschlag.Keiner);
        Assert.Equal(Rundenplanung.Beide, sitzung.Optionen.Rundenplanung);

        sitzung.RundeUebernehmen(Rundenvorschlag.NurRueckrunde);

        Assert.Equal(Rundenplanung.NurRueckrunde, Oeffnen(mitAenderungen: true).Optionen.Rundenplanung);
    }

    [Fact]
    public void Erststart_nennt_die_Pflichtspieltage_der_Staffel()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: false);

        Assert.True(sitzung.ErsterStart);
        Assert.Contains(sitzung.Erststarthinweise(), h => h.Titel == "Pflichtspieltage");
        Assert.Empty(sitzung.AbweichungenVomStandard());
    }

    [Fact]
    public void Terminplan_enthaelt_alle_Spiele_nach_Termin_mit_Hinweisen()
    {
        Plansitzung sitzung = Plansitzung.Oeffnen(Testdaten.Datei("4__Kreisklasse_Gruppe_A (2).xml"), Path.Combine(ordner, "basis"));
        List<Spiel> spiele = CsvSpiele(File.ReadAllBytes(Testdaten.Datei("4. Kreisklasse Gruppe A.csv")));

        IReadOnlyList<Terminplanzeile> plan = sitzung.Terminplan(spiele);

        Assert.Equal(spiele.Count, plan.Count);
        Assert.All(plan, z => Assert.NotNull(z.Zeitpunkt));
        Assert.Equal(plan.OrderBy(z => z.Zeitpunkt).Select(z => z.Zeitpunkt), plan.Select(z => z.Zeitpunkt));
        Assert.DoesNotContain(plan, z => z.Hinweis.Contains("ungültiger Termin", StringComparison.Ordinal));
        Assert.DoesNotContain(plan, z => z.Hinweis.Contains("doppeltes Spiel", StringComparison.Ordinal));
        Assert.All(plan, z => Assert.InRange(z.Kalenderwoche, 1, 53));
    }

    [Theory]
    [InlineData("TTC Beispiel III", "TTC Beispiel")]
    [InlineData("Qvqm Bmno VII", "Qvqm Bmno")]
    [InlineData("Vluy Wwae", "Vluy Wwae")]
    [InlineData("Ujuu Hpom XXIV", "Ujuu Hpom")]
    public void Vereinsname_ohne_Mannschaftsnummer(string mannschaft, string verein)
    {
        Assert.Equal(verein, Terminplanauswertung.OhneMannschaftsnummer(mannschaft));
        Assert.True(Terminplanauswertung.GleicherVerein(mannschaft, verein + " II"));
    }

    [Fact]
    public void Terminwuensche_je_Mannschaft_mit_Auswertung()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: false);

        Terminwunschuebersicht uebersicht = sitzung.Terminwuensche();

        Assert.Equal(sitzung.Staffel.Mannschaften.Select(m => m.Name).Order(StringComparer.Ordinal), uebersicht.Mannschaften.Select(m => m.Name).Order(StringComparer.Ordinal));
        Assert.NotNull(uebersicht.Erster);
        Assert.True(uebersicht.Letzter >= uebersicht.Erster);
        Assert.Contains(uebersicht.Mannschaften, m => m.Marken.Count > 0);
        Assert.All(uebersicht.Mannschaften, m => Assert.Contains(m.Zeilen, z => z.Ueberschrift && z.Text == "Auswertung:"));
        Assert.All(uebersicht.Mannschaften, m => Assert.Contains(m.Zeilen, z => z.Farbe is Terminwunschfarbe.Rot or Terminwunschfarbe.Gruen));
    }

    [Fact]
    public void Nachbartermine_je_Mannschaft_nach_Tagen()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: false);

        IReadOnlyList<MannschaftsNachbartermine> nachbarn = sitzung.Nachbartermine();

        Assert.Equal(sitzung.Staffel.Mannschaften.Count, nachbarn.Count);
        Assert.Contains(nachbarn, m => m.Tage.Count > 0);
        Assert.All(nachbarn.SelectMany(m => m.Tage), tag =>
        {
            Assert.NotEmpty(tag);
            Assert.All(tag, n => Assert.Equal(tag[0].Zeitpunkt.Date, n.Zeitpunkt.Date));
            Assert.Equal(tag.OrderBy(n => n.Zeitpunkt).Select(n => n.Zeitpunkt), tag.Select(n => n.Zeitpunkt));
        });
        Assert.All(nachbarn, m => Assert.Equal(m.Tage.OrderBy(t => t[0].Zeitpunkt).Select(t => t[0].Zeitpunkt), m.Tage.Select(t => t[0].Zeitpunkt)));
    }

    [Fact]
    public void Diagramme_des_Plans()
    {
        Plansitzung sitzung = Plansitzung.Oeffnen(Testdaten.Datei("4__Kreisklasse_Gruppe_A (2).xml"), Path.Combine(ordner, "basis"));
        List<Spiel> spiele = CsvSpiele(File.ReadAllBytes(Testdaten.Datei("4. Kreisklasse Gruppe A.csv")));

        Diagrammdaten daten = sitzung.Diagramme(spiele);

        Assert.True(daten.HatSpiele);
        Assert.True(daten.Erster <= daten.Letzter);
        Assert.Equal(sitzung.Staffel.Mannschaften.Count, daten.Mannschaften.Count);
        Assert.All(daten.Mannschaften, m =>
        {
            Assert.Equal(spiele.Count(s => s.Heim == m.Name || s.Gast == m.Name), m.Spiele.Count(s => s.Zeitpunkt is not null));
            Assert.Equal(m.Spiele.Select(s => s.Zeitpunkt), m.Spiele.OrderBy(s => s.Zeitpunkt).Select(s => s.Zeitpunkt));
            Assert.All(m.Abstaende.SelectMany(z => z), l => Assert.True(l.Von <= l.Bis));
        });
        Assert.Equal(daten.MitRueckrunde ? 2 : 1, daten.Wochen.Count);
        Assert.All(daten.Wochen.SelectMany(w => w.Montage), t => Assert.Equal(DayOfWeek.Monday, t.DayOfWeek));
        Assert.All(daten.Wochen, w => Assert.Equal(w.Montage.Count, w.MaxDifferenz.Count));
    }

    [Theory]
    [InlineData(4, 4.0, 0, 255)]
    [InlineData(12, 4.0, 255, 0)]
    [InlineData(2, 4.0, 127, 255)]
    [InlineData(3, 4.0, 63, 255)]
    public void Abstandsfarbe_wie_im_Original(int abstand, double ideal, int rot, int gruen)
    {
        Assert.Equal((rot, gruen), Diagrammauswertung.Abstandsfarbe(abstand, ideal));
    }

    [Fact]
    public void Montag_davor_wie_im_Original()
    {
        double mittwoch = DelphiDatum.Wert(new DateOnly(2026, 9, 30));
        Assert.Equal(DelphiDatum.Wert(new DateOnly(2026, 9, 28)), Diagrammauswertung.MontagDavor(mittwoch + 0.75));
        Assert.Equal(DelphiDatum.Wert(new DateOnly(2026, 9, 28)), Diagrammauswertung.MontagDavor(DelphiDatum.Wert(new DateOnly(2026, 9, 28))));
    }

    [Fact]
    public void Daten_uebernehmen_ohne_Aenderung_schreibt_dieselben_Modifikationen()
    {
        // R2s .modifications wurde gegen einen älteren Exportstand gespeichert (ohne bestehenden Spielplan), daher ein
        // Paar, dessen Differenz die Datei exakt ergibt (siehe PlanDatenTests).
        const string Name = "3__Kreisklasse (11)";
        string ziel = Path.Combine(ordner, "daten", Name + ".xml");
        File.Copy(Testdaten.Datei(Name + ".xml"), ziel, overwrite: true);
        File.Copy(Testdaten.Datei(Name + ".modifications"), Plansitzung.ModifikationenPfad(ziel), overwrite: true);
        Plansitzung sitzung = Plansitzung.Oeffnen(ziel, Path.Combine(ordner, "basis"));
        string datei = Plansitzung.ModifikationenPfad(sitzung.Pfad);
        string vorher = PlanDatenDatei.Laden(datei).Kanonisch();

        Datenbearbeitung bearbeitung = sitzung.DatenBearbeiten();
        Ligadaten gelesen = bearbeitung.Ligadaten;
        bearbeitung.Ligadaten = gelesen;
        sitzung.DatenUebernehmen(bearbeitung);

        Assert.True(bearbeitung.HatStandard);
        Assert.Equal(vorher, PlanDatenDatei.Laden(datei).Kanonisch());
    }

    [Fact]
    public void Einteilung_der_Kriterien_steht_als_Attribut_am_Plan_in_den_Modifikationen()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: true);
        Assert.Equal(string.Empty, sitzung.Staffel.Kriterienstufen);
        const string Einteilung = "A:Sperrtermine,Hallenbelegung;B:ParalleleSpiele;C:Spielverteilung";

        sitzung.KriterienstufenSpeichern(Einteilung);

        Assert.Equal(Einteilung, sitzung.Staffel.Kriterienstufen);
        DatenKnoten modifikationen = PlanDatenDatei.Laden(Plansitzung.ModifikationenPfad(sitzung.Pfad));
        Assert.Equal(Einteilung, modifikationen.Lesen(StaffelAbbildung.KriterienAttribut));
        Assert.Empty(modifikationen.KinderMitNamen(StaffelAbbildung.KriterienAttribut));
        Plansitzung wieder = Plansitzung.Oeffnen(sitzung.Pfad, Path.Combine(ordner, "basis"));
        Assert.Equal(Einteilung, wieder.Staffel.Kriterienstufen);

        // Einrichtung „Standardreihenfolge verwenden“: nur für diese Sitzung, auch nach anderen Datenänderungen; gespeichert bleibt sie.
        wieder.StandardKriterienstufenFuerDieseSitzung();
        Assert.Empty(wieder.Staffel.Kriterienstufen);
        wieder.DatenUebernehmen(wieder.DatenBearbeiten());
        Assert.Empty(wieder.Staffel.Kriterienstufen);
        Assert.Equal(Einteilung, Plansitzung.Oeffnen(sitzung.Pfad, Path.Combine(ordner, "basis")).Staffel.Kriterienstufen);
        wieder.KriterienstufenSpeichern(Einteilung);
        Assert.Equal(Einteilung, wieder.Staffel.Kriterienstufen);
    }

    [Fact]
    public void Geaenderte_Ligadaten_werden_gespeichert_und_gelten_sofort()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: true);
        string staffelordner = sitzung.Staffelordner;
        Datenbearbeitung bearbeitung = sitzung.DatenBearbeiten();
        Ligadaten alt = bearbeitung.Ligadaten;

        bearbeitung.Ligadaten = alt with { Name = "Testliga", Ende = alt.Ende.AddDays(7) };
        sitzung.DatenUebernehmen(bearbeitung);

        Assert.Equal("Testliga", sitzung.Staffel.Name);
        Assert.Equal(staffelordner, sitzung.Staffelordner);
        Assert.NotEqual("Testliga", sitzung.DatenBearbeiten().StandardLigadaten?.Name);
        Plansitzung wieder = Plansitzung.Oeffnen(sitzung.Pfad, Path.Combine(ordner, "basis"));
        Assert.Equal("Testliga", wieder.Staffel.Name);
        Assert.Equal(alt.Ende.AddDays(7), wieder.Staffel.Ende);
    }

    [Fact]
    public void Abbruch_der_Bearbeitung_aendert_nichts()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: true);
        string name = sitzung.Staffel.Name;

        Datenbearbeitung bearbeitung = sitzung.DatenBearbeiten();
        bearbeitung.Ligadaten = bearbeitung.Ligadaten with { Name = "Verworfen" };

        Assert.Equal(name, sitzung.Staffel.Name);
        Assert.Equal(name, sitzung.DatenBearbeiten().Ligadaten.Name);
    }

    [Theory]
    [InlineData("", "1", "Herren", "Der Liganame darf nicht leer sein")]
    [InlineData("Liga", " ", "Herren", "Die Liganummer darf nicht leer sein")]
    [InlineData("Liga", "1", "", "Die Art (Herren/Damen) darf nicht leer sein")]
    [InlineData("Liga", "1", "Herren", null)]
    public void Ligadaten_pruefen_wie_im_Original(string name, string id, string art, string? meldung)
    {
        var daten = new Ligadaten(name, id, art, new DateOnly(2026, 8, 1), new DateOnly(2027, 1, 1), new DateOnly(2027, 5, 1));

        Assert.Equal(meldung, daten.Pruefen());
        Assert.Equal("Das Ende der Rückrunde muss nach dem Start der Rückrunde sein", (daten with { Name = "L", Id = "1", Art = "H", Ende = daten.Rueckrundenbeginn }).Pruefen());
        Assert.Equal("Der Start der Rückrunde muss nach dem Start der Vorrunde sein", (daten with { Name = "L", Id = "1", Art = "H", Rueckrundenbeginn = daten.Beginn }).Pruefen());
    }

    [Fact]
    public void Mannschaften_pruefen_auf_Eindeutigkeit()
    {
        Datenbearbeitung b = Oeffnen(mitAenderungen: false).DatenBearbeiten();
        IReadOnlyList<string> namen = b.Mannschaftsnamen();
        Mannschaftsdaten erste = b.Mannschaft(namen[0]);
        Mannschaftsdaten zweite = b.Mannschaft(namen[1]);
        var neu = new Mannschaftsdaten("Neue Mannschaft", "X-1", "V-1", 1, string.Empty);

        Assert.Equal(namen.Order(StringComparer.Ordinal), namen);
        Assert.Null(b.MannschaftPruefen(null, neu));
        Assert.Equal("Mannschaftsname ist nicht eindeutig", b.MannschaftPruefen(null, neu with { Name = erste.Name }));
        Assert.Equal("Mannschafts-ID ist nicht eindeutig", b.MannschaftPruefen(null, neu with { Id = erste.Id }));
        Assert.Equal("Zu diesem Verein gibt es schon eine Mannschaft mit dieser Nummer", b.MannschaftPruefen(null, neu with { VereinsId = erste.VereinsId, Nummer = erste.Nummer }));
        Assert.Equal("Die Vereins-ID darf nicht leer sein", b.MannschaftPruefen(null, neu with { VereinsId = " " }));
        Assert.Null(b.MannschaftPruefen(erste.Name, erste));
        Assert.Equal("Mannschaftsname ist nicht eindeutig", b.MannschaftPruefen(erste.Name, erste with { Name = zweite.Name }));
    }

    [Fact]
    public void Mannschaft_umbenennen_merkt_den_urspruenglichen_Namen()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: false);
        Datenbearbeitung b = sitzung.DatenBearbeiten();
        string alt = b.Mannschaftsnamen()[0];

        b.MannschaftSpeichern(alt, b.Mannschaft(alt) with { Name = "Umbenannt" });
        Assert.False(b.MannschaftenSindStandard);
        sitzung.DatenUebernehmen(b);

        Mannschaft m = Assert.Single(sitzung.Staffel.Mannschaften, x => x.Name == "Umbenannt");
        Assert.Equal(alt, m.UrspruenglicherName);

        Datenbearbeitung zurueck = sitzung.DatenBearbeiten();
        zurueck.MannschaftSpeichern("Umbenannt", zurueck.Mannschaft("Umbenannt") with { Name = alt });
        Assert.True(zurueck.MannschaftenSindStandard);
    }

    [Fact]
    public void Mannschaften_auf_Standard_zuruecksetzen()
    {
        Datenbearbeitung b = Oeffnen(mitAenderungen: false).DatenBearbeiten();
        List<string> vorher = b.Mannschaftsnamen().ToList();

        b.MannschaftLoeschen(vorher[0]);
        b.MannschaftSpeichern(null, new Mannschaftsdaten("Neue Mannschaft", "X-1", "V-1", 1, "2"));
        b.MannschaftSpeichern(vorher[1], b.Mannschaft(vorher[1]) with { Name = "Umbenannt", Spiellokal = "3" });
        Assert.False(b.MannschaftenSindStandard);

        b.MannschaftenZuruecksetzen();

        Assert.True(b.MannschaftenSindStandard);
        Assert.Equal(vorher, b.Mannschaftsnamen());
    }

    [Fact]
    public void Setzliste_bearbeiten_und_uebernehmen()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: false);
        Datenbearbeitung b = sitzung.DatenBearbeiten();
        IReadOnlyList<string> namen = b.Mannschaftsnamen();

        Setzlistendaten anfang = b.Setzliste();
        Assert.Equal(namen, anfang.Reihenfolge);

        List<string> umgekehrt = namen.Reverse().ToList();
        b.SetzlisteSpeichern(new Setzlistendaten(true, umgekehrt));
        Assert.Equal(new Setzlistendaten(true, umgekehrt).Reihenfolge, b.Setzliste().Reihenfolge);
        Assert.True(b.Setzliste().Aktiv);
        sitzung.DatenUebernehmen(b);

        Setzliste? setzliste = sitzung.Staffel.Setzliste;
        Assert.NotNull(setzliste);
        Assert.True(setzliste.Aktiv);
        Assert.Equal(umgekehrt, setzliste.Eintraege.OrderBy(e => e.Platz).Select(e => e.Mannschaft));
    }

    [Fact]
    public void Setzliste_ignoriert_unbekannte_und_ergaenzt_fehlende_Mannschaften()
    {
        Datenbearbeitung b = Oeffnen(mitAenderungen: false).DatenBearbeiten();
        IReadOnlyList<string> namen = b.Mannschaftsnamen();

        b.SetzlisteSpeichern(new Setzlistendaten(false, [namen[2], "Gibt es nicht", namen[0]]));
        b.MannschaftLoeschen(namen[0]);

        Setzlistendaten gelesen = b.Setzliste();
        Assert.Equal(namen[2], gelesen.Reihenfolge[0]);
        Assert.Equal(namen.Count - 1, gelesen.Reihenfolge.Count);
        Assert.DoesNotContain(namen[0], gelesen.Reihenfolge);
        Assert.Equal(gelesen.Reihenfolge.Skip(1), namen.Where(n => n != namen[0] && n != namen[2]));
    }

    [Fact]
    public void Heimtage_ueberstehen_Lesen_und_Schreiben()
    {
        Datenbearbeitung b = Oeffnen(mitAenderungen: true).DatenBearbeiten();
        foreach (string name in b.Mannschaftsnamen())
        {
            List<Heimtag> vorher = b.Heimtage(name).ToList();

            b.HeimtageSpeichern(name, vorher);

            Assert.Equal(vorher, b.Heimtage(name));
        }

        Assert.Contains(b.Mannschaftsnamen(), n => b.Heimtage(n).Any(h => h.IstHeimtag));
    }

    [Fact]
    public void Spiellokale_bearbeiten_und_uebernehmen()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: false);
        Datenbearbeitung b = sitzung.DatenBearbeiten();
        string name = b.Mannschaftsnamen().First(n => b.SpiellokaleLesen(n).Heimtage.Count > 1);
        Spiellokaldaten daten = b.SpiellokaleLesen(name);
        List<(DateTime Termin, string Spiellokal)> tage = daten.Heimtage.ToList();
        tage[0] = (tage[0].Termin, "3");
        tage[1] = (tage[1].Termin, "9");

        b.SpiellokaleSpeichern(name, daten with { Standardlokal = "2", Heimtage = tage });
        sitzung.DatenUebernehmen(b);

        Mannschaft m = sitzung.Staffel.Mannschaften.Single(x => x.Name == name);
        Assert.Equal("2", m.Spiellokal);
        List<Heimspieltermin> termine = m.Heimspieltermine.OrderBy(t => t.Zeitpunkt).ToList();
        Assert.Equal("3", termine.Single(t => t.Zeitpunkt == tage[0].Termin).Spiellokal);
        Assert.Equal(string.Empty, termine.Single(t => t.Zeitpunkt == tage[1].Termin).Spiellokal);
        Assert.False(sitzung.DatenBearbeiten().SpiellokaleLesen(name).IstStandard);
    }

    [Fact]
    public void Spiellokale_Standard_wie_im_Original()
    {
        DateTime t = new(2026, 9, 5, 18, 0, 0, DateTimeKind.Unspecified);
        Assert.True(new Spiellokaldaten(string.Empty, [(t, string.Empty)], [("(Damen) X", string.Empty)]).IstStandard);
        Assert.False(new Spiellokaldaten("1", [], []).IstStandard);
        Assert.False(new Spiellokaldaten(string.Empty, [], [("(Damen) X", "2")]).IstStandard);
        Assert.False(new Spiellokaldaten(string.Empty, [(t, "2")], []).IstStandard);
    }

    [Fact]
    public void Heimkoppel_Auswahl_wie_im_Original()
    {
        var einzeln = new Heimkoppel(new DateOnly(2026, 9, 5), null, Koppelwunsch.Gewuenscht, Koppelwunsch.Keiner);
        var paar = new Heimkoppel(new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 6), Koppelwunsch.Keiner, Koppelwunsch.Hoch);

        Assert.Equal(4, einzeln.Auswahl().Count);
        Assert.Equal(2, einzeln.Index);
        Assert.Equal(13, paar.Auswahl().Count);
        Assert.Equal(9, paar.Index);
        Assert.Equal("Sa 05.09.2026 und So 06.09.2026", paar.Beschriftung);
        Assert.Equal("So 06.09.2026: Zwei Heimspiele an diesem Tag gewünscht (hohe Prio)", paar.Auswahl()[9]);
        for (int i = 0; i < paar.Auswahl().Count; i++)
        {
            Assert.Equal(i, paar.MitIndex(i).Index);
        }
    }

    [Fact]
    public void Heimkoppeln_bearbeiten_und_uebernehmen()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: true);
        Datenbearbeitung b = sitzung.DatenBearbeiten();
        foreach (string n in b.Mannschaftsnamen())
        {
            List<Heimkoppel> vorher = b.Heimkoppeln(n).ToList();
            b.HeimkoppelnSpeichern(n, vorher);
            Assert.Equal(vorher, b.Heimkoppeln(n));
        }

        string name = b.Mannschaftsnamen().First(n => b.Heimtage(n).Any(h => h.IstHeimtag));
        Heimtag erster = b.Heimtage(name).First(h => h.IstHeimtag);
        DateTime folgetag = erster.Datum.Date.AddDays(1);
        b.HeimtageSpeichern(
            name,
            [
                .. b.Heimtage(name).Where(h => h.Datum.Date != folgetag),
                erster with { Datum = erster.Datum.AddDays(1), Koppeltermin = false, Doppeltermin = false, AuswaertsKoppelZweitzeit = false },
            ]);
        List<Heimkoppel> koppeln = b.Heimkoppeln(name).ToList();
        int index = koppeln.FindIndex(k => k.Tag2 is not null);
        Heimkoppel paar = koppeln[index].MitIndex(12);
        koppeln[index] = paar;

        b.HeimkoppelnSpeichern(name, koppeln);
        sitzung.DatenUebernehmen(b);

        Mannschaft m = sitzung.Staffel.Mannschaften.Single(x => x.Name == name);
        Assert.All(
            m.Heimspieltermine.Where(t => DateOnly.FromDateTime(t.Zeitpunkt) == paar.Tag1 || DateOnly.FromDateTime(t.Zeitpunkt) == paar.Tag2),
            t => Assert.Equal((Terminkoppelung.Koppeltermin, Koppelprioritaet.Hart), (t.Koppelung, t.Prioritaet)));
        Assert.Contains(sitzung.DatenBearbeiten().Heimkoppeln(name), k => k == paar);
    }

    [Fact]
    public void Auswaertskoppeln_bearbeiten_wie_im_Original()
    {
        Datenbearbeitung b = Oeffnen(mitAenderungen: false).DatenBearbeiten();
        List<string> namen = b.Mannschaftsnamen().ToList();
        string team = namen[0];
        var koppel = new Auswaertskoppeldaten(namen[1], namen[2], 0);

        Assert.DoesNotContain(team, b.Auswaertskoppelpartner(team));
        Assert.Equal("Bitte Mannschaft 1 angegeben", b.AuswaertskoppelPruefen(team, null, koppel with { MannschaftA = string.Empty }));
        Assert.Equal("Mannschaft 1 und 2 müssen unterschiedlich sein", b.AuswaertskoppelPruefen(team, null, koppel with { MannschaftB = namen[1] }));
        Assert.Null(b.AuswaertskoppelPruefen(team, null, koppel));
        Assert.True(b.AuswaertskoppelnSindStandard(team, []));

        b.AuswaertskoppelSpeichern(team, null, koppel);
        Assert.Contains(koppel, b.Auswaertskoppeln(team));
        Assert.Equal("Diesen Auswärtskoppelwunsch gibt es schon", b.AuswaertskoppelPruefen(team, null, koppel with { Art = 2 }));
        Assert.Null(b.AuswaertskoppelPruefen(team, koppel, koppel with { Art = 2 }));
        Assert.False(b.AuswaertskoppelnSindStandard(team, []));
        Assert.Equal($"{namen[1]}, {namen[2]} (am gleichen Tag)", koppel.Bezeichnung);

        b.AuswaertskoppelSpeichern(team, koppel, koppel with { Art = 1 });
        Assert.Contains(koppel with { Art = 1 }, b.Auswaertskoppeln(team));

        b.AuswaertskoppelnZuruecksetzen(team);
        Assert.True(b.AuswaertskoppelnSindStandard(team, b.AuswaertsZweitzeiten(team)));
    }

    [Fact]
    public void Alternative_Zeiten_fuer_Auswaertskoppeln()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: false);
        Datenbearbeitung b = sitzung.DatenBearbeiten();
        List<string> namen = b.Mannschaftsnamen().ToList();
        string team = namen.First(n => b.Heimtage(n).Any(h => h.IstHeimtag && !h.Koppeltermin));
        List<string> andere = namen.Where(n => n != team).ToList();
        Heimtag tag = b.Heimtage(team).First(h => h.IstHeimtag && !h.Koppeltermin);
        string partner = andere[0];
        b.HeimtageSpeichern(partner, [.. b.Heimtage(partner), tag with { Datum = tag.Datum.AddHours(2), Koppeltermin = false, Doppeltermin = false, AuswaertsKoppelZweitzeit = false }]);
        b.AuswaertskoppelSpeichern(andere[1], null, new Auswaertskoppeldaten(team, partner, 0));

        Zweitzeit zeit = Assert.Single(b.AuswaertsZweitzeiten(team), z => z.Termin == tag.Datum);
        Assert.Null(zeit.Zeit);
        b.AuswaertsZweitzeitenSpeichern(team, [zeit with { Zeit = new TimeOnly(20, 0) }]);
        Assert.Equal(new TimeOnly(20, 0), b.AuswaertsZweitzeiten(team).Single(z => z.Termin == tag.Datum).Zeit);
        sitzung.DatenUebernehmen(b);

        Heimspieltermin termin = sitzung.Staffel.Mannschaften.Single(m => m.Name == team).Heimspieltermine.Single(t => t.Zeitpunkt == tag.Datum);
        Assert.True(termin.ZweiteUhrzeitFuerAuswaertskoppel);
        Assert.Equal(new TimeOnly(20, 0), termin.ZweiteUhrzeit);
    }

    [Theory]
    [InlineData("18:00", 18, 0)]
    [InlineData(" 19:30 ", 19, 30)]
    [InlineData("8:00", -1, 0)]
    [InlineData("00:00", -1, 0)]
    [InlineData("25:00", -1, 0)]
    [InlineData("", -1, 0)]
    public void Alternative_Zeit_lesen_wie_im_Original(string text, int stunde, int minute)
    {
        Assert.Equal(stunde < 0 ? (TimeOnly?)null : new TimeOnly(stunde, minute), Zweitzeit.Lesen(text));
    }

    [Theory]
    [InlineData("18:00", "18:00")]
    [InlineData("18:00 A K2:14:00 Max:2 L:3", "18:00 A K2:14:00 Max:2 L:3")]
    [InlineData("max:2 L:3 A 18:00", "18:00 A Max:2 L:3")]
    [InlineData("19:30 T2:15:00 D1", "19:30 T2:15:00 D1")]
    [InlineData("frei", "FREI")]
    [InlineData("18:00 FREI", "FREI")]
    [InlineData("abc", "")]
    [InlineData("25:00", "")]
    [InlineData("A K1:14:00", "")]
    [InlineData("18:00 L:\"Halle A\"", "18:00 L:\"Halle A\"")]
    public void Kurztext_der_Wunschtermine_wie_im_Original(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, Heimtagtext.Bereinigen(eingabe));
    }

    [Fact]
    public void Wunschtermine_bearbeiten_und_uebernehmen()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: false);
        Datenbearbeitung b = sitzung.DatenBearbeiten();
        foreach (string n in b.Mannschaftsnamen())
        {
            List<Heimtag> vorher = b.Heimtage(n).ToList();
            IReadOnlyList<Wunschterminwoche> wochen = b.Wunschtermine(n);
            Assert.True(b.WunschtermineSindStandard(n, wochen));
            Assert.Null(Datenbearbeitung.WunschterminePruefen(wochen));
            b.WunschtermineSpeichern(n, wochen);
            Assert.Equal(vorher, b.Heimtage(n));
        }

        string name = b.Mannschaftsnamen()[0];
        List<Wunschterminwoche> geaendert = b.Wunschtermine(name).ToList();
        int w = geaendert.FindIndex(x => x.Texte[5].Length == 0 && x.Texte[6].Length == 0 && x.Texte[4].Length == 0);
        Wunschterminwoche woche = geaendert[w];
        geaendert[w] = woche with { Texte = [.. woche.Texte.Take(5), "20:00 D2", string.Empty] };
        Assert.StartsWith("Am Sa ", Datenbearbeitung.WunschterminePruefen(geaendert), StringComparison.Ordinal);
        geaendert[w] = woche with { Texte = [.. woche.Texte.Take(5), "20:00 D2", "10:00 D2"] };
        Assert.Null(Datenbearbeitung.WunschterminePruefen(geaendert));
        Assert.False(b.WunschtermineSindStandard(name, geaendert));

        b.WunschtermineSpeichern(name, geaendert);
        sitzung.DatenUebernehmen(b);

        List<Heimspieltermin> doppel = sitzung.Staffel.Mannschaften.Single(m => m.Name == name).Heimspieltermine
            .Where(t => DateOnly.FromDateTime(t.Zeitpunkt) >= woche.Montag.AddDays(5) && DateOnly.FromDateTime(t.Zeitpunkt) <= woche.Montag.AddDays(6))
            .ToList();
        Assert.Equal(2, doppel.Count);
        Assert.All(doppel, t => Assert.Equal((Terminkoppelung.Doppelspieltag, Koppelprioritaet.Hart), (t.Koppelung, t.Prioritaet)));
    }

    [Fact]
    public void Nachbarmannschaften_bearbeiten_wie_im_Original()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: false);
        Datenbearbeitung b = sitzung.DatenBearbeiten();
        string team = b.Mannschaftsnamen().First(n => b.Nachbarmannschaften(n).Count > 0);
        Nachbarmannschaftseintrag erster = b.Nachbarmannschaften(team)[0];

        Nachbarmannschaftsentwurf unveraendert = b.NachbarmannschaftBearbeiten(team, erster.Index);
        Assert.Null(b.NachbarmannschaftPruefen(team, erster.Index, unveraendert));
        b.NachbarmannschaftUebernehmen(team, erster.Index, unveraendert);
        Assert.True(b.NachbarmannschaftenSindStandard(team));

        Nachbarmannschaftsentwurf neu = b.NachbarmannschaftBearbeiten(team, null);
        Assert.Equal("Der Name darf nicht leer sein", b.NachbarmannschaftPruefen(team, null, neu));
        neu.Name = "TTC Nachbar";
        Assert.Equal("Die Art darf nicht leer sein", b.NachbarmannschaftPruefen(team, null, neu));
        neu.Art = "Damen";
        neu.Nummer = 2;
        neu.Spiellokal = "2";
        var termin = new DateTime(2026, 10, 3, 18, 0, 0, DateTimeKind.Unspecified);
        var heimspiel = new Nachbarspieldaten(termin, "TTC Nachbar", "TV Gast", string.Empty);
        Assert.Equal("Kein Gegner angegeben", neu.SpielPruefen(null, heimspiel, " "));
        Assert.Null(neu.SpielPruefen(null, heimspiel, "TV Gast"));
        neu.SpielSpeichern(null, heimspiel, heimspiel: true);
        Assert.Equal("Zu diesem Termin gibt es schon eine Begegnung", neu.SpielPruefen(null, heimspiel, "TV Gast"));
        Assert.Equal("2", Assert.Single(neu.Spiele()).Spiel.Spiellokal);
        neu.Name = "TTC Nachbar II";
        Assert.Equal("TTC Nachbar", Assert.Single(neu.Spiele()).Spiel.Heim);
        Assert.Null(b.NachbarmannschaftPruefen(team, null, neu));
        b.NachbarmannschaftUebernehmen(team, null, neu);
        Assert.False(b.NachbarmannschaftenSindStandard(team));
        Assert.Contains(b.Nachbarmannschaften(team), e => e.Bezeichnung == "(Damen) TTC Nachbar II 1, Spiele, Spiellokal: 2");

        Nachbarmannschaftsentwurf doppelt = b.NachbarmannschaftBearbeiten(team, null);
        doppelt.Name = "TTC Nachbar II";
        doppelt.Art = "Damen";
        Assert.Equal("Diese Nachbarmannschaft gibt es schon", b.NachbarmannschaftPruefen(team, null, doppelt));

        sitzung.DatenUebernehmen(b);
        Nachbarmannschaft nachbar = sitzung.Staffel.Mannschaften.Single(m => m.Name == team).Nachbarmannschaften.Single(n => n.Name == "TTC Nachbar II");
        Assert.Equal(("Damen", 2, "2"), (nachbar.Geschlecht, nachbar.Nummer, nachbar.Spiellokal));
        Assert.Equal(new Nachbarspiel(termin, "TTC Nachbar", "TV Gast", string.Empty), Assert.Single(nachbar.Spiele));

        Datenbearbeitung zurueck = sitzung.DatenBearbeiten();
        zurueck.NachbarmannschaftenZuruecksetzen(team);
        Assert.True(zurueck.NachbarmannschaftenSindStandard(team));
    }

    [Fact]
    public void Sechzig_km_Regel_bearbeiten_und_uebernehmen()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: false);
        Datenbearbeitung b = sitzung.DatenBearbeiten();
        List<string> namen = b.Mannschaftsnamen().ToList();
        string team = namen[0];
        Assert.Equal(b.NurWochenendeStandard(team), b.NurWochenendeGegen(team));

        b.NurWochenendeSpeichern(team, [namen[2], "Gibt es nicht", namen[1]]);

        Assert.Equal(new[] { namen[2], namen[1] }, b.NurWochenendeGegen(team));
        sitzung.DatenUebernehmen(b);
        Mannschaft m = sitzung.Staffel.Mannschaften.Single(x => x.Name == team);
        Assert.Contains(namen[1], m.KeinWochenspielGegen);
        Assert.Contains(namen[2], m.KeinWochenspielGegen);
    }

    [Fact]
    public void Spielfreie_Tage_bearbeiten_und_uebernehmen()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: false);
        Datenbearbeitung b = sitzung.DatenBearbeiten();
        IReadOnlyList<DateOnly> moeglich = b.MoeglicheSpieltage();
        Assert.Equal(moeglich.Order(), moeglich);
        Assert.Equal(b.SpielfreieTageStandard(), b.SpielfreieTage());
        DateOnly ohneWunsch = new(2026, 12, 24);
        while (moeglich.Contains(ohneWunsch))
        {
            ohneWunsch = ohneWunsch.AddDays(1);
        }

        b.SpielfreieTageSpeichern([moeglich[3], moeglich[1], ohneWunsch]);
        Assert.Equal(new[] { moeglich[1], moeglich[3] }, b.SpielfreieTage());

        // Abweichend vom Original bleibt ein spielfreier Tag ohne Heimspielwunsch erhalten (Befund #22).
        b.SpielfreieTageSpeichern([moeglich[1], moeglich[3]]);
        sitzung.DatenUebernehmen(b);

        Assert.Contains(ohneWunsch, sitzung.Staffel.SpielfreieTage);
        Assert.Contains(moeglich[1], sitzung.Staffel.SpielfreieTage);
        Assert.Contains(moeglich[3], sitzung.Staffel.SpielfreieTage);
    }

    [Fact]
    public void Pflichtspieltage_bearbeiten_und_uebernehmen()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: false);
        Datenbearbeitung b = sitzung.DatenBearbeiten();
        Assert.Equal(b.PflichtspielzeitenStandard(), b.Pflichtspielzeiten());
        var spaet = new Pflichtspielzeit(new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 14), 2);
        var frueh = new Pflichtspielzeit(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 14), 1);

        Assert.Equal("Das \"bis\" Datum darf nicht kleiner als das \"von\" Datum sein", (spaet with { Bis = spaet.Von.AddDays(-1) }).Pruefen([]));
        Assert.Equal("Diesen Zeitraum gibt es schon", (spaet with { Anzahl = 5 }).Pruefen([spaet]));
        Assert.Null(frueh.Pruefen([spaet]));
        Assert.Equal("So 01.03.2026 - So 14.03.2027".Length, spaet.Bezeichnung.Length);

        b.PflichtspielzeitenSpeichern([spaet, frueh]);
        Assert.Equal(new[] { frueh, spaet }, b.Pflichtspielzeiten());
        sitzung.DatenUebernehmen(b);

        Assert.Contains(sitzung.Staffel.Pflichtspielzeitraeume, p => p.Von == frueh.Von && p.Bis == frueh.Bis && p.AnzahlSpiele == 1);
        Assert.Contains(sitzung.Staffel.Pflichtspielzeitraeume, p => p.Von == spaet.Von && p.Bis == spaet.Bis && p.AnzahlSpiele == 2);
    }

    [Fact]
    public void Festgelegte_Begegnungen_bearbeiten_und_uebernehmen()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: false);
        Datenbearbeitung b = sitzung.DatenBearbeiten();
        Assert.True(b.VorgegebeneSpieleSindStandard);
        int vorher = b.VorgegebeneSpiele().Count;
        IReadOnlyList<string> namen = b.Mannschaftsnamen();
        var spiel = new Vorgabespiel(0, new DateTime(2026, 11, 7, 18, 30, 0, DateTimeKind.Unspecified), namen[0], namen[1], string.Empty);

        Assert.Equal("Keine Heimmanschaft angegeben", b.VorgegebenesSpielPruefen(null, spiel with { Heim = string.Empty }));
        Assert.Equal("Keine Gastmanschaft angegeben", b.VorgegebenesSpielPruefen(null, spiel with { Gast = string.Empty }));
        Assert.Equal("Die Mannschaft kann nicht gegen sich selbst spielen", b.VorgegebenesSpielPruefen(null, spiel with { Gast = namen[0] }));
        Assert.Null(b.VorgegebenesSpielPruefen(null, spiel));

        b.VorgegebenesSpielSpeichern(null, spiel);
        Assert.False(b.VorgegebeneSpieleSindStandard);
        Assert.Equal("Dieses Spiel gibt es zu diesem Termin schon", b.VorgegebenesSpielPruefen(null, spiel));
        Vorgabespiel gespeichert = b.VorgegebeneSpiele().Single(s => s.Termin == spiel.Termin && s.Heim == spiel.Heim);
        Assert.Null(b.VorgegebenesSpielPruefen(gespeichert.Index, spiel));
        Assert.Equal(vorher + 1, b.VorgegebeneSpiele().Count);

        sitzung.DatenUebernehmen(b);
        Assert.Contains(sitzung.Staffel.VorgegebeneSpiele, s => s.Zeitpunkt == spiel.Termin && s.Heim == spiel.Heim && s.Gast == spiel.Gast);

        Datenbearbeitung zweite = sitzung.DatenBearbeiten();
        Vorgabespiel wieder = zweite.VorgegebeneSpiele().Single(s => s.Termin == spiel.Termin && s.Heim == spiel.Heim);
        zweite.VorgegebenesSpielLoeschen(wieder.Index);
        Assert.Equal(vorher, zweite.VorgegebeneSpiele().Count);
        zweite.VorgegebeneSpieleZuruecksetzen();
        Assert.True(zweite.VorgegebeneSpieleSindStandard);
    }

    [Fact]
    public void Heimrecht_bearbeiten_und_uebernehmen()
    {
        Plansitzung sitzung = Oeffnen(mitAenderungen: false);
        Datenbearbeitung b = sitzung.DatenBearbeiten();
        IReadOnlyList<string> namen = b.Mannschaftsnamen();
        string team = namen[0];
        Heimrecht vorher = b.HeimrechtLesen(team);
        Assert.Equal(Heimrecht.Automatisch, vorher.HeimspieleVorrunde);
        Assert.Equal(namen.Count - 1, vorher.Gegner.Count);
        Assert.DoesNotContain(team, vorher.Gegner.Keys);

        var gegner = vorher.Gegner.ToDictionary(g => g.Key, _ => Heimrecht.Egal);
        gegner[namen[1]] = Heimrecht.Vorrunde;
        gegner[namen[2]] = Heimrecht.Rueckrunde;
        b.HeimrechtSpeichern(team, new Heimrecht(3, gegner));
        Heimrecht nachher = b.HeimrechtLesen(team);
        Assert.Equal(3, nachher.HeimspieleVorrunde);
        Assert.Equal(Heimrecht.Vorrunde, nachher.Gegner[namen[1]]);
        Assert.Equal(Heimrecht.Rueckrunde, nachher.Gegner[namen[2]]);
        Assert.Equal(Heimrecht.Egal, nachher.Gegner[namen[3]]);

        sitzung.DatenUebernehmen(b);
        Mannschaft m = sitzung.Staffel.Mannschaften.Single(x => x.Name == team);
        Assert.Equal(3, m.HeimspieleHinrunde);
        Assert.Contains(new Heimrechtvorgabe(namen[1], Runde.Hinrunde), m.Heimrechte);
        Assert.Contains(new Heimrechtvorgabe(namen[2], Runde.Rueckrunde), m.Heimrechte);
        Assert.Equal(2, m.Heimrechte.Count);
    }

    [Fact]
    public void Datendialog_D01_Allgemein_wie_im_Original() =>
        DatendialogVergleichen("D01_Allgemein", b => b.Ligadaten = b.Ligadaten with { Name = "4. Kreisklasse Gruppe A Test", Ende = new DateOnly(2027, 5, 16) });

    [Fact]
    public void Datendialog_D02_Mannschaft_aendern_wie_im_Original() =>
        DatendialogVergleichen("D02_Mannschaft_aendern", b => b.MannschaftSpeichern("Vluy Wwae", b.Mannschaft("Vluy Wwae") with { Name = "Vluy Wwae Aoia", Spiellokal = "2" }));

    [Fact]
    public void Datendialog_D03_Mannschaft_neu_wie_im_Original_bis_auf_Befund_21() =>
        DatendialogVergleichen(
            "D03_Mannschaft_neu",
            b => b.MannschaftSpeichern(null, new Mannschaftsdaten("Trbk Tzqu", "1234", "12345", 1, string.Empty)),
            erwartet => erwartet.KinderMitNamen("team").Single(t => t.Lesen("teamname") == "Trbk Tzqu").Setzen("homerights", -1));

    [Fact]
    public void Datendialog_D04_Setzliste_wie_im_Original() =>
        DatendialogVergleichen("D04_Setzliste", b =>
        {
            List<string> reihenfolge = ["Vluy Wwae", .. b.Mannschaftsnamen().Where(n => n != "Vluy Wwae")];
            b.SetzlisteSpeichern(new Setzlistendaten(true, reihenfolge));
        });

    [Fact]
    public void Datendialog_D05_Spiellokale_wie_im_Original() =>
        DatendialogVergleichen("D05_Spiellokale", b =>
        {
            Spiellokaldaten daten = b.SpiellokaleLesen("Tueq Qfga IV");
            b.SpiellokaleSpeichern("Tueq Qfga IV", daten with
            {
                Standardlokal = "2",
                Nachbarn = daten.Nachbarn.Select(n => n.Bezeichnung == "(Damen) Tueq Qfga" ? (n.Bezeichnung, "3") : n).ToList(),
            });
        });

    [Fact]
    public void Datendialog_D06_Heimkoppeln_wie_im_Original() =>
        DatendialogVergleichen("D06_Heimkoppeln", b =>
        {
            var tag = new DateOnly(2015, 10, 10);
            List<Heimkoppel> koppeln = b.Heimkoppeln("Euxw Grxg Bcbs")
                .Select(k => k with
                {
                    Option1 = k.Tag1 == tag ? AufMoeglich(k.Option1) : k.Option1,
                    Option2 = k.Tag2 == tag ? AufMoeglich(k.Option2) : k.Option2,
                })
                .ToList();
            b.HeimkoppelnSpeichern("Euxw Grxg Bcbs", koppeln);
        });

    [Fact]
    public void Datendialog_D07_Auswaertskoppeln_wie_im_Original() =>
        DatendialogVergleichen("D07_Auswaertskoppeln", b =>
            b.AuswaertskoppelSpeichern("Ujuu Eaom IV", null, new Auswaertskoppeldaten("Tueq Qfga IV", "Vluy Wwae", 0)));

    [Fact]
    public void Datendialog_D08_Wunschtermine_wie_im_Original() =>
        DatendialogVergleichen("D08_Wunschtermine", b =>
        {
            List<Wunschterminwoche> wochen = b.Wunschtermine("Tueq Qfga IV")
                .Select(w => w.Montag == new DateOnly(2026, 9, 7)
                    ? w with { Texte = w.Texte.Select((t, i) => i == 2 ? t.Replace("20:15", "20:00", StringComparison.Ordinal) : t).ToList() }
                    : w)
                .ToList();
            b.WunschtermineSpeichern("Tueq Qfga IV", wochen);
        });

    [Fact]
    public void Datendialog_D09_Nachbarmannschaften_wie_im_Original() =>
        DatendialogVergleichen("D09_Nachbarmannschaften", b =>
        {
            Nachbarmannschaftsentwurf entwurf = b.NachbarmannschaftBearbeiten("Tueq Qfga IV", null);
            entwurf.Name = "Tueq Tzqu";
            entwurf.Nummer = 1;
            entwurf.Art = "Herren";
            entwurf.SpielSpeichern(null, new Nachbarspieldaten(new DateTime(2026, 10, 15, 20, 0, 0, DateTimeKind.Unspecified), "Tueq Tzqu", "X", string.Empty), heimspiel: true);
            Assert.Null(b.NachbarmannschaftPruefen("Tueq Qfga IV", null, entwurf));
            b.NachbarmannschaftUebernehmen("Tueq Qfga IV", null, entwurf);
        });

    [Fact]
    public void Datendialog_D10_60km_wie_im_Original() =>
        DatendialogVergleichen("D10_60km", b => b.NurWochenendeSpeichern("Tueq Qfga IV", ["Vluy Wwae"]));

    [Fact]
    public void Datendialog_D11_Spielfreie_Tage_wie_im_Original() =>
        DatendialogVergleichen("D11_Spielfreie_Tage", b => b.SpielfreieTageSpeichern([new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 18)]));

    [Fact]
    public void Datendialog_D12_Pflichtspieltage_wie_im_Original() =>
        DatendialogVergleichen("D12_Pflichtspieltage", b => b.PflichtspielzeitenSpeichern(
        [
            new Pflichtspielzeit(new DateOnly(2027, 4, 12), new DateOnly(2027, 4, 18), 2),
            new Pflichtspielzeit(new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 7), 1),
        ]));

    [Fact]
    public void Datendialog_D13_Begegnungen_wie_im_Original() =>
        DatendialogVergleichen("D13_Begegnungen", b => b.VorgegebenesSpielSpeichern(
            null,
            new Vorgabespiel(0, new DateTime(2026, 10, 10, 20, 0, 0, DateTimeKind.Unspecified), "Vluy Wwae", "Ujuu Hpom IV", string.Empty)));

    [Fact]
    public void Datendialog_D14_Heimrecht_wie_im_Original() =>
        DatendialogVergleichen("D14_Heimrecht", b =>
        {
            Heimrecht bisher = b.HeimrechtLesen("Vluy Wwae");
            var gegner = bisher.Gegner.ToDictionary(g => g.Key, g => g.Key == "Ujuu Hpom IV" ? Heimrecht.Vorrunde : g.Value);
            b.HeimrechtSpeichern("Vluy Wwae", new Heimrecht(3, gegner));
        });

    [Fact]
    public void Umbenennung_zieht_alle_Verweise_nach()
    {
        Datenbearbeitung b = Oeffnen(mitAenderungen: false).DatenBearbeiten();
        IReadOnlyList<string> namen = b.Mannschaftsnamen();
        string a = namen[0];
        string alt = namen[1];
        string c = namen[2];
        b.SetzlisteSpeichern(new Setzlistendaten(true, namen));
        b.HeimrechtSpeichern(a, new Heimrecht(Heimrecht.Automatisch, new Dictionary<string, int> { [alt] = Heimrecht.Vorrunde }));
        b.NurWochenendeSpeichern(a, [alt]);
        b.AuswaertskoppelSpeichern(a, null, new Auswaertskoppeldaten(alt, c, 0));
        b.AuswaertskoppelSpeichern(c, null, new Auswaertskoppeldaten(a, alt, 1));
        b.VorgegebenesSpielSpeichern(null, new Vorgabespiel(0, new DateTime(2026, 10, 10, 20, 0, 0, DateTimeKind.Unspecified), alt, c, string.Empty));
        b.VorgegebenesSpielSpeichern(null, new Vorgabespiel(0, new DateTime(2026, 11, 10, 20, 0, 0, DateTimeKind.Unspecified), c, alt, string.Empty));

        b.MannschaftSpeichern(alt, b.Mannschaft(alt) with { Name = "Umbenannt" });

        Assert.Contains("Umbenannt", b.Setzliste().Reihenfolge);
        Assert.DoesNotContain(alt, b.Setzliste().Reihenfolge);
        Assert.Equal(Heimrecht.Vorrunde, b.HeimrechtLesen(a).Gegner["Umbenannt"]);
        Assert.Equal("Umbenannt", Assert.Single(b.NurWochenendeGegen(a)));
        Assert.Contains(b.Auswaertskoppeln(a), k => k.MannschaftA == "Umbenannt" && k.MannschaftB == c);
        Assert.Contains(b.Auswaertskoppeln(c), k => k.MannschaftA == a && k.MannschaftB == "Umbenannt");
        Assert.Contains(b.VorgegebeneSpiele(), s => s.Heim == "Umbenannt");
        Assert.Contains(b.VorgegebeneSpiele(), s => s.Gast == "Umbenannt");
    }

    [Fact]
    public void Auswaertskoppel_loeschen()
    {
        Datenbearbeitung b = Oeffnen(mitAenderungen: false).DatenBearbeiten();
        IReadOnlyList<string> namen = b.Mannschaftsnamen();
        var koppel = new Auswaertskoppeldaten(namen[1], namen[2], 2);
        b.AuswaertskoppelSpeichern(namen[0], null, koppel);
        Assert.Contains(koppel, b.Auswaertskoppeln(namen[0]));

        b.AuswaertskoppelLoeschen(namen[0], koppel);
        b.AuswaertskoppelLoeschen(namen[0], koppel);

        Assert.DoesNotContain(koppel, b.Auswaertskoppeln(namen[0]));
    }

    [Fact]
    public void Heimkoppeln_alle_Auswahlmoeglichkeiten_bleiben_erhalten()
    {
        Datenbearbeitung b = Oeffnen(mitAenderungen: false).DatenBearbeiten();
        string team = b.Mannschaftsnamen()[0];
        List<Heimtag> tage = b.Heimtage(team).ToList();
        HashSet<DateOnly> belegt = tage.Select(t => DateOnly.FromDateTime(t.Datum)).ToHashSet();
        List<Heimtag> einzeln = tage
            .Where(t => t.IstHeimtag)
            .Where(t => Enumerable.Range(-2, 5).All(d => d == 0 || !belegt.Contains(DateOnly.FromDateTime(t.Datum).AddDays(d))))
            .Take(2)
            .ToList();
        Assert.Equal(2, einzeln.Count);
        DateOnly paarTag = DateOnly.FromDateTime(einzeln[0].Datum);
        DateOnly einzelTag = DateOnly.FromDateTime(einzeln[1].Datum);
        tage.Add(einzeln[0] with { Datum = einzeln[0].Datum.AddDays(1) });
        tage = tage.Select(t => t == einzeln[1] ? t with { Koppeltermin = true } : t).ToList();
        b.HeimtageSpeichern(team, tage);

        Heimkoppel paar = b.Heimkoppeln(team).Single(k => k.Tag1 == paarTag);
        Heimkoppel allein = b.Heimkoppeln(team).Single(k => k.Tag1 == einzelTag);
        Assert.Equal(paarTag.AddDays(1), paar.Tag2);
        Assert.Null(allein.Tag2);

        for (int i = 0; i < paar.Auswahl().Count; i++)
        {
            b.HeimkoppelnSpeichern(team, [paar.MitIndex(i), allein]);
            Assert.Equal(i, b.Heimkoppeln(team).Single(k => k.Tag1 == paarTag).Index);
        }

        for (int i = 1; i < allein.Auswahl().Count; i++)
        {
            b.HeimkoppelnSpeichern(team, [allein.MitIndex(i)]);
            Assert.Equal(i, b.Heimkoppeln(team).Single(k => k.Tag1 == einzelTag).Index);
        }
    }

    [Fact]
    public void Eigene_Plandatei_ohne_Termine_bekommt_die_Vorgaben_des_Originals()
    {
        string pfad = Path.Combine(ordner, "daten", "leer.xml");
        PlanDatenDatei.Speichern(pfad, new DatenKnoten("plan"));

        Datenbearbeitung b = Plansitzung.Oeffnen(pfad, Path.Combine(ordner, "basis")).DatenBearbeiten();

        int jahr = DateTime.Today.Year;
        Assert.Equal(new DateOnly(jahr, 7, 1), b.Ligadaten.Beginn);
        Assert.Equal(new DateOnly(jahr, 12, 30), b.Ligadaten.Rueckrundenbeginn);
        Assert.Equal(new DateOnly(jahr + 1, 5, 1), b.Ligadaten.Ende);
        Assert.False(b.HatStandard);
        Assert.True(b.VorgegebeneSpieleSindStandard);
        b.VorgegebeneSpieleZuruecksetzen();
        Assert.Contains("Herren", Ligadaten.Arten);
    }

    private static Koppelwunsch AufMoeglich(Koppelwunsch wunsch) => wunsch switch
    {
        Koppelwunsch.Gewuenscht or Koppelwunsch.Hoch => Koppelwunsch.Moeglich,
        Koppelwunsch.DoppelGewuenscht or Koppelwunsch.DoppelHoch => Koppelwunsch.DoppelMoeglich,
        _ => wunsch,
    };

    private static string Schluessel(Spiel s) => $"{s.Zeitpunkt:O}|{s.Heim}|{s.Gast}";

    private static List<Spiel> CsvSpiele(byte[] csv) => DelphiKompatibel.Windows1252.GetString(csv)
        .Split("\r\n")
        .Skip(1)
        .Where(z => z.Length > 0)
        .Select(z => z.Split(';'))
        .Select(f => new Spiel(DateTime.ParseExact(f[4] + " " + f[6], "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture), f[8], f[10], string.Empty))
        .ToList();

    private static string Eingabeordner() => Path.Combine(Testdaten.Referenz, "eingabe");

    /// <summary>
    /// Vergleich mit einer im Original gemachten Bearbeitung (Referenz/datendialoge, siehe ANLEITUNG.md): dieselbe
    /// Bearbeitung über die Datenbearbeitung ausführen und die geschriebene <c>.modifications</c> inhaltlich vergleichen.
    /// Die Reihenfolge der Attribute folgt im Original der Hash-Reihenfolge und zählt deshalb nicht.
    /// </summary>
    /// <param name="name">Dateiname ohne Endung, z. B. <c>D01_Allgemein</c>.</param>
    /// <param name="bearbeiten">Die Bearbeitung wie im Original.</param>
    /// <param name="bewussteAbweichung">Passt die Erwartung an eine dokumentierte Korrektur an.</param>
    private void DatendialogVergleichen(string name, Action<Datenbearbeitung> bearbeiten, Action<DatenKnoten>? bewussteAbweichung = null)
    {
        string quelle = Path.Combine(Path.GetDirectoryName(Eingabeordner())!, "datendialoge");
        string ziel = Path.Combine(ordner, "daten", name + ".xml");
        File.Copy(Path.Combine(quelle, name + ".xml"), ziel, overwrite: true);
        string aenderungen = Plansitzung.ModifikationenPfad(ziel);
        if (File.Exists(aenderungen))
        {
            File.Delete(aenderungen);
        }

        Plansitzung sitzung = Plansitzung.Oeffnen(ziel, Path.Combine(ordner, "basis"));
        Datenbearbeitung b = sitzung.DatenBearbeiten();
        bearbeiten(b);
        sitzung.DatenUebernehmen(b);

        DatenKnoten erwartet = PlanDatenDatei.Laden(Path.Combine(quelle, name + ".modifications"));
        bewussteAbweichung?.Invoke(erwartet);
        Assert.Equal(erwartet.Kanonisch(), PlanDatenDatei.Laden(aenderungen).Kanonisch());
    }

    /// <summary>Kopiert den Referenzfall R2 in einen eigenen Ordner, damit nichts neben den Referenzdaten entsteht.</summary>
    private Plansitzung Oeffnen(bool mitAenderungen)
    {
        string ziel = Path.Combine(ordner, "daten", "staffel.xml");
        File.Copy(Path.Combine(Eingabeordner(), R2 + ".xml"), ziel, overwrite: true);
        string aenderungen = Plansitzung.ModifikationenPfad(ziel);
        if (mitAenderungen)
        {
            File.Copy(Path.Combine(Eingabeordner(), R2 + ".modifications"), aenderungen, overwrite: true);
        }
        else if (File.Exists(aenderungen))
        {
            File.Delete(aenderungen);
        }

        return Plansitzung.Oeffnen(ziel, Path.Combine(ordner, "basis"));
    }
}
