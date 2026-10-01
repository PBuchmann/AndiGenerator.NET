// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using AndiGenerator.Presentation.Tests;
using AndiGenerator.UI.Ansichten;
using Avalonia.Controls;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Dock.Avalonia.Controls;
using Xunit.Abstractions;

namespace AndiGenerator.UI.Tests;

/// <summary>
/// Rauchtest des Hauptfensters: Staffel öffnen, Einrichtung abschließen und jede Ansicht einmal mit dem Plan aus click-TT
/// anzeigen und zeichnen. Die Inhalte prüfen die ViewModel-Tests; hier geht es um XAML, Bindungen, Vorlagen und Zeichnen.
/// </summary>
[Collection(OberflaechenSammlung.Name)]
public sealed class HauptfensterTests : IDisposable
{
    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-ui-" + Guid.NewGuid().ToString("N"));
    private readonly Oberflaeche oberflaeche;
    private readonly ITestOutputHelper ausgabe;

    public HauptfensterTests(Oberflaeche oberflaeche, ITestOutputHelper ausgabe)
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
    public Task Alle_Ansichten_werden_ohne_Fehler_angezeigt() => oberflaeche.Ausfuehren(async () =>
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
            Oberflaeche.Zeichnen(fenster);
            await modell.OeffnenCommand.ExecuteAsync(null);
            o.Abarbeiten();
            Oberflaeche.Zeichnen(fenster);
            Assert.NotEmpty(fenster.GetVisualDescendants().OfType<EinrichtungAnsicht>());

            EinrichtungViewModel einrichtung = Assert.IsType<EinrichtungViewModel>(modell.Einrichtung);
            await einrichtung.AbschliessenCommand.ExecuteAsync(null);
            o.Abarbeiten();
            Oberflaeche.Zeichnen(fenster);

            Zeigen<KostenAnsicht>(fenster, o, modell.KostenansichtCommand);
            Zeigen<QualitaetAnsicht>(fenster, o, modell.QualitaetsansichtCommand);
            Zeigen<MeldungenAnsicht>(fenster, o, modell.MeldungsansichtCommand);
            Zeigen<TerminplanAnsicht>(fenster, o, modell.TerminplanansichtCommand);
            Zeigen<DiagrammAnsicht>(fenster, o, modell.DiagrammansichtCommand);
            Zeigen<TerminwunschAnsicht>(fenster, o, modell.TerminwunschansichtCommand);
            Zeigen<NachbarterminAnsicht>(fenster, o, modell.NachbarterminansichtCommand);
            Zeigen<OptionenAnsicht>(fenster, o, modell.EinstellungenCommand);
        }
        finally
        {
            fenster.Close();
        }

        Assert.Empty(o.Meldungen);
        oberflaeche.KeineFehler(ausgabe);
    });

    private static void Zeigen<T>(Window fenster, TestOberflaeche o, IRelayCommand befehl)
        where T : Control
    {
        Assert.True(befehl.CanExecute(null));
        befehl.Execute(null);
        o.Abarbeiten();
        Oberflaeche.Zeichnen(fenster);
        T ansicht = Assert.Single(fenster.GetVisualDescendants().OfType<T>());
        if (ansicht.DataContext is AnsichtViewModel plan)
        {
            plan.Quelle = PlanQuelle.ClickTt;
            o.Abarbeiten();
            Oberflaeche.Zeichnen(fenster);
        }
    }
}
