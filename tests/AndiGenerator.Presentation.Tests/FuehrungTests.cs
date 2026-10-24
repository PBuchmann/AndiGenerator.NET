// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.RegularExpressions;

namespace AndiGenerator.Presentation.Tests;

/// <summary>
/// Führung „Schritt für Schritt“: passende Führung je Lage, Einstieg beim aktuellen Schritt, Weiter, Zurück, Zeigen, Schritte
/// mit Bedingung, Hinweis in der Statuszeile; die Anleitung nennt dieselben Schritte.
/// </summary>
public sealed class FuehrungTests : IDisposable
{
    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-fuehrung-" + Guid.NewGuid().ToString("N"));
    private readonly TestOberflaeche o = new();
    private readonly List<string> gezeigt = [];
    private readonly HauptfensterViewModel hf;

    public FuehrungTests()
    {
        Directory.CreateDirectory(Path.Combine(ordner, "daten"));
        string datei = Path.Combine(ordner, "daten", "staffel.xml");
        File.Copy(Path.Combine(Testdaten.Referenz, "datendialoge", "D01_Allgemein.xml"), datei);
        o.DateiZumOeffnen = datei;
        hf = new HauptfensterViewModel(o, () => throw new InvalidOperationException("Keine Fenster im Test"), Path.Combine(ordner, "basis"), null);
        hf.ZielHervorheben += (_, ziel) => gezeigt.Add(ziel);
    }

    public static TheoryData<Fuehrungsart> Arten { get; } = [Fuehrungsart.Kostenoptimierung, Fuehrungsart.Automodus, Fuehrungsart.Kompaktspieltage];

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

    [Theory]
    [MemberData(nameof(Arten))]
    public void Jeder_Schritt_zeigt_auf_ein_bekanntes_Ziel_und_einen_Abschnitt_der_Anleitung(Fuehrungsart art)
    {
        string anleitung = File.ReadAllText(Path.Combine(Testdaten.Referenz, "..", "..", "Doku", "ANLEITUNG.md"));
        IReadOnlyList<Fuehrungsschritt> schritte = Fuehrungen.Schritte(art);

        Assert.InRange(schritte.Count, 8, 12);
        Assert.All(schritte, s => Assert.Contains(s.Ziel!, Fuehrungsziel.Alle));
        Assert.All(schritte, s => Assert.Matches(new Regex("^### " + Regex.Escape(s.Abschnitt) + " ", RegexOptions.Multiline), anleitung));

        // Die Anleitung nennt dieselben Schritte in derselben Reihenfolge, mit demselben Text.
        string kopf = "#### " + Fuehrungen.Titel(art) + "\n";
        int beginn = anleitung.IndexOf(kopf, StringComparison.Ordinal);
        Assert.True(beginn >= 0, kopf);
        string abschnitt = anleitung[(beginn + kopf.Length)..];
        abschnitt = abschnitt[..abschnitt.IndexOf("\n#", StringComparison.Ordinal)];
        string[] zeilen = abschnitt.Split('\n').Where(z => Regex.IsMatch(z, @"^\d+\. ")).ToArray();
        Assert.Equal(schritte.Select((s, i) => $"{i + 1}. **{s.Titel}** – {s.Text} (Abschnitt {s.Abschnitt})"), zeilen);
    }

    [Fact]
    public async Task Kostenoptimierung_Schritt_fuer_Schritt()
    {
        Assert.False(hf.FuehrungCommand.CanExecute(null));
        await OeffnenAsync();
        Assert.True(hf.FuehrungCommand.CanExecute(null));
        Assert.Equal("Schritt für Schritt: Kostenoptimierung – nächster Schritt 1: Verfahren und Ausgangsplan wählen", hf.FuehrungsHinweis);

        hf.FuehrungCommand.Execute(null);
        FuehrungViewModel f = Assert.IsType<FuehrungViewModel>(hf.Fuehrung);
        Assert.True(hf.ZeigtFuehrung);
        Assert.False(hf.HatFuehrungsHinweis);
        Assert.Equal(Fuehrungsart.Kostenoptimierung, f.Art);
        Assert.Equal("Schritt für Schritt: Kostenoptimierung", f.Titel);
        Assert.Equal("Schritt 1 von 10", f.FortschrittText);
        Assert.Equal([Fuehrungsziel.Verfahren], gezeigt);
        Assert.False(f.ZurueckCommand.CanExecute(null));
        Assert.Equal("Mehr dazu: Anleitung, Abschnitt 5.1", f.Aktuell.Verweis);

        f.WeiterCommand.Execute(null);
        Assert.Equal("Generierung starten", f.Aktuell.Titel);
        Assert.Equal("✓", f.Schritte[0].Marke);
        Assert.Equal("2", f.Aktuell.Marke);

        // Starten hakt den Schritt selbst ab; die Führung zeigt dann die Kacheln.
        gezeigt.Clear();
        await PlanErrechnenAsync();
        Assert.Equal("Kacheln lesen", f.Aktuell.Titel);
        Assert.True(f.Schritte[1].IstErledigt);
        Assert.Contains(Fuehrungsziel.Kacheln, gezeigt);

        // Zeigen öffnet die Ansicht, in der das Ziel steht.
        f.WeiterCommand.Execute(null);
        Assert.Equal("Teuerste Kosten finden", f.Aktuell.Titel);
        Assert.Equal("Kosten", hf.AktiveAnsicht);
        Assert.Equal(Fuehrungsziel.KostenMatrix, gezeigt[^1]);

        // Zurück bleibt stehen, auch wenn die Bedingung des Schritts schon gilt.
        f.ZurueckCommand.Execute(null);
        f.ZurueckCommand.Execute(null);
        Assert.Equal("Generierung starten", f.Aktuell.Titel);
        Assert.False(f.Aktuell.IstErledigt);
        o.Abarbeiten();
        Assert.Equal("Generierung starten", f.Aktuell.Titel);

        // Klick auf einen Schritt wählt ihn.
        f.Schritte[^1].WaehlenCommand.Execute(null);
        Assert.Equal("Fertig", f.WeiterText);
        Assert.Equal(Fuehrungsziel.NavExport, gezeigt[^1]);
        f.WeiterCommand.Execute(null);
        Assert.Null(hf.Fuehrung);

        // Der Hinweis nennt nun den Schritt, an dem die Führung zuletzt stand; F1 öffnet sie dort wieder.
        Assert.EndsWith("nächster Schritt 10: Exportieren und drucken", hf.FuehrungsHinweis, StringComparison.Ordinal);
        hf.FuehrungCommand.Execute(null);
        Assert.Equal("Exportieren und drucken", hf.Fuehrung!.Aktuell.Titel);
        hf.FuehrungCommand.Execute(null);
        Assert.Null(hf.Fuehrung);
        hf.StoppenCommand.Execute(null);

        // Schließen der Staffel beendet die Führung und vergisst ihren Stand.
        hf.FuehrungCommand.Execute(null);
        await hf.SchliessenCommand.ExecuteAsync(null);
        Assert.Null(hf.Fuehrung);
        Assert.False(hf.HatFuehrungsHinweis);
    }

    [Fact]
    public async Task Automodus_beginnt_nach_der_Wahl_des_Verfahrens()
    {
        await OeffnenAsync();
        hf.AutomodusCommand.Execute(null);
        Assert.Equal(Fuehrungsart.Automodus, hf.PassendeFuehrung);
        Assert.EndsWith("nächster Schritt 2: Einteilung prüfen", hf.FuehrungsHinweis, StringComparison.Ordinal);

        hf.FuehrungCommand.Execute(null);
        FuehrungViewModel f = Assert.IsType<FuehrungViewModel>(hf.Fuehrung);
        Assert.Equal(Fuehrungsart.Automodus, f.Art);
        Assert.True(f.Schritte[0].IstErledigt);
        Assert.Equal("Einteilung prüfen", f.Aktuell.Titel);
        f.ZeigenCommand.Execute(null);
        Assert.Equal("Qualitaet", hf.AktiveAnsicht);
        Assert.Equal(Fuehrungsziel.QualitaetTabelle, gezeigt[^1]);

        f.WeiterCommand.Execute(null);
        Assert.Equal("Generierung starten", f.Aktuell.Titel);
        try
        {
            await PlanErrechnenAsync();
            Assert.True(f.Schritte[2].IstErledigt);
            Assert.Equal("Phasen verfolgen", f.Aktuell.Titel);
        }
        finally
        {
            hf.StoppenCommand.Execute(null);
        }
    }

    [Fact]
    public async Task Kompaktspieltage_Schritt_fuer_Schritt()
    {
        o.DateiZumOeffnen = Kompaktklasse.Vorbereiten(ordner);
        await OeffnenAsync();
        Assert.Equal(Fuehrungsart.Kompaktspieltage, hf.PassendeFuehrung);
        hf.FuehrungCommand.Execute(null);
        FuehrungViewModel f = Assert.IsType<FuehrungViewModel>(hf.Fuehrung);

        // Im Kompaktmodus ist die Ansicht schon offen: Die Führung beginnt beim zweiten Schritt.
        Assert.True(f.Schritte[0].IstErledigt);
        Assert.Equal("Meldungen und Hinweise prüfen", f.Aktuell.Titel);
        Assert.Equal(Fuehrungsziel.KompaktRaster, gezeigt[^1]);
        f.WeiterCommand.Execute(null);
        f.WeiterCommand.Execute(null);
        Assert.Equal("Pläne berechnen", f.Aktuell.Titel);

        // Während der Berechnung bleibt der Schritt stehen; liegen die Pläne vor, geht es zum Vergleich.
        o.Sammeln = true;
        Task lauf = hf.GenerierungCommand.ExecuteAsync(null);
        for (int i = 0; i < 600 && !(hf.KompaktRechnet && hf.Statuszeile.Contains(" – ", StringComparison.Ordinal)); i++)
        {
            await Task.Delay(100);
            o.Abarbeiten();
        }

        Assert.Equal("Pläne berechnen", f.Aktuell.Titel);
        hf.StoppenCommand.Execute(null);
        for (int i = 0; i < 600 && !lauf.IsCompleted; i++)
        {
            await Task.Delay(100);
            o.Abarbeiten();
        }

        await lauf;
        o.Abarbeiten();
        Assert.Equal("Pläne vergleichen", f.Aktuell.Titel);

        // Merken hakt „Plan merken“ ab, wenn die Führung dort steht.
        f.Schritte.Single(s => s.Titel == "Plan merken").WaehlenCommand.Execute(null);
        f.WeiterCommand.Execute(null);
        Assert.Equal("Drucken und exportieren", f.Aktuell.Titel);
        o.Texteingabe = "Gemerkt";
        await hf.PlanMerkenCommand.ExecuteAsync(null);
        o.Abarbeiten();
        hf.FuehrungCommand.Execute(null);
        Assert.EndsWith("nächster Schritt 9: Drucken und exportieren", hf.FuehrungsHinweis, StringComparison.Ordinal);
    }

    private async Task OeffnenAsync()
    {
        await hf.OeffnenCommand.ExecuteAsync(null);
        if (hf.Einrichtung is { } einrichtung)
        {
            await einrichtung.AbschliessenCommand.ExecuteAsync(null);
        }

        Assert.True(hf.ZeigtArbeitsbereich);
    }

    /// <summary>Startet die Generierung und wartet, bis sie einen Plan gemeldet hat.</summary>
    private async Task PlanErrechnenAsync()
    {
        o.Sammeln = true;
        await hf.GenerierungCommand.ExecuteAsync(null);
        for (int i = 0; i < 300 && !hf.PlanMerkenCommand.CanExecute(null); i++)
        {
            await Task.Delay(100);
            o.Abarbeiten();
        }

        Assert.True(hf.PlanMerkenCommand.CanExecute(null));
    }
}
