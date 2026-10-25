// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Rendering;
using SkiaSharp;

namespace AndiGenerator.UI;

/// <summary>
/// Zeichenfläche auf einer Skia-Leinwand (PDF-Seite). Ohne Leinwand misst sie nur, z. B. für den Seitenumbruch.
/// Schrift wie in der Oberfläche: die mitgelieferte IBM Plex Sans (fett = SemiBold), ersatzweise Segoe UI. Die Schriften
/// werden einmal geladen und von allen Seiten geteilt, damit das PDF sie nur einmal einbettet.
/// </summary>
internal sealed partial class SkiaZeichenflaeche : IZeichenflaeche, IDisposable
{
    private const string Ersatzschrift = "Segoe UI";

    private static readonly Lazy<SKTypeface> SchriftNormal = new(() => Laden("IBMPlexSans-Regular.ttf", SKFontStyle.Normal));
    private static readonly Lazy<SKTypeface> SchriftFett = new(() => Laden("IBMPlexSans-SemiBold.ttf", SKFontStyle.Bold));

    private readonly SKCanvas? leinwand;
    private readonly Dictionary<(double Groesse, bool Fett), SKFont> schriften = [];
    private readonly SKPaint stift = new() { IsAntialias = true };

    /// <summary>Initialisiert die Zeichenfläche.</summary>
    /// <param name="leinwand">Die Leinwand oder <c>null</c> zum reinen Messen.</param>
    public SkiaZeichenflaeche(SKCanvas? leinwand)
    {
        this.leinwand = leinwand;
    }

    /// <inheritdoc/>
    public double TextBreite(string text, double groesse, bool fett) => Schrift(groesse, fett).MeasureText(text);

    /// <inheritdoc/>
    public void Text(string text, double x, double y, double groesse, Farbe farbe, bool fett)
    {
        if (leinwand is null || text.Length == 0)
        {
            return;
        }

        SKFont schrift = Schrift(groesse, fett);
        schrift.GetFontMetrics(out SKFontMetrics metriken);
        stift.Style = SKPaintStyle.Fill;
        stift.Color = Skia(farbe);
        leinwand.DrawText(text, (float)x, (float)(y - metriken.Ascent), schrift, stift);
    }

    /// <inheritdoc/>
    public void TextSenkrecht(string text, double x, double y, double groesse, Farbe farbe)
    {
        if (leinwand is null || text.Length == 0)
        {
            return;
        }

        SKFont schrift = Schrift(groesse, false);
        schrift.GetFontMetrics(out SKFontMetrics metriken);
        stift.Style = SKPaintStyle.Fill;
        stift.Color = Skia(farbe);
        leinwand.Save();
        leinwand.Translate((float)x, (float)y);
        leinwand.RotateDegrees(-90);
        leinwand.DrawText(text, 0, -metriken.Ascent, schrift, stift);
        leinwand.Restore();
    }

    /// <inheritdoc/>
    public void Linie(double x1, double y1, double x2, double y2, Farbe farbe, double staerke)
    {
        if (leinwand is null)
        {
            return;
        }

        stift.Style = SKPaintStyle.Stroke;
        stift.StrokeWidth = (float)staerke;
        stift.Color = Skia(farbe);
        leinwand.DrawLine((float)x1, (float)y1, (float)x2, (float)y2, stift);
    }

    /// <inheritdoc/>
    public void Rechteck(double x, double y, double breite, double hoehe, Farbe farbe)
    {
        if (leinwand is null)
        {
            return;
        }

        stift.Style = SKPaintStyle.Fill;
        stift.Color = Skia(farbe);
        leinwand.DrawRect((float)x, (float)y, (float)breite, (float)hoehe, stift);
    }

    /// <inheritdoc/>
    public void Kreis(double mx, double my, double radius, Farbe? fuellung, Farbe? rand, double staerke)
    {
        if (leinwand is null)
        {
            return;
        }

        if (fuellung is Farbe f)
        {
            stift.Style = SKPaintStyle.Fill;
            stift.Color = Skia(f);
            leinwand.DrawCircle((float)mx, (float)my, (float)radius, stift);
        }

        if (rand is Farbe r)
        {
            stift.Style = SKPaintStyle.Stroke;
            stift.StrokeWidth = (float)staerke;
            stift.Color = Skia(r);
            leinwand.DrawCircle((float)mx, (float)my, (float)radius, stift);
        }
    }

    /// <inheritdoc/>
    public void Vieleck(IReadOnlyList<(double X, double Y)> punkte, Farbe? fuellung, Farbe? rand, double staerke, bool geschlossen)
    {
        ArgumentNullException.ThrowIfNull(punkte);
        if (leinwand is null || punkte.Count < 2)
        {
            return;
        }

        using var pfad = new SKPath();
        pfad.MoveTo((float)punkte[0].X, (float)punkte[0].Y);
        for (int i = 1; i < punkte.Count; i++)
        {
            pfad.LineTo((float)punkte[i].X, (float)punkte[i].Y);
        }

        if (geschlossen)
        {
            pfad.Close();
        }

        if (fuellung is Farbe f)
        {
            stift.Style = SKPaintStyle.Fill;
            stift.Color = Skia(f);
            leinwand.DrawPath(pfad, stift);
        }

        if (rand is Farbe r)
        {
            stift.Style = SKPaintStyle.Stroke;
            stift.StrokeWidth = (float)staerke;
            stift.StrokeJoin = SKStrokeJoin.Miter;
            stift.StrokeCap = SKStrokeCap.Butt;
            stift.Color = Skia(r);
            leinwand.DrawPath(pfad, stift);
        }
    }

    /// <inheritdoc/>
    public void TextMitRand(string text, double x, double y, double groesse, Farbe farbe, bool fett, Farbe rand, double staerke)
    {
        if (leinwand is null || text.Length == 0)
        {
            return;
        }

        SKFont schrift = Schrift(groesse, fett);
        schrift.GetFontMetrics(out SKFontMetrics metriken);
        stift.Style = SKPaintStyle.StrokeAndFill;
        stift.StrokeWidth = (float)staerke;
        stift.StrokeJoin = SKStrokeJoin.Round;
        stift.Color = Skia(rand);
        leinwand.DrawText(text, (float)x, (float)(y - metriken.Ascent), schrift, stift);
        stift.StrokeJoin = SKStrokeJoin.Miter;
        Text(text, x, y, groesse, farbe, fett);
    }

    /// <inheritdoc/>
    public IDisposable Deckkraft(double wert)
    {
        if (leinwand is null)
        {
            return new Wiederherstellen(null);
        }

        using var ebene = new SKPaint { Color = new SKColor(0, 0, 0, (byte)Math.Round(Math.Clamp(wert, 0, 1) * 255)) };
        leinwand.SaveLayer(ebene);
        return new Wiederherstellen(leinwand);
    }

    /// <inheritdoc/>
    public IDisposable Begrenzen(double x, double y, double breite, double hoehe)
    {
        if (leinwand is null)
        {
            return new Wiederherstellen(null);
        }

        leinwand.Save();
        leinwand.ClipRect(new SKRect((float)x, (float)y, (float)(x + breite), (float)(y + hoehe)));
        return new Wiederherstellen(leinwand);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (SKFont schrift in schriften.Values)
        {
            schrift.Dispose();
        }

        stift.Dispose();
    }

    private static SKColor Skia(Farbe farbe) => new(farbe.R, farbe.G, farbe.B);

    /// <summary>Öffnet eine Schriftdatei aus <c>Assets/Fonts</c> (Oberfläche: Avalonia-Ressource; MCP-Server: eingebettet).</summary>
    /// <param name="datei">Dateiname, z. B. <c>IBMPlexSans-Regular.ttf</c>.</param>
    /// <returns>Der Inhalt.</returns>
    private static partial Stream SchriftOeffnen(string datei);

    private static SKTypeface Laden(string datei, SKFontStyle ersatz)
    {
        try
        {
            using Stream strom = SchriftOeffnen(datei);
            using SKData daten = SKData.Create(strom);
            if (SKTypeface.FromData(daten) is SKTypeface schrift)
            {
                return schrift;
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or UriFormatException)
        {
            // Ohne Avalonia-Laufzeit (oder bei fehlender Datei) mit der Systemschrift weiter.
            return Ersatz(ersatz);
        }

        return Ersatz(ersatz);
    }

    private static SKTypeface Ersatz(SKFontStyle stil) => SKTypeface.FromFamilyName(Ersatzschrift, stil) ?? SKTypeface.Default;

    private SKFont Schrift(double groesse, bool fett)
    {
        if (!schriften.TryGetValue((groesse, fett), out SKFont? schrift))
        {
            schrift = new SKFont(fett ? SchriftFett.Value : SchriftNormal.Value, (float)groesse) { Subpixel = true };
            schriften.Add((groesse, fett), schrift);
        }

        return schrift;
    }

    /// <summary>Stellt beim Beenden eines Bereichs (Deckkraft, Ausschnitt) den vorigen Zustand der Leinwand her.</summary>
    private sealed class Wiederherstellen(SKCanvas? leinwand) : IDisposable
    {
        public void Dispose() => leinwand?.Restore();
    }
}
