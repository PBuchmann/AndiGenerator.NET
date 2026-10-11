// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Presentation;
using AndiGenerator.Presentation.Tests;
using AndiGenerator.UI.Ansichten;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Dock.Avalonia.Controls;
using Xunit.Abstractions;

namespace AndiGenerator.UI.Tests;

/// <summary>
/// Bedienung mit der Maus und die übrigen Dialoge: Bearbeitungsdialoge der Spielplandaten, Rückfrage beim Sichern,
/// Ziehen in Setzliste und Wunschterminen, Leitstand und Verlauf während einer Generierung.
/// </summary>
[Collection(OberflaechenSammlung.Name)]
public sealed class BedienungTests : IDisposable
{
    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-bedienung-" + Guid.NewGuid().ToString("N"));
    private readonly Oberflaeche oberflaeche;
    private readonly ITestOutputHelper ausgabe;

    public BedienungTests(Oberflaeche oberflaeche, ITestOutputHelper ausgabe)
    {
        this.oberflaeche = oberflaeche;
        this.ausgabe = ausgabe;
        oberflaeche.Protokoll.Leeren();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(ordner))
            {
                Directory.Delete(ordner, recursive: true);
            }
        }
        catch (IOException)
        {
            // Aufräumen ist nicht Teil des Tests.
        }
    }

    [Fact]
    public Task Die_Bearbeitungsdialoge_der_Spielplandaten_werden_ohne_Fehler_angezeigt() => oberflaeche.Ausfuehren(async () =>
    {
        using var a = new Arbeitsplatz("datendialoge/D01_Allgemein.xml");
        var gezeigt = new List<string>();
        a.Oberflaeche.Mannschaft = d => Anzeigen(new Mannschaftsdialog(d), gezeigt);
        a.Oberflaeche.Auswaertskoppel = d => Anzeigen(new Auswaertskoppeldialog(d), gezeigt);
        a.Oberflaeche.Pflichtspielzeit = d => Anzeigen(new Pflichtspielzeitdialog(d), gezeigt);
        a.Oberflaeche.Vorgabespiel = d => Anzeigen(new Vorgabespieldialog(d), gezeigt);
        a.Oberflaeche.Nachbarspiel = d => Anzeigen(new Nachbarspieldialog(d), gezeigt);
        a.Oberflaeche.Nachbarmannschaft = async d =>
        {
            d.Name = "Testmannschaft";
            d.Nummer = 1;
            d.Art = "Herren";
            var fenster = new Nachbarmannschaftsdialog(d);
            fenster.Show();
            Oberflaeche.Zeichnen(fenster);
            await d.NeuCommand.ExecuteAsync(null);
            Oberflaeche.Zeichnen(fenster);
            fenster.Close();
            gezeigt.Add(nameof(Nachbarmannschaftsdialog));
            return false;
        };

        await a.Seite<MannschaftenSeiteViewModel>().NeuCommand.ExecuteAsync(null);
        AuswaertskoppelnSeiteViewModel koppeln = a.Seite<AuswaertskoppelnSeiteViewModel>();
        await Arbeitsplatz.Detail<AuswaertskoppelDetailViewModel>(koppeln, koppeln.Namen[0]).NeuCommand.ExecuteAsync(null);
        NachbarmannschaftenSeiteViewModel nachbarn = a.Seite<NachbarmannschaftenSeiteViewModel>();
        await Arbeitsplatz.Detail<NachbarmannschaftDetailViewModel>(nachbarn, nachbarn.Namen[0]).NeuCommand.ExecuteAsync(null);
        await a.Seite<PflichtspieltageSeiteViewModel>().NeuCommand.ExecuteAsync(null);
        await a.Seite<VorgabespieleSeiteViewModel>().NeuCommand.ExecuteAsync(null);

        string[] erwartet =
        [
            nameof(Mannschaftsdialog), nameof(Auswaertskoppeldialog), nameof(Nachbarspieldialog), nameof(Nachbarmannschaftsdialog),
            nameof(Pflichtspielzeitdialog), nameof(Vorgabespieldialog),
        ];
        Assert.Equal(erwartet, gezeigt);
        oberflaeche.KeineFehler(ausgabe);
    });

    [Fact]
    public Task Die_Rueckfrage_beim_Sichern_zeigt_beide_Plaene() => oberflaeche.Ausfuehren(() =>
    {
        using var a = new Arbeitsplatz("datendialoge/D01_Allgemein.xml");
        Planbewertung bewertung = Referenzbewertung.Bewerten(a.Sitzung.Staffel, a.Sitzung.Optionen);
        var vergleich = new Planvergleich("Die Staffel wird geschlossen.", bewertung, bewertung, Stufeneinteilung.Standard, new DateTime(2026, 10, 12, 9, 30, 0, DateTimeKind.Local));
        var fenster = new Sicherungsdialog(vergleich);
        fenster.Show();
        Oberflaeche.Zeichnen(fenster);
        fenster.Close();
        oberflaeche.KeineFehler(ausgabe);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Die_Setzliste_laesst_sich_am_Griff_umordnen() => oberflaeche.Ausfuehren(() =>
    {
        using var a = new Arbeitsplatz("datendialoge/D01_Allgemein.xml");
        var fenster = new Datendialog(a.Dialog);
        fenster.Show();
        try
        {
            SetzlistenSeiteViewModel seite = a.Dialog.Seiten.OfType<SetzlistenSeiteViewModel>().Single();
            Seitenliste(fenster).SelectedItem = seite;
            seite.Aktiv = true;
            Oberflaeche.Zeichnen(fenster);
            string erste = seite.Reihenfolge[0];
            int anzahl = seite.Reihenfolge.Count;

            List<Border> griffe = Sichtbare(fenster, "griff");
            List<Border> zeilen = Sichtbare(fenster, "setzzeile");
            Point von = Punkt(griffe[0], fenster, 0.5);
            Point nach = Punkt(zeilen[2], fenster, 0.8);
            fenster.MouseDown(von, MouseButton.Left);
            fenster.MouseMove(new Point(von.X, von.Y + 10));
            Oberflaeche.Zeichnen(fenster);
            fenster.MouseMove(nach);
            Oberflaeche.Zeichnen(fenster);
            fenster.MouseUp(nach, MouseButton.Left);
            Oberflaeche.Zeichnen(fenster);

            Assert.Equal(anzahl, seite.Reihenfolge.Count);
            Assert.NotEqual(erste, seite.Reihenfolge[0]);
            Assert.Contains(erste, seite.Reihenfolge);
            Assert.True(seite.Bearbeitet);
        }
        finally
        {
            fenster.Close();
        }

        oberflaeche.KeineFehler(ausgabe);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Ein_Wunschtermin_laesst_sich_auf_einen_anderen_Tag_ziehen() => oberflaeche.Ausfuehren(() =>
    {
        using var a = new Arbeitsplatz("datendialoge/D01_Allgemein.xml");
        var fenster = new Datendialog(a.Dialog);
        fenster.Show();
        try
        {
            WunschtermineSeiteViewModel seite = a.Dialog.Seiten.OfType<WunschtermineSeiteViewModel>().Single();
            Seitenliste(fenster).SelectedItem = seite;
            seite.Mannschaft = Namen.RheinduerkheimIV;
            Oberflaeche.Zeichnen(fenster);

            var rahmen = new Rect(fenster.ClientSize);
            List<(Border Karte, Point Mitte)> karten = Sichtbare(fenster, "kaertchen")
                .Where(k => k.DataContext is WunschterminZelle)
                .Select(k => (k, Punkt(k, fenster, 0.5)))
                .Where(k => rahmen.Contains(k.Item2))
                .ToList();
            (Border quelle, Point von) = karten.First(k => ((WunschterminZelle)k.Karte.DataContext!).HatTermin);
            (Border ziel, Point nach) = karten.First(k => ((WunschterminZelle)k.Karte.DataContext!).IstLeer);
            var vonZelle = (WunschterminZelle)quelle.DataContext!;
            var nachZelle = (WunschterminZelle)ziel.DataContext!;
            string text = vonZelle.Text;

            fenster.MouseDown(von, MouseButton.Left);
            fenster.MouseMove(new Point(von.X + 12, von.Y + 12), RawInputModifiers.LeftMouseButton);
            Oberflaeche.Zeichnen(fenster);
            Assert.True(vonZelle.IstQuelle, "Das Ziehen hat nicht begonnen.");

            // Das Anfassen setzt den Fokus auf die Seite; dabei kann der Bildlauf springen. Deshalb das Ziel neu bestimmen.
            nach = Punkt(ziel, fenster, 0.5);
            fenster.MouseMove(nach, RawInputModifiers.LeftMouseButton);
            Oberflaeche.Zeichnen(fenster);
            Assert.True(nachZelle.IstZiel, "Das Ziel wurde nicht erkannt.");
            fenster.MouseUp(nach, MouseButton.Left);
            Oberflaeche.Zeichnen(fenster);

            Assert.Equal(text, nachZelle.Text);
            Assert.True(vonZelle.IstLeer);
            Assert.DoesNotContain("Wunschtermin", a.Oberflaeche.Aufrufe);
        }
        finally
        {
            fenster.Close();
        }

        oberflaeche.KeineFehler(ausgabe);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Leitstand_und_Verlauf_zeigen_die_laufende_Generierung() => oberflaeche.Ausfuehren(async () =>
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
            o.Abarbeiten();
            if (modell.Einrichtung is { } einrichtung)
            {
                await einrichtung.AbschliessenCommand.ExecuteAsync(null);
                o.Abarbeiten();
            }

            // Kostenoptimierung: Leitstand mit Spezial-Insel, Verlauf mit Kosten.
            modell.KostenoptimierungCommand.Execute(null);
            await modell.GenerierungCommand.ExecuteAsync(null);
            await WartenAsync(o, fenster, TimeSpan.FromSeconds(4), () => false);
            Zeigen<MaschinenraumAnsicht>(fenster, o, modell.MaschinenraumCommand);
            await WartenAsync(o, fenster, TimeSpan.FromSeconds(2), () => false);
            Zeigen<VerlaufAnsicht>(fenster, o, modell.VerlaufCommand);
            Ueberfahren<Verlaufsdiagramm>(fenster);

            // Automodus: Leitstand mit Gewichten, Verlauf mit Verstößen, Phasen und davor den Kosten.
            modell.AutomodusCommand.Execute(null);
            await WartenAsync(o, fenster, TimeSpan.FromSeconds(20), () => modell.Anzeige.Phase.Length > 0);
            await WartenAsync(o, fenster, TimeSpan.FromSeconds(2), () => false);
            Zeigen<MaschinenraumAnsicht>(fenster, o, modell.MaschinenraumCommand);
            Ueberfahren<Maschinenschaubild>(fenster);
            Zeigen<VerlaufAnsicht>(fenster, o, modell.VerlaufCommand);
            Ueberfahren<Verlaufsdiagramm>(fenster);

            modell.PauseCommand.Execute(null);
            o.Abarbeiten();
            Oberflaeche.Zeichnen(fenster);
            modell.StoppenCommand.Execute(null);
            o.Abarbeiten();
        }
        finally
        {
            fenster.Close();
        }

        oberflaeche.KeineFehler(ausgabe);
    });

    private static bool Anzeigen(Window dialog, List<string> gezeigt)
    {
        dialog.Show();
        Oberflaeche.Zeichnen(dialog);
        dialog.Close();
        gezeigt.Add(dialog.GetType().Name);
        return false;
    }

    private static ListBox Seitenliste(Window fenster) =>
        fenster.GetVisualDescendants().OfType<ListBox>().Single(l => l.Classes.Contains("seitenliste"));

    private static List<Border> Sichtbare(Window fenster, string klasse) =>
        fenster.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains(klasse) && b.IsEffectivelyVisible).ToList();

    /// <summary>Punkt in einem Steuerelement (waagrecht in der Mitte, senkrecht beim Anteil) in Fensterkoordinaten.</summary>
    private static Point Punkt(Control steuerelement, Window fenster, double senkrecht) =>
        steuerelement.TranslatePoint(new Point(steuerelement.Bounds.Width / 2, steuerelement.Bounds.Height * senkrecht), fenster)
        ?? throw new InvalidOperationException("Steuerelement liegt nicht im Fenster.");

    private static void Zeigen<T>(Window fenster, TestOberflaeche o, IRelayCommand befehl)
        where T : Control
    {
        befehl.Execute(null);
        o.Abarbeiten();
        Oberflaeche.Zeichnen(fenster);
        Assert.Single(fenster.GetVisualDescendants().OfType<T>());
    }

    /// <summary>Fährt mit der Maus über die Mitte eines Steuerelements (z. B. Fadenkreuz im Verlauf).</summary>
    private static void Ueberfahren<T>(Window fenster)
        where T : Control
    {
        T steuerelement = fenster.GetVisualDescendants().OfType<T>().First(s => s.IsEffectivelyVisible);
        fenster.MouseMove(Punkt(steuerelement, fenster, 0.5));
        Oberflaeche.Zeichnen(fenster);
        fenster.MouseMove(new Point(1, 1));
        Oberflaeche.Zeichnen(fenster);
    }

    /// <summary>Lässt die Generierung rechnen und arbeitet dabei die Meldungen an die Oberfläche ab.</summary>
    private static async Task WartenAsync(TestOberflaeche o, Window fenster, TimeSpan hoechstens, Func<bool> fertig)
    {
        DateTime ende = DateTime.UtcNow + hoechstens;
        while (DateTime.UtcNow < ende && !fertig())
        {
            await Task.Delay(200);
            o.Abarbeiten();
            Dispatcher.UIThread.RunJobs();
        }

        Oberflaeche.Zeichnen(fenster);
    }
}
