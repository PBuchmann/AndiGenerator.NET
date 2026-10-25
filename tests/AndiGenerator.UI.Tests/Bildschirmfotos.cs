// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Persistence.Terminwuensche;
using AndiGenerator.Presentation;
using AndiGenerator.Presentation.Tests;
using AndiGenerator.Rendering;
using AndiGenerator.UI.Ansichten;
using AndiGenerator.UI.Seiten;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Dock.Avalonia.Controls;

namespace AndiGenerator.UI.Tests;

/// <summary>
/// Erzeugt die Bildschirmfotos der Anleitung (<c>Doku/Anleitung/Bilder</c>) mit der echten Oberfläche ohne Bildschirm und
/// anonymisierten Testdaten – immer gleich groß und mit denselben Daten. Läuft nur, wenn die Umgebungsvariable
/// <see cref="Variable"/> den Zielordner nennt (<c>bildschirmfotos.cmd</c>); im normalen Testlauf tut der Test nichts.
/// </summary>
[Collection(OberflaechenSammlung.Name)]
public sealed class Bildschirmfotos : IDisposable
{
    /// <summary>Umgebungsvariable mit dem Zielordner der Bilder.</summary>
    public const string Variable = "ANDIGEN_BILDER";

    /// <summary>Vergrößerung der Bilder (wie ein Bildschirm mit 150 %), damit sie im PDF scharf sind.</summary>
    private const double Skala = 1.5;

    private const string Staffel = "R2_4__Kreisklasse_Gruppe_A (2)";

    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-bilder-" + Guid.NewGuid().ToString("N"));
    private readonly Oberflaeche oberflaeche;

    public Bildschirmfotos(Oberflaeche oberflaeche)
    {
        this.oberflaeche = oberflaeche;
        oberflaeche.Protokoll.Leeren();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(ordner, recursive: true);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            // Aufräumen ist nicht Teil der Bilder.
        }
    }

    [Fact]
    public Task Bilder_der_Anleitung_erzeugen() => oberflaeche.Ausfuehren(async () =>
    {
        if (Environment.GetEnvironmentVariable(Variable) is not { Length: > 0 } ziel)
        {
            return;
        }

        Directory.CreateDirectory(ziel);
        IReadOnlyList<Vereinsmeldung> bericht = Terminmeldungen(
            Plansitzung.Oeffnen(StaffelKopieren("bericht", mitAenderungen: false), Path.Combine(ordner, "basis-bericht")).Staffel.Mannschaften);
        await EinrichtungAsync(ziel, StaffelKopieren("erststart", mitAenderungen: false), bericht);
        string datei = StaffelKopieren("daten", mitAenderungen: true);
        SetzlisteEinschalten(datei);
        await HauptfensterAsync(ziel, datei);
        Dialoge(ziel, datei);
        await DatenseitenAsync(ziel);
        await KompaktEinrichtungAsync(ziel, KompaktklasseRoh());
        await KompaktAsync(ziel, Kompaktklasse.Vorbereiten(ordner));
    });

    /// <summary>
    /// Startseite und Einrichtung: Die Einrichtung erscheint nur, wenn es etwas zu klären gibt – sicher beim ersten Öffnen
    /// einer click-TT-Datei (ohne <c>.modifications</c>).
    /// </summary>
    private static async Task EinrichtungAsync(string ziel, string datei, IReadOnlyList<Vereinsmeldung> bericht)
    {
        string basis = Path.Combine(Path.GetDirectoryName(datei)!, "..", "basis");
        var o = new TestOberflaeche { DateiZumOeffnen = datei, Sammeln = true };
        using var modell = new HauptfensterViewModel(o, () => new HostWindow(), basis, null);
        var fenster = new HauptfensterView { DataContext = modell, Width = 1400, Height = 860 };
        fenster.Show();
        try
        {
            Foto(ziel, "01-startseite", fenster, o);
            await modell.OeffnenCommand.ExecuteAsync(null);
            Foto(ziel, "02-einrichtung", fenster, o);

            // Daten aus click-TT: Mannschaftsmeldung mit erfundenen TTR-Werten als Vorschlag für die Setzliste.
            if (modell.Einrichtung is { } einrichtung && einrichtung.Schritte.FirstOrDefault(s => s.Titel == "Daten aus click-TT ergänzen") is { } zusatz)
            {
                einrichtung.Aktiv = zusatz;
                if (zusatz.Seite is { Seiten: [ZusatzdatenSeiteViewModel seite] } dialog)
                {
                    string csv = Path.Combine(Path.GetDirectoryName(datei)!, "Mannschaftsmeldung.csv");
                    await File.WriteAllTextAsync(csv, Mannschaftsmeldung(dialog.Bearbeitung.Setzliste().Reihenfolge));
                    seite.AufstellungenLaden(csv);
                }

                Oberflaeche.Zeichnen(fenster);
                Foto(ziel, "26-daten-aus-clicktt", fenster, o);

                // Terminwünsche (PDF): erfundene Spiellokale mit echten Postleitzahlen; die Seite rollt zur Meldung.
                if (zusatz.Seite is { Seiten: [ZusatzdatenSeiteViewModel seite2] } dialog2)
                {
                    seite2.TerminwuenscheAnzeigen(dialog2.Bearbeitung.TerminwuenscheAbgleichen(bericht), "Terminmeldungen.pdf");
                    Oberflaeche.Zeichnen(fenster);
                    Foto(ziel, "27-terminwuensche-pdf", fenster, o);

                    // Bis zur Liste „Sperre fehlt vermutlich“ mit dem Knopf rollen, die Kacheln darüber bleiben im Bild.
                    Button setzen = fenster.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, "Fehlende Sperren setzen…"));
                    setzen.BringIntoView(new Rect(0, 0, setzen.Bounds.Width, setzen.Bounds.Height + 40));
                    Oberflaeche.Zeichnen(fenster);
                    Foto(ziel, "28-wochenspiele", fenster, o);
                }
            }
        }
        finally
        {
            fenster.Close();
        }
    }

    /// <summary>
    /// Bericht „Terminwünsche“ wie aus click-TT: je Verein erfundene Spiellokale an echten Orten (Postleitzahl und Ort, für
    /// die Entfernungen), die Termine der Staffel, zwei Bemerkungen und ein absichtlicher Unterschied.
    /// </summary>
    private static List<Vereinsmeldung> Terminmeldungen(IReadOnlyList<Mannschaft> mannschaften)
    {
        (string Plz, string Ort)[] orte =
        [
            ("55116", "Mainz"), ("55232", "Alzey"), ("56068", "Koblenz"), ("67547", "Worms"), ("55543", "Bad Kreuznach"),
            ("55218", "Ingelheim am Rhein"), ("55276", "Oppenheim"), ("55286", "Wörrstadt"), ("55411", "Bingen am Rhein"),
            ("54290", "Trier"), ("55469", "Simmern"), ("55606", "Kirn"),
        ];
        string[] roemisch = [string.Empty, string.Empty, " II", " III", " IV", " V", " VI", " VII", " VIII", " IX", " X"];
        List<string> vereine = mannschaften.Select(m => m.VereinsId).Distinct().ToList();
        var meldungen = new List<Vereinsmeldung>();
        for (int i = 0; i < mannschaften.Count; i++)
        {
            Mannschaft m = mannschaften[i];
            (string plz, string ort) = orte[vereine.IndexOf(m.VereinsId) % orte.Length];
            string halle = m.Heimspieltermine.Select(t => t.Halle).FirstOrDefault(h => h.Length > 0) ?? ("Sporthalle " + ort);
            List<GemeldeterHeimtermin> termine = m.Heimspieltermine.Select(t => new GemeldeterHeimtermin(t.Zeitpunkt, halle, "2", "-")).ToList();
            if (i == 0 && termine.Count > 1)
            {
                termine.RemoveAt(termine.Count - 1);
            }

            string bemerkung = (i % 4) switch
            {
                1 => "Bitte möglichst keine Spiele vor 19:30 Uhr.",
                3 => "Heimspiele bevorzugt freitags.",
                _ => string.Empty,
            };
            meldungen.Add(new Vereinsmeldung(
                "Verein " + ort,
                int.Parse(m.VereinsId, System.Globalization.CultureInfo.InvariantCulture),
                "Erwachsene" + roemisch[Math.Min(m.Nummer, roemisch.Length - 1)],
                new Dictionary<int, Spiellokaladresse>
                {
                    [1] = new(halle, "Schulstraße " + (i + 3).ToString(System.Globalization.CultureInfo.InvariantCulture), plz, ort),
                    [2] = new("Turnhalle " + ort, "Am Sportplatz 1", plz, ort),
                },
                bemerkung,
                termine,
                m.Sperrtermine));
        }

        return meldungen;
    }

    /// <summary>Mannschaftsmeldung im Format von click-TT mit erfundenen TTR-Werten (je Mannschaft 4 oder 5 Stammspieler).</summary>
    private static string Mannschaftsmeldung(IReadOnlyList<string> mannschaften)
    {
        var text = new System.Text.StringBuilder("MannschaftName;SpielerMannschaftNr;SpielerRang;TTR;SpielerStatus\n");
        for (int i = 0; i < mannschaften.Count; i++)
        {
            string name = mannschaften[i];
            int nummer = name.Split(' ')[^1] switch { "II" => 2, "III" => 3, "IV" => 4, "V" => 5, "VI" => 6, _ => 1 };
            int basis = 1350 + ((i * 137) % 400);
            for (int rang = 1; rang <= 4 + (i % 2); rang++)
            {
                text.Append(System.Globalization.CultureInfo.InvariantCulture, $"{name};{nummer};{rang};{basis - (rang * 23)};\n");
            }
        }

        return text.ToString();
    }

    /// <summary>Hauptfenster mit Kostenoptimierung und Automodus, alle Ansichten, Spielplandaten.</summary>
    private static async Task HauptfensterAsync(string ziel, string datei)
    {
        string basis = Path.Combine(Path.GetDirectoryName(datei)!, "..", "basis");
        var o = new TestOberflaeche { DateiZumOeffnen = datei, Sammeln = true };
        using var modell = new HauptfensterViewModel(o, () => new HostWindow(), basis, null);
        var fenster = new HauptfensterView { DataContext = modell, Width = 1400, Height = 860 };
        fenster.Show();
        try
        {
            await modell.OeffnenCommand.ExecuteAsync(null);
            o.Abarbeiten();
            if (modell.Einrichtung is { } einrichtung)
            {
                await einrichtung.AbschliessenCommand.ExecuteAsync(null);
                o.Abarbeiten();
            }

            // Kostenoptimierung ab dem Plan aus click-TT, kurz rechnen lassen und anhalten.
            modell.Startplan = modell.Startplaene.Single(p => p.Quelle == PlanQuelle.ClickTt);
            modell.KostenoptimierungCommand.Execute(null);
            await modell.GenerierungCommand.ExecuteAsync(null);
            await WartenAsync(o, TimeSpan.FromSeconds(8), () => false);

            // Leitstand der Kostenoptimierung (mit Spezial-Insel), solange gerechnet wird; danach wieder die Kosten vorn.
            Zeigen(modell.LeitstandCommand, o);
            await WartenAsync(o, TimeSpan.FromSeconds(2), () => false);
            Foto(ziel, "20-leitstand-kosten", Ansichten(fenster), o);
            Zeigen(modell.KostenansichtCommand, o);
            modell.PauseCommand.Execute(null);

            // Erste Sicherung: Noch gibt es keinen gesicherten Plan, der Plan der Kostenoptimierung wird still gesichert.
            Assert.True(await modell.BeendenErlaubtAsync());
            o.Abarbeiten();
            Foto(ziel, "03-hauptfenster", fenster, o);
            Foto(ziel, "04-kostenansicht", Ansichten(fenster), o);

            Zeigen(modell.MeldungsansichtCommand, o);
            Foto(ziel, "16-meldungen", Ansichten(fenster), o);
            Zeigen(modell.TerminplanansichtCommand, o);
            Foto(ziel, "06-terminplan", Ansichten(fenster), o);

            Zeigen(modell.DiagrammansichtCommand, o);
            Foto(ziel, "07-diagramm-spieltage", Ansichten(fenster), o);
            var diagramm = (DiagrammAnsichtViewModel)Einzige<DiagrammAnsicht>(fenster).DataContext!;
            if (diagramm.Auswahl.FirstOrDefault(w => w.Teil == Diagrammteil.Spielverteilung) is { } verteilung)
            {
                diagramm.WaehlenCommand.Execute(verteilung);
                Foto(ziel, "17-diagramm-spielverteilung", Ansichten(fenster), o);
            }

            DiagrammeFotografieren(ziel, fenster, diagramm, o, [
                (Diagrammteil.WechselHeimAuswaerts, "35-diagramm-wechsel"),
                (Diagrammteil.AbstandHeimAuswaerts, "36-diagramm-abstand"),
                (Diagrammteil.SpieleProWoche, "37-diagramm-woche"),
                (Diagrammteil.Setzliste, "38-diagramm-setzliste"),
            ]);

            Zeigen(modell.TerminwunschansichtCommand, o);
            var wuensche = (TerminwunschAnsichtViewModel)Einzige<TerminwunschAnsicht>(fenster).DataContext!;
            wuensche.IstUebersicht = true;
            Foto(ziel, "08-terminwuensche", Ansichten(fenster), o);
            wuensche.IstKarten = true;
            Foto(ziel, "14-terminwuensche-karten", Ansichten(fenster), o);

            Zeigen(modell.NachbarterminansichtCommand, o);
            Foto(ziel, "15-nachbarmannschaften", Ansichten(fenster), o);
            Zeigen(modell.EinstellungenCommand, o);
            Foto(ziel, "10-einstellungen", Ansichten(fenster), o);

            // Wechsel in den Automodus: ohne Basisoptimierung gleich Stufe A; rechnen, bis er an Kriterien arbeitet.
            modell.AutomodusCommand.Execute(null);
            await WartenAsync(o, TimeSpan.FromSeconds(45), () => modell.Anzeige.Phase.Length > 0);
            await WartenAsync(o, TimeSpan.FromSeconds(3), () => false);

            // Leitstand, solange gerechnet wird (die Kerne zeigen ihre Last).
            Zeigen(modell.LeitstandCommand, o);
            await WartenAsync(o, TimeSpan.FromSeconds(2), () => false);
            Foto(ziel, "19-leitstand", Ansichten(fenster), o);

            modell.PauseCommand.Execute(null);
            Foto(ziel, "18-automodus", Kacheln(fenster), o);

            // Verlauf von Kostenoptimierung und Automodus.
            Zeigen(modell.VerlaufCommand, o);
            Foto(ziel, "21-verlauf", Ansichten(fenster), o);

            // Zweite Sicherung: Jetzt gibt es einen gesicherten Plan; die Rückfrage vergleicht ihn mit dem des Automodus.
            Assert.True(await modell.BeendenErlaubtAsync());
            o.Abarbeiten();
            if (o.Vergleich is { } vergleich)
            {
                var sicherung = new Sicherungsdialog(vergleich);
                sicherung.Show();
                Foto(ziel, "22-sicherung", sicherung, o);
                sicherung.Close();
            }

            // Qualität im Automodus (mit Einstufung), gewählt die erste Zeile mit Verstößen je Mannschaft.
            Zeigen(modell.QualitaetsansichtCommand, o);
            var qualitaet = (QualitaetAnsichtViewModel)Einzige<QualitaetAnsicht>(fenster).DataContext!;
            await WartenAsync(o, TimeSpan.FromSeconds(60), () => !qualitaet.Zeilen.Any(z => z.BestmoeglichRechnet));
            if (qualitaet.Zeilen.FirstOrDefault(z => z.Zustand != Qualitaetszustand.Erfuellt && z.Kriterium is not null) is { } zeile)
            {
                qualitaet.Waehlen(zeile);
            }

            Foto(ziel, "05-qualitaet", Ansichten(fenster), o);

            // Schritt für Schritt im Automodus: Begleitleiste rechts, der Scheinwerfer auf dem aktuellen Schritt.
            modell.FuehrungStarten(Fuehrungsart.Automodus);
            Foto(ziel, "59-schritt-fuer-schritt", fenster, o);
            modell.FuehrungCommand.Execute(null);

            // B-Varianten aus HiGHS im bildschirmfüllenden Dialog, verglichen mit dem Plan aus click-TT (der laufende Plan ist
            // in B meist schon so gut, dass keine Variante ihn verbessert): Knopf in der Kopfzeile der Stufe B.
            qualitaet.Quelle = PlanQuelle.ClickTt;
            await WartenAsync(o, TimeSpan.FromSeconds(60), () => KnopfB(qualitaet) is not null);
            if (KnopfB(qualitaet) is IAsyncRelayCommand knopf)
            {
                await knopf.ExecuteAsync(null);
                o.Abarbeiten();
            }

            if (o.Alternativendialoge.LastOrDefault() is { } varianten)
            {
                var alternativen = new Alternativendialog(varianten) { Width = 1400, Height = 860 };
                alternativen.Show();
                Foto(ziel, "39-b-varianten", alternativen, o);
                alternativen.Close();
            }

            var ueber = new Ueberdialog(new UeberViewModel(o, UeberViewModel.VersionVon(typeof(HauptfensterViewModel).Assembly)));
            ueber.Show();
            Foto(ziel, "40-ueber", ueber, o);
            ueber.Close();

            // Spielplandaten über den Befehl, der den Dialog zeigt.
            o.Datendialog = dialog =>
            {
                var datenfenster = new Datendialog(dialog);
                datenfenster.Show();
                Foto(ziel, "12-spielplandaten", datenfenster, o);
                datenfenster.Close();
                return Task.FromResult(false);
            };
            modell.StoppenCommand.Execute(null);
            o.Abarbeiten();
            await modell.DatenCommand.ExecuteAsync(null);
        }
        finally
        {
            fenster.Close();
        }
    }

    /// <summary>
    /// Kompaktspieltage mit der erfundenen Klasse <c>testdaten/kompakt</c>: Varianten berechnen (HiGHS), Pläne und
    /// Erläuterung, Raster mit Karte, Spieltage, Meldungen bearbeiten und die Druckauswahl im Kompaktmodus.
    /// </summary>
    private static async Task KompaktAsync(string ziel, string datei)
    {
        string basis = Path.Combine(Path.GetDirectoryName(datei)!, "..", "basis-kompakt");
        var o = new TestOberflaeche { DateiZumOeffnen = datei, Sammeln = true };
        using var modell = new HauptfensterViewModel(o, () => new HostWindow(), basis, null);
        var fenster = new HauptfensterView { DataContext = modell, Width = 1400, Height = 860 };
        DruckauswahlViewModel? druckauswahl = null;
        fenster.Show();
        try
        {
            await modell.OeffnenCommand.ExecuteAsync(null);
            o.Abarbeiten();
            if (modell.Einrichtung is { } einrichtung)
            {
                await einrichtung.AbschliessenCommand.ExecuteAsync(null);
                o.Abarbeiten();
            }

            Zeigen(modell.KompaktspieltageCommand, o);
            await modell.GenerierungCommand.ExecuteAsync(null);
            await WartenAsync(o, TimeSpan.FromMinutes(3), () => !modell.KompaktRechnet);
            Foto(ziel, "30-kompakt-plaene", Ansichten(fenster), o);

            modell.FuehrungStarten(Fuehrungsart.Kompaktspieltage);
            Foto(ziel, "60-kompakt-schritt-fuer-schritt", fenster, o);
            modell.FuehrungCommand.Execute(null);

            // Diagramme im Kompaktmodus: Ausrichter statt Wechsel, Spielorte in Spielverteilung und Woche.
            Zeigen(modell.DiagrammansichtCommand, o);
            var diagramm = (DiagrammAnsichtViewModel)Einzige<DiagrammAnsicht>(fenster).DataContext!;
            DiagrammeFotografieren(ziel, fenster, diagramm, o, [
                (Diagrammteil.Ausrichter, "56-kompakt-diagramm-ausrichter"),
                (Diagrammteil.Spielverteilung, "57-kompakt-diagramm-spielverteilung"),
                (Diagrammteil.SpieleProWoche, "58-kompakt-diagramm-woche"),
            ]);
            Zeigen(modell.KompaktspieltageCommand, o);

            // Für Raster, Karte und Spieltage ein hohes Fenster, damit alles ohne Rollen im Bild ist.
            fenster.Height = 2600;
            Oberflaeche.Zeichnen(fenster);
            Border raster = fenster.GetVisualDescendants().OfType<Border>().First(b => b.Name == "Raster");
            Foto(ziel, "31-kompakt-raster-karte", raster.GetVisualAncestors().OfType<Grid>().First(), o);
            Border spieltage = fenster.GetVisualDescendants().OfType<TextBlock>()
                .First(b => b.Text == "Spieltage" && b.FontSize > 14)
                .GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("karte"));
            Foto(ziel, "32-kompakt-spieltage", spieltage, o);

            // Meldungen im Raster bearbeiten: Rahmen auf einem Heimtermin, darunter Beginn und Halle.
            var ansicht = (KompaktspieltageAnsichtViewModel)Einzige<KompaktspieltageAnsicht>(fenster).DataContext!;
            ansicht.Bearbeiten = true;
            Kompaktzelle zelle = ansicht.Zeilen.SelectMany(z => z.Zellen).Skip(9).FirstOrDefault(z => z.Meldung == "beides") ?? ansicht.Zeilen[1].Zellen[1];
            ansicht.ZelleWaehlenCommand.Execute(zelle);
            Oberflaeche.Zeichnen(fenster);
            Foto(ziel, "33-kompakt-bearbeiten", raster, o);
            ansicht.Bearbeiten = false;
            fenster.Height = 860;
            Oberflaeche.Zeichnen(fenster);

            // Drucken im Kompaktmodus: nur die Übersicht für die Vereine, mit dem gewählten Plan, im Hochformat.
            o.Druckauswahl = a =>
            {
                druckauswahl = a;
                return true;
            };
            o.DateiZumSpeichern = Path.Combine(Path.GetTempPath(), "andigen-kompaktdruck.pdf");
            await modell.DruckenCommand.ExecuteAsync(null);
            o.Abarbeiten();
            Assert.NotNull(druckauswahl);
            Assert.True(druckauswahl.IstKompakt);
            Assert.Equal("Bestes Gesamtpaket", druckauswahl.Planname);
            Druckdokument dokument = Assert.IsType<Druckdokument>(o.Ausdruck);
            Assert.Equal("Bestes Gesamtpaket", dokument.Plan);
            Assert.Equal(["Meldungen und Plan", "Alle Fahrten", "Kompaktspieltage"], dokument.Abschnitte.Take(3).Select(a => a.Titel));
            Assert.DoesNotContain(dokument.Abschnitte, a => a.Titel is "Terminwünsche" or "Spielplan" or "Mannschaftspläne" or "Diagramme");
            Assert.Same(Seitenformat.A4Hoch, o.Format);
        }
        finally
        {
            fenster.Close();
        }

        var druck = new Druckauswahldialog(druckauswahl ?? throw new InvalidOperationException("Die Druckauswahl wurde nicht gezeigt."));
        druck.Show();
        Foto(ziel, "34-kompakt-druckauswahl", druck, o);
        druck.Close();
    }

    /// <summary>Gewichtung einer Kostenart, Druckauswahl und Wunschtermin.</summary>
    private static void Dialoge(string ziel, string datei)
    {
        var o = new TestOberflaeche();
        Plansitzung sitzung = Plansitzung.Oeffnen(datei, Path.Combine(Path.GetDirectoryName(datei)!, "..", "basis"));
        var art = MannschaftsKostenart.Sperrtermine;
        (string name, IReadOnlyList<string> meldungen) = sitzung.Staffel.Mannschaften
            .Select(m => (m.Name, sitzung.Meldungen(sitzung.Staffel.BestehenderSpielplan, m.Name, art)))
            .OrderByDescending(x => x.Item2.Count)
            .First();
        var gewichtung = new Gewichtungsdialog($"Gewichtung der Mannschaft {name} ändern", Gewichtungsanzeige.Name(art), Gewichtung.Normal, meldungen);
        gewichtung.Show();
        Foto(ziel, "09-gewichtung", gewichtung, o);
        gewichtung.Close();

        var druck = new Druckauswahldialog(new DruckauswahlViewModel([PlanQuelle.Laufend, PlanQuelle.ClickTt]));
        druck.Show();
        Foto(ziel, "11-druckauswahl", druck, o);
        druck.Close();

        // Wunschtermin: Doppelklick auf einen Termin in den Spielplandaten öffnet den Dialog.
        using var a = new Arbeitsplatz("datendialoge/D01_Allgemein.xml");
        a.Oberflaeche.Wunschtermin = dialog =>
        {
            var wunsch = new Wunschtermindialog(dialog);
            wunsch.Show();
            Foto(ziel, "13-wunschtermin", wunsch, o);
            wunsch.Close();
            return false;
        };
        var daten = new Datendialog(a.Dialog);
        daten.Show();
        try
        {
            SeiteWaehlen<SpielfreieTageSeiteViewModel>(daten, a.Dialog);
            Foto(ziel, "23-spielfreie-tage", daten, o);

            // Setzliste mit erfundenen TTR-Schnitten wie nach einer übernommenen Mannschaftsmeldung.
            SetzlistenSeiteViewModel setzliste = SeiteWaehlen<SetzlistenSeiteViewModel>(daten, a.Dialog);
            a.Bearbeitung.MannschaftsstaerkenSpeichern(a.Bearbeitung.Setzliste().Reihenfolge.Select((n, i) => new Mannschaftsstaerke(n, 1350 + ((i * 137) % 400), 4)));
            setzliste.Laden();
            setzliste.Aktiv = true;
            Oberflaeche.Zeichnen(daten);
            Foto(ziel, "24-setzliste", daten, o);

            WunschtermineSeiteViewModel seite = SeiteWaehlen<WunschtermineSeiteViewModel>(daten, a.Dialog);
            seite.Mannschaft = Namen.RheinduerkheimIV;
            Oberflaeche.Zeichnen(daten);
            Foto(ziel, "25-wunschtermine", daten, o);
            WunschterminDetail detail = daten.GetVisualDescendants().OfType<WunschterminDetail>().Single();
            Point punkt = detail.GetVisualDescendants()
                .OfType<Border>()
                .Where(t => t.Classes.Contains("kaertchen") && t.DataContext is WunschterminZelle)
                .Select(t => t.TranslatePoint(new Point(t.Bounds.Width / 2, t.Bounds.Height / 2), daten))
                .OfType<Point>()
                .First(p => new Rect(daten.ClientSize).Contains(p));
            daten.MouseDown(punkt, MouseButton.Left);
            daten.MouseUp(punkt, MouseButton.Left);
            daten.MouseDown(punkt, MouseButton.Left);
            daten.MouseUp(punkt, MouseButton.Left);
            Oberflaeche.Zeichnen(daten);

            // Wochenende (60-km-Regel) mit Entfernungen, nachdem die Terminwünsche übernommen sind.
            a.Bearbeitung.TerminwuenscheUebernehmen(a.Bearbeitung.TerminwuenscheAbgleichen(Terminmeldungen(a.Sitzung.Staffel.Mannschaften)), "Terminmeldungen.pdf");
            WochenendeSeiteViewModel wochenende = SeiteWaehlen<WochenendeSeiteViewModel>(daten, a.Dialog);
            wochenende.Mannschaft = wochenende.Namen.First(n => n != wochenende.Mannschaft);
            Oberflaeche.Zeichnen(daten);
            Foto(ziel, "29-wochenende-entfernungen", daten, o);
        }
        finally
        {
            daten.Close();
        }
    }

    /// <summary>
    /// Die übrigen Seiten der Spielplandaten mit der Staffel der Anleitung (mit Änderungen) und die Bearbeitungsdialoge,
    /// die sie öffnen: Mannschaft, Auswärtskoppel, Nachbarmannschaft mit Spiel, Pflichtspielzeit, Vorgabespiel.
    /// </summary>
    private static async Task DatenseitenAsync(string ziel)
    {
        var o = new TestOberflaeche();
        using var a = new Arbeitsplatz("eingabe/" + Staffel + ".xml");
        a.Oberflaeche.Mannschaft = d => DialogFoto(ziel, "49-dialog-mannschaft", new Mannschaftsdialog(d), o);
        a.Oberflaeche.Auswaertskoppel = d => DialogFoto(ziel, "50-dialog-auswaertskoppel", new Auswaertskoppeldialog(d), o);
        a.Oberflaeche.Nachbarspiel = d => DialogFoto(ziel, "51-dialog-nachbarspiel", new Nachbarspieldialog(d), o);
        a.Oberflaeche.Nachbarmannschaft = async d =>
        {
            d.Name = "TTC Nachbarort";
            d.Nummer = 2;
            d.Art = NachbarmannschaftsdialogViewModel.Arten.FirstOrDefault() ?? "Herren";
            var fenster = new Nachbarmannschaftsdialog(d);
            fenster.Show();
            Foto(ziel, "52-dialog-nachbarmannschaft", fenster, o);
            await d.NeuCommand.ExecuteAsync(null);
            fenster.Close();
            return false;
        };
        a.Oberflaeche.Pflichtspielzeit = d => DialogFoto(ziel, "53-dialog-pflichtspielzeit", new Pflichtspielzeitdialog(d), o);
        a.Oberflaeche.Vorgabespiel = d => DialogFoto(ziel, "54-dialog-vorgabespiel", new Vorgabespieldialog(d), o);

        var daten = new Datendialog(a.Dialog);
        daten.Show();
        try
        {
            MannschaftenSeiteViewModel mannschaften = SeiteWaehlen<MannschaftenSeiteViewModel>(daten, a.Dialog);
            Foto(ziel, "41-daten-mannschaften", daten, o);
            mannschaften.Auswahl = mannschaften.Namen.FirstOrDefault();
            await (mannschaften.Auswahl is null ? mannschaften.NeuCommand : mannschaften.BearbeitenCommand).ExecuteAsync(null);

            Mehr<SpiellokaleSeiteViewModel>(daten, a.Dialog);
            Foto(ziel, "42-daten-spiellokale", daten, o);
            Mehr<HeimkoppelnSeiteViewModel>(daten, a.Dialog);
            Foto(ziel, "43-daten-heimkoppeln", daten, o);

            AuswaertskoppelnSeiteViewModel koppeln = Mehr<AuswaertskoppelnSeiteViewModel>(daten, a.Dialog);
            Foto(ziel, "44-daten-auswaertskoppeln", daten, o);
            if (koppeln.Detail is AuswaertskoppelDetailViewModel koppel)
            {
                await koppel.NeuCommand.ExecuteAsync(null);
            }

            NachbarmannschaftenSeiteViewModel nachbarn = Mehr<NachbarmannschaftenSeiteViewModel>(daten, a.Dialog);
            Foto(ziel, "45-daten-nachbarmannschaften", daten, o);
            if (nachbarn.Detail is NachbarmannschaftDetailViewModel nachbar)
            {
                await nachbar.NeuCommand.ExecuteAsync(null);
            }

            PflichtspieltageSeiteViewModel pflicht = SeiteWaehlen<PflichtspieltageSeiteViewModel>(daten, a.Dialog);
            Foto(ziel, "46-daten-pflichtspieltage", daten, o);
            await pflicht.NeuCommand.ExecuteAsync(null);

            VorgabespieleSeiteViewModel vorgaben = SeiteWaehlen<VorgabespieleSeiteViewModel>(daten, a.Dialog);
            Foto(ziel, "47-daten-vorgabespiele", daten, o);
            await vorgaben.NeuCommand.ExecuteAsync(null);

            Mehr<HeimrechtSeiteViewModel>(daten, a.Dialog);
            Foto(ziel, "48-daten-heimrecht", daten, o);
        }
        finally
        {
            daten.Close();
        }
    }

    /// <summary>Eine Seite mit Mannschaftsliste: gewählt ist die erste Mannschaft mit Einträgen (sonst die erste).</summary>
    private static T Mehr<T>(Datendialog daten, DatenDialogViewModel dialog)
        where T : MehrMannschaftenSeiteViewModel
    {
        T seite = SeiteWaehlen<T>(daten, dialog);
        string? mitEintraegen = seite.Namen.FirstOrDefault(n =>
        {
            seite.Mannschaft = n;
            return seite.Detail is { IstStandard: false };
        });
        seite.Mannschaft = mitEintraegen ?? seite.Namen.FirstOrDefault();

        Oberflaeche.Zeichnen(daten);
        return seite;
    }

    private static bool DialogFoto(string ziel, string name, Window dialog, TestOberflaeche o)
    {
        dialog.Show();
        Foto(ziel, name, dialog, o);
        dialog.Close();
        return false;
    }

    /// <summary>Zeigt die Diagramme nacheinander (nur die, die es für die Staffel gibt) und fotografiert sie.</summary>
    private static void DiagrammeFotografieren(string ziel, Window fenster, DiagrammAnsichtViewModel diagramm, TestOberflaeche o, IReadOnlyList<(Diagrammteil Teil, string Name)> liste)
    {
        foreach ((Diagrammteil teil, string name) in liste)
        {
            if (diagramm.Auswahl.FirstOrDefault(w => w.Teil == teil) is { } wahl)
            {
                diagramm.WaehlenCommand.Execute(wahl);
                Foto(ziel, name, Ansichten(fenster), o);
            }
        }
    }

    /// <summary>Erstes Öffnen der Kompaktklasse: Die Einrichtung schlägt Kompaktspieltage vor.</summary>
    private static async Task KompaktEinrichtungAsync(string ziel, string datei)
    {
        var o = new TestOberflaeche { DateiZumOeffnen = datei, Sammeln = true };
        using var modell = new HauptfensterViewModel(o, () => new HostWindow(), Path.Combine(Path.GetDirectoryName(datei)!, "..", "basis-roh"), null);
        var fenster = new HauptfensterView { DataContext = modell, Width = 1400, Height = 860 };
        fenster.Show();
        try
        {
            await modell.OeffnenCommand.ExecuteAsync(null);
            o.Abarbeiten();
            if (modell.Einrichtung is { } einrichtung && einrichtung.Schritte.FirstOrDefault(s => s.Titel == "Klasse mit Kompaktspieltagen") is { } schritt)
            {
                einrichtung.Aktiv = schritt;
                Foto(ziel, "55-kompakt-einrichtung", fenster, o);
            }
        }
        finally
        {
            fenster.Close();
        }
    }

    /// <summary>Wechselt im Dialog „Spielplandaten bearbeiten“ wie per Klick in der Seitenliste auf eine Seite.</summary>
    private static T SeiteWaehlen<T>(Datendialog daten, DatenDialogViewModel dialog)
        where T : DatenSeiteViewModel
    {
        T seite = dialog.Seiten.OfType<T>().Single();
        daten.GetVisualDescendants().OfType<ListBox>().Single(l => l.Classes.Contains("seitenliste")).SelectedItem = seite;
        Oberflaeche.Zeichnen(daten);
        return seite;
    }

    /// <summary>
    /// Speichert ein Steuerelement (Fenster oder Teil davon) als PNG in 150 %. Gezeichnet wird immer das ganze Fenster und
    /// dann zugeschnitten: Zeichnet man nur einen Teil, zerfällt dort die Schrift.
    /// </summary>
    private static void Foto(string ziel, string name, Control steuerelement, TestOberflaeche o)
    {
        o.Abarbeiten();
        Zeichnen(steuerelement);
        o.Abarbeiten();

        // Der Scheinwerfer der Führung „Schritt für Schritt“ dunkelt alles außer dem gezeigten Element ab; im Programm
        // erlischt er nach wenigen Sekunden, im Test vergeht diese Zeit nicht. Für saubere Bilder vor dem Foto ausschalten
        // (erst nach dem Abarbeiten, das ihn neu richten kann).
        if (TopLevel.GetTopLevel(steuerelement) is HauptfensterView hauptfenster)
        {
            Dispatcher.UIThread.RunJobs();
            hauptfenster.Licht.Ausschalten();
        }

        Zeichnen(steuerelement);
        Visual fenster = TopLevel.GetTopLevel(steuerelement) ?? steuerelement;
        var aufloesung = new Vector(96 * Skala, 96 * Skala);
        using var bild = new RenderTargetBitmap(Pixel(fenster.Bounds.Size), aufloesung);
        bild.Render(fenster);
        string datei = Path.Combine(ziel, name + ".png");
        if (ReferenceEquals(fenster, steuerelement))
        {
            Speichern(bild, datei);
            return;
        }

        Point oben = steuerelement.TranslatePoint(default, fenster) ?? default;
        PixelRect ausschnitt = new PixelRect(
            (int)Math.Round(oben.X * Skala),
            (int)Math.Round(oben.Y * Skala),
            Pixel(steuerelement.Bounds.Size).Width,
            Pixel(steuerelement.Bounds.Size).Height)
            .Intersect(new PixelRect(bild.PixelSize));
        using var teil = new WriteableBitmap(ausschnitt.Size, aufloesung, PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (ILockedFramebuffer puffer = teil.Lock())
        {
            bild.CopyPixels(ausschnitt, puffer.Address, puffer.RowBytes * ausschnitt.Height, puffer.RowBytes);
        }

        Speichern(teil, datei);
    }

    private static PixelSize Pixel(Size groesse) =>
        new((int)Math.Ceiling(groesse.Width * Skala), (int)Math.Ceiling(groesse.Height * Skala));

    private static void Speichern(Bitmap bild, string datei)
    {
#pragma warning disable CS0618 // wie Bildschirmfoto: die neue Speicher-Methode verlangt noch undokumentierte Optionen
        bild.Save(datei);
#pragma warning restore CS0618
    }

    private static void Zeichnen(Control steuerelement)
    {
        if (TopLevel.GetTopLevel(steuerelement) is Window fenster)
        {
            Oberflaeche.Zeichnen(fenster);
        }
        else
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>Lässt die Generierung rechnen und arbeitet dabei die Meldungen an die Oberfläche ab.</summary>
    private static async Task WartenAsync(TestOberflaeche o, TimeSpan hoechstens, Func<bool> fertig)
    {
        DateTime ende = DateTime.UtcNow + hoechstens;
        while (DateTime.UtcNow < ende && !fertig())
        {
            await Task.Delay(200);
            o.Abarbeiten();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Zeigen(IRelayCommand befehl, TestOberflaeche o)
    {
        befehl.Execute(null);
        o.Abarbeiten();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Einzige<T>(Window fenster)
        where T : Control =>
        fenster.GetVisualDescendants().OfType<T>().Single();

    /// <summary>Der Hauptbereich mit den Ansichten (ohne Kopf, Kacheln und Leiste).</summary>
    private static Control Ansichten(Window fenster) => fenster.GetVisualDescendants().OfType<DockControl>().First();

    /// <summary>Die Kacheln mit dem Stand der Generierung.</summary>
    private static Control Kacheln(Window fenster) =>
        fenster.GetVisualDescendants().OfType<Grid>().First(g => g.Classes.Contains("kacheln"));

    /// <summary>Der Knopf „B-Varianten wählen“ in der Kopfzeile der Stufe B, falls es Varianten gibt.</summary>
    private static IAsyncRelayCommand? KnopfB(QualitaetAnsichtViewModel qualitaet) =>
        qualitaet.Zeilen.Select(z => z.Kopf).OfType<Stufenkopf>().FirstOrDefault(k => k.Knopf.StartsWith("B-Varianten", StringComparison.Ordinal))?.KnopfBefehl as IAsyncRelayCommand;

    /// <summary>Schaltet die Setzliste ein (Reihenfolge wie in den Daten), damit es auch ihr Diagramm gibt.</summary>
    private void SetzlisteEinschalten(string datei)
    {
        Plansitzung sitzung = Plansitzung.Oeffnen(datei, Path.Combine(ordner, "basis-setzliste"));
        Datenbearbeitung b = sitzung.DatenBearbeiten();
        b.SetzlisteSpeichern(new Setzlistendaten(true, b.Setzliste().Reihenfolge));
        sitzung.DatenUebernehmen(b);
    }

    /// <summary>Kopiert die erfundene Kompaktklasse unverändert (wie frisch aus click-TT) in einen eigenen Ordner.</summary>
    private string KompaktklasseRoh()
    {
        string daten = Path.Combine(ordner, "kompakt-roh");
        Directory.CreateDirectory(daten);
        string datei = Path.Combine(daten, "Jugend 19 Testliga (HR).xml");
        File.Copy(Path.Combine(Testdaten.Referenz, "..", "kompakt", "Jugend_19_Testliga_(HR).xml"), datei);
        return datei;
    }

    /// <summary>Kopiert die Staffel (click-TT-Datei, auf Wunsch mit Änderungen) in einen eigenen Ordner, damit nichts verändert wird.</summary>
    private string StaffelKopieren(string unterordner, bool mitAenderungen)
    {
        string daten = Path.Combine(ordner, unterordner);
        Directory.CreateDirectory(daten);
        string quelle = Path.Combine(Testdaten.Referenz, "eingabe", Staffel);
        string datei = Path.Combine(daten, "4. Kreisklasse Gruppe A.xml");
        File.Copy(quelle + ".xml", datei);
        if (mitAenderungen && File.Exists(quelle + ".modifications"))
        {
            File.Copy(quelle + ".modifications", Path.ChangeExtension(datei, ".modifications"));
        }

        return datei;
    }
}
