// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using AndiGenerator.Presentation.Tests;
using AndiGenerator.UI.Ansichten;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Xunit.Abstractions;

namespace AndiGenerator.UI.Tests;

/// <summary>
/// Führung „Schritt für Schritt“ in der Oberfläche: Jedes Ziel gibt es (mit seiner Ansicht), die Begleitleiste erscheint und der
/// Scheinwerfer richtet sich auf das gezeigte Element.
/// </summary>
[Collection(OberflaechenSammlung.Name)]
public sealed class FuehrungsTests : IDisposable
{
    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-fuehrung-ui-" + Guid.NewGuid().ToString("N"));
    private readonly Oberflaeche oberflaeche;
    private readonly ITestOutputHelper ausgabe;

    public FuehrungsTests(Oberflaeche oberflaeche, ITestOutputHelper ausgabe)
    {
        this.oberflaeche = oberflaeche;
        this.ausgabe = ausgabe;
        oberflaeche.Protokoll.Leeren();
    }

    public void Dispose()
    {
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
    public Task Jedes_Ziel_gibt_es_und_der_Scheinwerfer_zeigt_es() => oberflaeche.Ausfuehren(async () =>
    {
        Directory.CreateDirectory(Path.Combine(ordner, "daten"));
        string datei = Path.Combine(ordner, "daten", "staffel.xml");
        File.Copy(Path.Combine(Testdaten.Referenz, "datendialoge", "D01_Allgemein.xml"), datei);
        var o = new TestOberflaeche { DateiZumOeffnen = datei, Sammeln = true };
        using var modell = new HauptfensterViewModel(o, () => new HostWindow(), Path.Combine(ordner, "basis"), null);
        var fenster = new HauptfensterView { DataContext = modell, Width = 1400, Height = 900 };
        fenster.Show();
        try
        {
            await modell.OeffnenCommand.ExecuteAsync(null);
            if (modell.Einrichtung is { } einrichtung)
            {
                await einrichtung.AbschliessenCommand.ExecuteAsync(null);
            }

            o.Abarbeiten();
            Oberflaeche.Zeichnen(fenster);
            Assert.False(Leiste(fenster).IsEffectivelyVisible);

            // Die Begleitleiste erscheint mit F1; der Scheinwerfer zeigt das Ziel des ersten Schritts.
            modell.FuehrungCommand.Execute(null);
            Oberflaeche.Zeichnen(fenster);
            Dispatcher.UIThread.RunJobs();
            Assert.True(Leiste(fenster).IsEffectivelyVisible);
            Assert.NotNull(fenster.Licht.Bereich);
            fenster.Licht.Ausschalten();
            Assert.Null(fenster.Licht.Bereich);

            // Normale Runden: jedes Ziel (B-Varianten nur, wenn HiGHS welche hat; Kompaktspieltage unten).
            modell.KostenansichtCommand.Execute(null);
            o.Abarbeiten();
            Oberflaeche.Zeichnen(fenster);
            ((AnsichtViewModel)fenster.GetVisualDescendants().OfType<KostenAnsicht>().Single().DataContext!).Quelle = PlanQuelle.ClickTt;
            Pruefen(fenster, o, Fuehrungsziel.KostenMatrix);
            modell.QualitaetsansichtCommand.Execute(null);
            o.Abarbeiten();
            Oberflaeche.Zeichnen(fenster);
            ((AnsichtViewModel)fenster.GetVisualDescendants().OfType<QualitaetAnsicht>().Single().DataContext!).Quelle = PlanQuelle.ClickTt;
            Pruefen(fenster, o, Fuehrungsziel.QualitaetTabelle);
            foreach (string ziel in Fuehrungsziel.Alle.Where(z => !z.Contains("Kompakt", StringComparison.Ordinal) && z is not Fuehrungsziel.BVarianten
                and not Fuehrungsziel.KostenMatrix and not Fuehrungsziel.QualitaetTabelle and not Fuehrungsziel.Stopp))
            {
                Pruefen(fenster, o, ziel);
            }

            // Fehlt ein Ziel, zeigt der Scheinwerfer seinen Ersatz: ohne B-Varianten die Kachel von HiGHS, ohne Generierung den Startknopf.
            Pruefen(fenster, o, Fuehrungsziel.BVarianten);
            Pruefen(fenster, o, Fuehrungsziel.Stopp);
            Assert.False(fenster.ScheinwerferRichten("Gibt es nicht"));

            // Stopp gibt es nur während der Generierung.
            await modell.GenerierungCommand.ExecuteAsync(null);
            Pruefen(fenster, o, Fuehrungsziel.Stopp);
            modell.StoppenCommand.Execute(null);

            // Kompaktspieltage.
            modell.KompaktspieltageCommand.Execute(null);
            o.Abarbeiten();
            Oberflaeche.Zeichnen(fenster);
            var kompakt = (KompaktspieltageAnsichtViewModel)fenster.GetVisualDescendants().OfType<KompaktspieltageAnsicht>().Single().DataContext!;
            await kompakt.EinschaltenCommand.ExecuteAsync(null);
            foreach (string ziel in Fuehrungsziel.Alle.Where(z => z.Contains("Kompakt", StringComparison.Ordinal)))
            {
                Pruefen(fenster, o, ziel);
            }

            // Die Führung passt sich an: im Kompaktmodus die Kompaktspieltage.
            modell.FuehrungCommand.Execute(null);
            modell.FuehrungCommand.Execute(null);
            Oberflaeche.Zeichnen(fenster);
            Assert.Equal(Fuehrungsart.Kompaktspieltage, modell.Fuehrung?.Art);
            await kompakt.AusschaltenCommand.ExecuteAsync(null);
        }
        finally
        {
            fenster.Close();
        }

        oberflaeche.KeineFehler(ausgabe);
    });

    private static FuehrungLeiste Leiste(HauptfensterView fenster) => fenster.GetVisualDescendants().OfType<FuehrungLeiste>().Single();

    private static void Pruefen(HauptfensterView fenster, TestOberflaeche o, string ziel)
    {
        o.Abarbeiten();
        Oberflaeche.Zeichnen(fenster);
        Assert.True(fenster.ScheinwerferRichten(ziel), "Ziel fehlt in der Oberfläche: " + ziel);
        Assert.NotNull(fenster.Licht.Bereich);
        fenster.Licht.Ausschalten();
    }
}
