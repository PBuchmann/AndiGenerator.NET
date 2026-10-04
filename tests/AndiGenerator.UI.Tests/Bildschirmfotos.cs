// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
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
        await EinrichtungAsync(ziel, StaffelKopieren("erststart", mitAenderungen: false));
        string datei = StaffelKopieren("daten", mitAenderungen: true);
        await HauptfensterAsync(ziel, datei);
        Dialoge(ziel, datei);
    });

    /// <summary>
    /// Startseite und Einrichtung: Die Einrichtung erscheint nur, wenn es etwas zu klären gibt – sicher beim ersten Öffnen
    /// einer click-TT-Datei (ohne <c>.modifications</c>).
    /// </summary>
    private static async Task EinrichtungAsync(string ziel, string datei)
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
        }
        finally
        {
            fenster.Close();
        }
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
            await WartenAsync(o, TimeSpan.FromSeconds(8), () => false);
            modell.GenerierungCommand.Execute(null);
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
            modell.GenerierungCommand.Execute(null);
            Foto(ziel, "18-automodus", Kacheln(fenster), o);

            // Qualität im Automodus (mit Einstufung), gewählt die erste Zeile mit Verstößen je Mannschaft.
            var qualitaet = (QualitaetAnsichtViewModel)Einzige<QualitaetAnsicht>(fenster).DataContext!;
            if (qualitaet.Zeilen.FirstOrDefault(z => z.Zustand != Qualitaetszustand.Erfuellt && z.Kriterium is not null) is { } zeile)
            {
                qualitaet.Waehlen(zeile);
            }

            Foto(ziel, "05-qualitaet", Ansichten(fenster), o);

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
            WunschtermineSeiteViewModel seite = a.Dialog.Seiten.OfType<WunschtermineSeiteViewModel>().Single();
            daten.GetVisualDescendants().OfType<ListBox>().Single(l => l.Classes.Contains("seitenliste")).SelectedItem = seite;
            seite.Mannschaft = Namen.RheinduerkheimIV;
            Oberflaeche.Zeichnen(daten);
            WunschterminDetail detail = daten.GetVisualDescendants().OfType<WunschterminDetail>().Single();
            Point punkt = detail.GetVisualDescendants()
                .OfType<TextBox>()
                .Where(t => t.DataContext is WunschterminZelle)
                .Select(t => t.TranslatePoint(new Point(t.Bounds.Width / 2, t.Bounds.Height / 2), daten))
                .OfType<Point>()
                .First(p => new Rect(daten.ClientSize).Contains(p));
            daten.MouseDown(punkt, MouseButton.Left);
            daten.MouseUp(punkt, MouseButton.Left);
            daten.MouseDown(punkt, MouseButton.Left);
            daten.MouseUp(punkt, MouseButton.Left);
            Oberflaeche.Zeichnen(daten);
        }
        finally
        {
            daten.Close();
        }
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
