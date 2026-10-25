// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using AndiGenerator.Presentation;
using AndiGenerator.Presentation.Tests;
using AndiGenerator.UI.Ansichten;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Xunit.Abstractions;

namespace AndiGenerator.UI.Tests;

/// <summary>
/// Sehr lange Mannschaftsnamen (z. B. mit Sponsor) sprengen keine Ansicht: In jeder Ansicht bleibt jeder Text mit dem Namen
/// schmaler als eine Namensspalte. Mit der Umgebungsvariable <c>ANDIGEN_LANGE_NAMEN</c> (Ordner) entstehen dazu Bilder jeder
/// Ansicht zum Ansehen.
/// </summary>
[Collection(OberflaechenSammlung.Name)]
public sealed class LangeNamenTests : IDisposable
{
    /// <summary>Ein Name, wie es ihn mit Sponsor und Abteilung geben kann.</summary>
    internal const string LangerName = "TTC OE Clarity Telefonie-Systeme Bad Homburg e.V. Tischtennisabteilung der Spielgemeinschaft Taunus-Main-Kinzig";

    private const double Hoechstbreite = 420;

    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-lange-namen-" + Guid.NewGuid().ToString("N"));
    private readonly Oberflaeche oberflaeche;
    private readonly ITestOutputHelper ausgabe;
    private readonly string? bilder = Environment.GetEnvironmentVariable("ANDIGEN_LANGE_NAMEN");

    public LangeNamenTests(Oberflaeche oberflaeche, ITestOutputHelper ausgabe)
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
    public Task Normale_Runde_mit_sehr_langem_Namen() => oberflaeche.Ausfuehren(async () =>
    {
        string datei = Vorbereiten("Liga mit Koppelterminen", null);
        var o = new TestOberflaeche { DateiZumOeffnen = datei, Sammeln = true };
        using var modell = new HauptfensterViewModel(o, () => new HostWindow(), Path.Combine(ordner, "basis"), null);
        var fenster = new HauptfensterView { DataContext = modell, Width = 1400, Height = 900 };
        fenster.Show();
        try
        {
            await OeffnenAsync(modell, o);
            Ansicht(fenster, o, modell.KostenansichtCommand, "kosten", klickTt: true);
            Ansicht(fenster, o, modell.QualitaetsansichtCommand, "qualitaet", klickTt: true);
            Ansicht(fenster, o, modell.TerminplanansichtCommand, "terminplan", klickTt: true);
            TerminplanAnsichtViewModel plan = (TerminplanAnsichtViewModel)fenster.GetVisualDescendants().OfType<TerminplanAnsicht>().Single().DataContext!;
            plan.Darstellung = 1;
            Pruefen(fenster, o, "mannschaftsplaene");
            plan.Darstellung = 2;
            Pruefen(fenster, o, "mannschaftsplaene-nachbarn");
            Ansicht(fenster, o, modell.MeldungsansichtCommand, "meldungen", klickTt: true);
            Ansicht(fenster, o, modell.DiagrammansichtCommand, "diagramme", klickTt: true);
            DiagrammAnsichtViewModel diagramme = (DiagrammAnsichtViewModel)fenster.GetVisualDescendants().OfType<DiagrammAnsicht>().Single().DataContext!;
            foreach (Diagrammwahl wahl in diagramme.Auswahl.ToList())
            {
                diagramme.WaehlenCommand.Execute(wahl);
                Pruefen(fenster, o, "diagramm-" + wahl.Teil);
            }

            Ansicht(fenster, o, modell.TerminwunschansichtCommand, "terminwuensche", klickTt: false);
            Ansicht(fenster, o, modell.NachbarterminansichtCommand, "nachbartermine", klickTt: false);
            Ansicht(fenster, o, modell.LeitstandCommand, "leitstand", klickTt: false);
            Ansicht(fenster, o, modell.EinstellungenCommand, "einstellungen", klickTt: false);
        }
        finally
        {
            fenster.Close();
        }

        oberflaeche.KeineFehler(ausgabe);
    });

    [Fact]
    public Task Kompaktspieltage_mit_sehr_langem_Namen() => oberflaeche.Ausfuehren(async () =>
    {
        string datei = Vorbereiten("Jugend 19 Hessenliga (HR)", "TTC OE Clarity Telefonie-Systeme Bad Homburg e.V.");
        var o = new TestOberflaeche { DateiZumOeffnen = datei, Sammeln = true };
        using var modell = new HauptfensterViewModel(o, () => new HostWindow(), Path.Combine(ordner, "basis"), null);
        var fenster = new HauptfensterView { DataContext = modell, Width = 1400, Height = 900 };
        fenster.Show();
        try
        {
            await OeffnenAsync(modell, o);
            Pruefen(fenster, o, "kompakt-meldungen");

            // Kurz rechnen (bis zum ersten Plan), damit Karte, km und Spieltage gefüllt sind.
            Task lauf = modell.GenerierungCommand.ExecuteAsync(null);
            for (int i = 0; i < 600 && !(modell.KompaktRechnet && modell.Statuszeile.Contains("geplant – ", StringComparison.Ordinal)); i++)
            {
                await Task.Delay(100);
                o.Abarbeiten();
            }

            modell.StoppenCommand.Execute(null);
            for (int i = 0; i < 600 && !lauf.IsCompleted; i++)
            {
                await Task.Delay(100);
                o.Abarbeiten();
            }

            await lauf;
            Pruefen(fenster, o, "kompakt-plan");

            // Die km je Mannschaft stehen ganz in ihrer Karte (bei langen Namen wird die Angabe „Ausrichter“ gekürzt).
            List<TextBlock> km = fenster.GetVisualDescendants().OfType<TextBlock>().Where(t => t.DataContext is Kompaktkmzeile && t.Classes.Contains("ziffer")).ToList();
            Assert.NotEmpty(km);
            Assert.All(km, t =>
            {
                Border karte = t.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("karte"));
                Point? rechts = t.TranslatePoint(new Point(t.Bounds.Width, 0), karte);
                Assert.True(rechts is { } r && r.X <= karte.Bounds.Width, $"{t.Text} ragt aus der Karte");
            });
            Ansicht(fenster, o, modell.DiagrammansichtCommand, "kompakt-diagramme", klickTt: false);
        }
        finally
        {
            fenster.Close();
        }

        oberflaeche.KeineFehler(ausgabe);
    });

    private static async Task OeffnenAsync(HauptfensterViewModel modell, TestOberflaeche o)
    {
        await modell.OeffnenCommand.ExecuteAsync(null);
        if (modell.Einrichtung is { } einrichtung)
        {
            await einrichtung.AbschliessenCommand.ExecuteAsync(null);
        }

        o.Abarbeiten();
        Assert.True(modell.ZeigtArbeitsbereich);
    }

    /// <summary>Kopiert ein Beispiel und ersetzt einen Mannschaftsnamen (sonst den ersten) durch <see cref="LangerName"/>.</summary>
    private string Vorbereiten(string beispiel, string? name)
    {
        Directory.CreateDirectory(Path.Combine(ordner, "daten"));
        string quelle = Path.Combine(Beispielsammlung.Programmordner, beispiel + ".xml");
        Encoding latin = Encoding.Latin1;
        string xml = File.ReadAllText(quelle, latin);
        int beginn = xml.IndexOf("<name>", StringComparison.Ordinal) + "<name>".Length;
        string alt = name ?? xml[beginn..xml.IndexOf("</name>", beginn, StringComparison.Ordinal)];
        string ziel = Path.Combine(ordner, "daten", beispiel + ".xml");
        File.WriteAllText(ziel, xml.Replace(alt, LangerName, StringComparison.Ordinal), latin);
        string aenderungen = Path.ChangeExtension(quelle, ".modifications");
        if (File.Exists(aenderungen))
        {
            File.WriteAllText(Path.ChangeExtension(ziel, ".modifications"), File.ReadAllText(aenderungen).Replace(alt, LangerName, StringComparison.Ordinal));
        }

        return ziel;
    }

    private void Ansicht(HauptfensterView fenster, TestOberflaeche o, CommunityToolkit.Mvvm.Input.IRelayCommand befehl, string name, bool klickTt)
    {
        befehl.Execute(null);
        o.Abarbeiten();
        Oberflaeche.Zeichnen(fenster);
        if (klickTt && fenster.GetVisualDescendants().OfType<Control>().Select(c => c.DataContext).OfType<AnsichtViewModel>().LastOrDefault(a => a.Quellen.Contains(PlanQuelle.ClickTt)) is { } ansicht)
        {
            ansicht.Quelle = PlanQuelle.ClickTt;
        }

        Pruefen(fenster, o, name);
    }

    /// <summary>
    /// Kein sichtbarer einzeiliger Text mit dem langen Namen ist breiter als eine Namensspalte (umbrechende Texte wie
    /// Erläuterungen und Hinweise bleiben in ihrer Karte); auf Wunsch ein Bild der Ansicht.
    /// </summary>
    private void Pruefen(HauptfensterView fenster, TestOberflaeche o, string name)
    {
        o.Abarbeiten();
        Oberflaeche.Zeichnen(fenster);
        Dispatcher.UIThread.RunJobs();
        Oberflaeche.Zeichnen(fenster);
        if (bilder is not null)
        {
            Directory.CreateDirectory(bilder);
            using WriteableBitmap? bild = fenster.CaptureRenderedFrame();
#pragma warning disable CS0618 // wie Bildschirmfotos: die neue Speicher-Methode verlangt noch undokumentierte Optionen
            bild?.Save(Path.Combine(bilder, name + ".png"));
#pragma warning restore CS0618
        }

        List<string> zuBreit = fenster.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && (t.Text ?? string.Empty).Contains("Spielgemeinschaft", StringComparison.Ordinal) && t.TextWrapping == Avalonia.Media.TextWrapping.NoWrap && t.Bounds.Width > Hoechstbreite)
            .Select(t => $"{name}: {t.Bounds.Width:F0} breit – {t.Text}")
            .ToList();
        Assert.True(zuBreit.Count == 0, string.Join(Environment.NewLine, zuBreit));
    }
}
