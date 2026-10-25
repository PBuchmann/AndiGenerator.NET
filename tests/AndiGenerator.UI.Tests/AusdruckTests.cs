// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using AndiGenerator.Presentation.Tests;
using AndiGenerator.Rendering;
using Dock.Avalonia.Controls;

namespace AndiGenerator.UI.Tests;

/// <summary>
/// Der Ausdruck als PDF mit SkiaSharp (wie „Drucken“ in der Oberfläche): normaler Plan mit allen Abschnitten und die
/// Übersicht der Kompaktspieltage mit Karte.
/// </summary>
[Collection(OberflaechenSammlung.Name)]
public sealed class AusdruckTests : IDisposable
{
    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-pdf-" + Guid.NewGuid().ToString("N"));
    private readonly Oberflaeche oberflaeche;

    public AusdruckTests(Oberflaeche oberflaeche)
    {
        this.oberflaeche = oberflaeche;
        Directory.CreateDirectory(ordner);
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
    public Task Plan_aus_click_TT_wird_als_PDF_geschrieben() => oberflaeche.Ausfuehren(async () =>
    {
        Directory.CreateDirectory(Path.Combine(ordner, "daten"));
        string datei = Path.Combine(ordner, "daten", "staffel.xml");
        File.Copy(Path.Combine(Testdaten.Referenz, "datendialoge", "D01_Allgemein.xml"), datei);
        var o = new TestOberflaeche { DateiZumOeffnen = datei };
        using var modell = new HauptfensterViewModel(o, () => new HostWindow(), Path.Combine(ordner, "basis"), null);
        await OeffnenAsync(modell);

        (Druckdokument dokument, Seitenformat format) = await AusdruckAsync(modell, o);
        Assert.Contains(dokument.Abschnitte, a => a.Titel == "Spielplan");

        await PruefenAsync(dokument, format, "plan.pdf");
    });

    [Fact]
    public Task Kompaktspieltage_werden_mit_Karte_als_PDF_geschrieben() => oberflaeche.Ausfuehren(async () =>
    {
        var o = new TestOberflaeche { DateiZumOeffnen = Kompaktklasse.Vorbereiten(ordner) };
        using var modell = new HauptfensterViewModel(o, () => new HostWindow(), Path.Combine(ordner, "basis"), null);
        await OeffnenAsync(modell);
        modell.KompaktspieltageCommand.Execute(null);

        // Beenden nach dem ersten Fortschritt: Der Planer liefert die bis dahin besten Pläne (sonst rechnet er bis zu zwei Minuten).
        Task lauf = modell.GenerierungCommand.ExecuteAsync(null);
        for (int i = 0; i < 600 && !lauf.IsCompleted && !modell.Statuszeile.Contains(" – ", StringComparison.Ordinal); i++)
        {
            await Task.Delay(100);
        }

        modell.StoppenCommand.Execute(null);
        await lauf;

        (Druckdokument dokument, Seitenformat format) = await AusdruckAsync(modell, o);
        Assert.Equal(["Meldungen und Plan", "Alle Fahrten", "Kompaktspieltage"], dokument.Abschnitte.Take(3).Select(a => a.Titel));

        await PruefenAsync(dokument, format, "kompakt.pdf");
    });

    private static async Task OeffnenAsync(HauptfensterViewModel modell)
    {
        await modell.OeffnenCommand.ExecuteAsync(null);
        if (modell.Einrichtung is { } einrichtung)
        {
            await einrichtung.AbschliessenCommand.ExecuteAsync(null);
        }

        Assert.True(modell.ZeigtArbeitsbereich);
    }

    private async Task<(Druckdokument Dokument, Seitenformat Format)> AusdruckAsync(HauptfensterViewModel modell, TestOberflaeche o)
    {
        o.Druckauswahl = _ => true;
        o.DateiZumSpeichern = Path.Combine(ordner, "ausdruck.pdf");
        await modell.DruckenCommand.ExecuteAsync(null);
        return (Assert.IsType<Druckdokument>(o.Ausdruck), Assert.IsType<Seitenformat>(o.Format));
    }

    /// <summary>Schreibt das PDF und prüft Kopf, Ende und dass es mehr als eine leere Seite enthält.</summary>
    private async Task PruefenAsync(Druckdokument dokument, Seitenformat format, string name)
    {
        string pfad = Path.Combine(ordner, name);
        PdfAusdruck.Schreiben(pfad, dokument, format);
        byte[] inhalt = await File.ReadAllBytesAsync(pfad);
        string text = System.Text.Encoding.Latin1.GetString(inhalt);
        Assert.StartsWith("%PDF-", text, StringComparison.Ordinal);
        Assert.Contains("%%EOF", text, StringComparison.Ordinal);
        Assert.True(inhalt.Length > 10_000, $"Das PDF ist nur {inhalt.Length} Bytes groß.");
    }
}
