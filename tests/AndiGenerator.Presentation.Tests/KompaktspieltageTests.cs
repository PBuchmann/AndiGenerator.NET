// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Rendering;
using Dock.Model.Core;

namespace AndiGenerator.Presentation.Tests;

/// <summary>
/// Die Ansicht „Kompaktspieltage“ mit der erfundenen Kompaktklasse: Meldungen im Raster bearbeiten, Auswahl von Sonntag,
/// Mannschaft und Spieltag, Berechnung mit Pause und Beenden, Pläne wählen, merken, drucken und exportieren.
/// </summary>
public sealed class KompaktspieltageTests : IDisposable
{
    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-kompakt-" + Guid.NewGuid().ToString("N"));
    private readonly TestOberflaeche o = new();
    private readonly HauptfensterViewModel hf;

    public KompaktspieltageTests()
    {
        Directory.CreateDirectory(ordner);
        o.DateiZumOeffnen = Kompaktklasse.Vorbereiten(ordner);
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

    /// <summary>Rahmen mit Pfeiltasten bewegen, Meldungen setzen, Beginn, Spiellokal und Tische eines Heimtermins speichern.</summary>
    /// <returns>Der Testlauf.</returns>
    [Fact]
    public async Task Meldungen_im_Raster_bearbeiten()
    {
        KompaktspieltageAnsichtViewModel ansicht = await AnsichtOeffnenAsync();
        Assert.Equal(string.Empty, ansicht.CursorText);
        ansicht.CursorBewegen(1, 1);
        Assert.Null(Rahmen(ansicht));

        ansicht.Bearbeiten = true;
        Assert.True(hf.KompaktMeldungenBearbeiten);
        Assert.False(hf.GenerierungCommand.CanExecute(null));
        Kompaktzelle start = Assert.IsType<Kompaktzelle>(Rahmen(ansicht));
        Assert.Equal(ansicht.Spalten[0].Name, start.Mannschaft);
        Assert.Equal(ansicht.Zeilen[0].Tag, start.Tag);
        Assert.NotEqual(string.Empty, ansicht.CursorText);

        // Am Rand bleibt der Rahmen stehen.
        ansicht.CursorBewegen(-3, -3);
        Assert.Same(start, Rahmen(ansicht));
        ansicht.CursorBewegen(1, 2);
        Kompaktzelle feld = Assert.IsType<Kompaktzelle>(Rahmen(ansicht));
        Assert.Equal(ansicht.Spalten[1].Name, feld.Mannschaft);
        Assert.Equal(ansicht.Zeilen[2].Tag, feld.Tag);
        ansicht.CursorBewegen(100, 100);
        Assert.Equal(ansicht.Spalten[^1].Name, Rahmen(ansicht)!.Mannschaft);
        Assert.Equal(ansicht.Zeilen[^1].Tag, Rahmen(ansicht)!.Tag);

        ansicht.ZelleWaehlenCommand.Execute(feld);
        await ansicht.FeldSetzenCommand.ExecuteAsync("A");
        Assert.True(Rahmen(ansicht)!.IstAuswaerts);
        Assert.False(ansicht.CursorHatTermin);

        // Ohne Heimtermin gibt es nichts zu speichern; unbekannte Tasten ändern nichts.
        await ansicht.TerminSpeichernCommand.ExecuteAsync(null);
        await ansicht.FeldSetzenCommand.ExecuteAsync("x");
        Assert.True(Rahmen(ansicht)!.IstAuswaerts);

        await ansicht.FeldSetzenCommand.ExecuteAsync("-");
        Assert.True(Rahmen(ansicht)!.IstNicht);
        await ansicht.FeldSetzenCommand.ExecuteAsync("h");
        Assert.True(Rahmen(ansicht)!.IstHeim);
        await ansicht.FeldSetzenCommand.ExecuteAsync("B");
        Assert.True(Rahmen(ansicht)!.IstBeides);
        Assert.True(ansicht.CursorHatTermin);
        Assert.NotEmpty(ansicht.CursorLokale);
        Assert.NotNull(ansicht.CursorLokal);

        ansicht.CursorZeit = "halb elf";
        await ansicht.TerminSpeichernCommand.ExecuteAsync(null);
        Assert.StartsWith("Bitte den Beginn als Uhrzeit eingeben", ansicht.Bearbeitungsmeldung, StringComparison.Ordinal);

        ansicht.CursorZeit = "9.30";
        ansicht.CursorZweiTische = true;
        await ansicht.TerminSpeichernCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, ansicht.Bearbeitungsmeldung);
        Assert.Equal("09:30", ansicht.CursorZeit);
        Assert.True(ansicht.CursorZweiTische);

        ansicht.CursorZeit = "10";
        await ansicht.TerminSpeichernCommand.ExecuteAsync(null);
        Assert.Equal("10:00", ansicht.CursorZeit);

        await ansicht.ZeitLoeschenCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, ansicht.CursorZeit);

        // Das Spiellokal gilt danach für alle Heimtermine der Mannschaft.
        Kompaktlokal lokal = ansicht.CursorLokale[^1];
        ansicht.CursorLokal = lokal;
        await ansicht.LokalFuerAlleCommand.ExecuteAsync(null);
        Assert.Equal(lokal.Nummer, ansicht.CursorLokal?.Nummer);

        // Schnelles Tippen: alle Eingaben kommen nacheinander an.
        await Task.WhenAll(ansicht.FeldSetzenCommand.ExecuteAsync("A"), ansicht.FeldSetzenCommand.ExecuteAsync("-"));
        Assert.True(Rahmen(ansicht)!.IstNicht);

        ansicht.Bearbeiten = false;
        Assert.False(hf.KompaktMeldungenBearbeiten);
        Assert.Equal(string.Empty, ansicht.CursorText);
        Assert.Null(Rahmen(ansicht));
    }

    /// <summary>Ein Tag als Zeile: außerhalb der Halbrunde abgelehnt, ein vorhandener Tag setzt nur den Rahmen.</summary>
    /// <returns>Der Testlauf.</returns>
    [Fact]
    public async Task Tag_anlegen()
    {
        KompaktspieltageAnsichtViewModel ansicht = await AnsichtOeffnenAsync();
        ansicht.NeuerTag = new DateTime(2020, 1, 5, 0, 0, 0, DateTimeKind.Unspecified);
        await ansicht.TagAnlegenCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, ansicht.Bearbeitungsmeldung);

        ansicht.Bearbeiten = true;
        await ansicht.TagAnlegenCommand.ExecuteAsync(null);
        Assert.Equal("Der 05.01.2020 liegt nicht in der geplanten Halbrunde oder ist spielfrei.", ansicht.Bearbeitungsmeldung);

        DateOnly letzter = ansicht.Zeilen[^1].Tag;
        ansicht.NeuerTag = letzter.ToDateTime(TimeOnly.MinValue);
        await ansicht.TagAnlegenCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, ansicht.Bearbeitungsmeldung);
        Assert.Null(ansicht.NeuerTag);
        Assert.Equal(letzter, Rahmen(ansicht)!.Tag);

        // Ein freier Samstag zwischen zwei Terminen wird eine neue Zeile.
        int vorher = ansicht.Zeilen.Count;
        DateOnly neu = Enumerable.Range(1, 60).Select(i => ansicht.Zeilen[0].Tag.AddDays(i))
            .First(t => t.DayOfWeek == DayOfWeek.Saturday && !ansicht.Zeilen.Any(z => z.Tag == t));
        ansicht.NeuerTag = neu.ToDateTime(TimeOnly.MinValue);
        await ansicht.TagAnlegenCommand.ExecuteAsync(null);
        if (ansicht.Bearbeitungsmeldung.Length == 0)
        {
            Assert.Equal(vorher + 1, ansicht.Zeilen.Count);
            Assert.Equal(neu, Rahmen(ansicht)!.Tag);
        }
    }

    /// <summary>
    /// Berechnen, anhalten (die bisher besten Pläne stehen zur Wahl), weiterrechnen, beenden; Pläne wählen, die Auswahl von
    /// Sonntag, Mannschaft und Spieltag, merken, drucken und exportieren.
    /// </summary>
    /// <returns>Der Testlauf.</returns>
    [Fact]
    public async Task Berechnen_anhalten_beenden_und_Plaene_nutzen()
    {
        KompaktspieltageAnsichtViewModel ansicht = await AnsichtOeffnenAsync();
        Assert.Equal("Noch kein Plan – ▶ berechnet Varianten", hf.Kompaktkacheln[0].Zusatz);
        o.Sammeln = true;
        Task lauf = hf.GenerierungCommand.ExecuteAsync(null);
        await WartenAsync(() => ansicht.Rechnet && ansicht.Fortschritt > 0);
        Assert.True(hf.StoppenCommand.CanExecute(null));

        // Anhalten: Der Zwischenstand steht zur Wahl, Weiterrechnen behält nur den gewählten Plan.
        await hf.GenerierungCommand.ExecuteAsync(null);
        o.Abarbeiten();
        Assert.True(hf.KompaktPausiert);
        Assert.True(ansicht.Angehalten);
        await hf.GenerierungCommand.ExecuteAsync(null);
        Assert.False(hf.KompaktPausiert);

        hf.StoppenCommand.Execute(null);
        await WartenAsync(() => lauf.IsCompleted);
        await lauf;
        o.Abarbeiten();
        Assert.False(ansicht.Rechnet);
        Assert.StartsWith("Kompaktspieltage: ", hf.Statuszeile, StringComparison.Ordinal);

        List<Kompaktplanwahl> varianten = ansicht.Plaene.Where(p => p.Variante is not null).ToList();
        Assert.NotEmpty(varianten);
        Kompaktplanwahl gewaehlt = Assert.Single(ansicht.Plaene, p => p.Gewaehlt);
        Assert.True(gewaehlt.KannMerken);
        Assert.Equal(gewaehlt.Titel, ansicht.Erlaeuterungstitel);
        Assert.NotEmpty(ansicht.Erlaeuterung);
        Assert.True(ansicht.HatPlan);
        Assert.NotEmpty(ansicht.Spieltage);
        Assert.NotEqual("–", hf.Kompaktkacheln[0].Wert);

        // Jede Variante lässt sich zeigen und erläutern.
        foreach (Kompaktplanwahl wahl in varianten)
        {
            ansicht.PlanWaehlenCommand.Execute(wahl);
            Assert.Equal(wahl.Titel, ansicht.Erlaeuterungstitel);
            Assert.NotEmpty(ansicht.Erlaeuterung);
        }

        AuswahlPruefen(ansicht);

        // Merken: Der Plan steht danach als gespeicherter Plan zur Wahl, auch nach erneutem Öffnen.
        ansicht.PlanWaehlenCommand.Execute(varianten[0]);
        o.Texteingabe = "Testplan";
        await ansicht.MerkenCommand.ExecuteAsync(null);
        o.Abarbeiten();
        Kompaktplanwahl gemerkt = Assert.Single(ansicht.Plaene, p => p.Gespeichert);
        Assert.Equal("Gemerkt: Testplan", gemerkt.Titel);
        Assert.False(gemerkt.KannMerken);
        ansicht.PlanWaehlenCommand.Execute(gemerkt);
        Assert.Equal(gemerkt.Titel, ansicht.Erlaeuterungstitel);

        await DruckenUndExportierenAsync(gemerkt.Titel);

        await hf.SchliessenCommand.ExecuteAsync(null);
        await hf.OeffnenCommand.ExecuteAsync(null);
        o.Abarbeiten();
        hf.KompaktspieltageCommand.Execute(null);
        KompaktspieltageAnsichtViewModel wieder = Assert.Single(Ansichten<KompaktspieltageAnsichtViewModel>());
        Assert.Contains(wieder.Plaene, p => p.Gespeichert && p.Titel == "Gemerkt: Testplan");
    }

    /// <summary>Ausschalten nimmt den Kompaktmodus zurück, Einschalten stellt ihn wieder her.</summary>
    /// <returns>Der Testlauf.</returns>
    [Fact]
    public async Task Aus_und_wieder_einschalten()
    {
        KompaktspieltageAnsichtViewModel ansicht = await AnsichtOeffnenAsync();
        Assert.True(hf.KompaktAktiv);
        Assert.False(hf.ZeigtGenerierung);

        await ansicht.AusschaltenCommand.ExecuteAsync(null);
        o.Abarbeiten();
        Assert.False(ansicht.Eingeschaltet);
        Assert.False(hf.KompaktAktiv);
        Assert.True(hf.ZeigtGenerierung);
        Assert.Empty(ansicht.Zeilen);
        Assert.Empty(ansicht.Spieltage);
        Assert.False(ansicht.HatPlan);

        await ansicht.EinschaltenCommand.ExecuteAsync(null);
        o.Abarbeiten();
        Assert.True(ansicht.Eingeschaltet);
        Assert.True(hf.KompaktAktiv);
        Assert.NotEmpty(ansicht.Zeilen);
        Assert.Contains(" mögliche Termine · ", ansicht.Kopfzeile, StringComparison.Ordinal);
        Assert.NotEmpty(ansicht.Mannschaftsnamen);
        Assert.InRange(ansicht.Kopfhoehe, 1, 1000);
    }

    private static Kompaktzelle? Rahmen(KompaktspieltageAnsichtViewModel ansicht) =>
        ansicht.Zeilen.SelectMany(z => z.Zellen).SingleOrDefault(z => z.IstCursor);

    /// <summary>Sonntag, Mannschaft und Spieltag wählen: Karte, km und Spieltage folgen; ein zweiter Klick zeigt wieder alle.</summary>
    private static void AuswahlPruefen(KompaktspieltageAnsichtViewModel ansicht)
    {
        Assert.Equal("Alle Fahrten der Halbrunde", ansicht.Kartentitel);
        Kompaktspieltagkarte karte = ansicht.Spieltage[0];
        ansicht.SpieltagWaehlenCommand.Execute(karte);
        Assert.StartsWith("Spieltag ", ansicht.Kartentitel, StringComparison.Ordinal);
        Assert.True(Assert.Single(ansicht.Spieltage, s => s.Gewaehlt).Nummer == karte.Nummer);
        ansicht.SpieltagWaehlenCommand.Execute(karte);
        Assert.Equal("Alle Fahrten der Halbrunde", ansicht.Kartentitel);

        Kompaktrasterzeile mitSpiel = ansicht.Zeilen.First(z => z.Zellen.Any(c => c.Spielt));
        ansicht.TagWaehlenCommand.Execute(mitSpiel);
        Assert.StartsWith("Fahrten am ", ansicht.Kartentitel, StringComparison.Ordinal);
        Assert.True(ansicht.Zeilen.Single(z => z.Tag == mitSpiel.Tag).Gewaehlt);
        Kompaktrasterzeile? ohneSpiel = ansicht.Zeilen.FirstOrDefault(z => !z.Zellen.Any(c => c.Spielt));
        if (ohneSpiel is not null)
        {
            ansicht.TagWaehlenCommand.Execute(ohneSpiel);
            Assert.StartsWith("Am ", ansicht.Kartentitel, StringComparison.Ordinal);
            Assert.EndsWith(" kein Spieltag", ansicht.Kartentitel, StringComparison.Ordinal);
            ansicht.TagWaehlenCommand.Execute(ohneSpiel);
        }
        else
        {
            ansicht.TagWaehlenCommand.Execute(mitSpiel);
        }

        string team = ansicht.KmZeilen[0].Name;
        ansicht.MannschaftWaehlenCommand.Execute(team);
        Assert.StartsWith("Fahrten von " + team, ansicht.Kartentitel, StringComparison.Ordinal);
        Assert.True(ansicht.Spalten.Single(s => s.Name == team).Gewaehlt);
        Assert.Contains(ansicht.Spieltage, s => s.Gewaehlt);
        ansicht.MannschaftWaehlenCommand.Execute(team);
        Assert.Equal("Alle Fahrten der Halbrunde", ansicht.Kartentitel);

        // Ohne Auswahl ändert ein leerer Klick nichts.
        ansicht.MannschaftWaehlenCommand.Execute(null);
        Assert.Equal("Alle Fahrten der Halbrunde", ansicht.Kartentitel);
        Assert.InRange(ansicht.Spieltagbreite, 310, 560);
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

    /// <summary>Druck im Kompaktmodus (Übersicht für die Vereine) und CSV-Export mit den Spieltagen des gewählten Plans.</summary>
    private async Task DruckenUndExportierenAsync(string plan)
    {
        DruckauswahlViewModel? auswahl = null;
        o.Druckauswahl = a =>
        {
            auswahl = a;
            return true;
        };
        o.DateiZumSpeichern = Path.Combine(ordner, "kompakt.pdf");
        await hf.DruckenCommand.ExecuteAsync(null);
        o.Abarbeiten();
        Assert.NotNull(auswahl);
        Assert.True(auswahl.IstKompakt);
        Assert.Equal(plan, auswahl.Planname);
        Druckdokument dokument = Assert.IsType<Druckdokument>(o.Ausdruck);
        Assert.Equal(["Meldungen und Plan", "Alle Fahrten", "Kompaktspieltage"], dokument.Abschnitte.Take(3).Select(a => a.Titel));

        o.Antwort = true;
        o.DateiZumSpeichern = Path.Combine(ordner, "kompakt.csv");
        await hf.CsvExportierenCommand.ExecuteAsync(null);
        o.Abarbeiten();
        Assert.True(File.Exists(o.DateiZumSpeichern));
        Assert.True((await File.ReadAllLinesAsync(o.DateiZumSpeichern)).Length > 1);
    }

    private async Task<KompaktspieltageAnsichtViewModel> AnsichtOeffnenAsync()
    {
        await hf.OeffnenCommand.ExecuteAsync(null);
        if (hf.Einrichtung is { } einrichtung)
        {
            await einrichtung.AbschliessenCommand.ExecuteAsync(null);
        }

        Assert.True(hf.ZeigtKompaktspieltage);
        hf.KompaktspieltageCommand.Execute(null);
        KompaktspieltageAnsichtViewModel ansicht = Assert.Single(Ansichten<KompaktspieltageAnsichtViewModel>());
        Assert.True(ansicht.Eingeschaltet);
        Assert.NotEmpty(ansicht.Zeilen);
        return ansicht;
    }

    private List<T> Ansichten<T>()
        where T : class
    {
        var liste = new List<T>();
        Sammeln(hf.Layout, liste);
        return liste;
    }

    /// <summary>Wartet (höchstens eine Minute), bis die Bedingung gilt, und führt dabei die gesammelten Oberflächenaufrufe aus.</summary>
    private async Task WartenAsync(Func<bool> bedingung)
    {
        for (int i = 0; i < 600 && !bedingung(); i++)
        {
            await Task.Delay(100);
            o.Abarbeiten();
        }

        Assert.True(bedingung());
    }
}
