// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Concurrent;
using System.Globalization;
using AndiGenerator.Rendering;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace AndiGenerator.UI;

/// <summary>Zeichenfläche auf einem Avalonia-Zeichenkontext (Bildschirm); Einheiten sind geräteunabhängige Pixel.</summary>
/// <param name="kontext">Der Zeichenkontext.</param>
/// <param name="schrift">Schrift (fett = SemiBold) oder <c>null</c> für die Standardschrift (fett = Bold).</param>
internal sealed class AvaloniaZeichenflaeche(DrawingContext kontext, FontFamily? schrift = null) : IZeichenflaeche
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");
    private static readonly Typeface Fett = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
    private static readonly ConcurrentDictionary<Farbe, IImmutableSolidColorBrush> Pinsel = new();

    private readonly Typeface normal = schrift is null ? Typeface.Default : new Typeface(schrift);
    private readonly Typeface fettschrift = schrift is null ? Fett : new Typeface(schrift, FontStyle.Normal, FontWeight.SemiBold);

    /// <inheritdoc/>
    public double TextBreite(string text, double groesse, bool fett) => Formatiert(text, groesse, Farbe.Tinte, fett).WidthIncludingTrailingWhitespace;

    /// <inheritdoc/>
    public void Text(string text, double x, double y, double groesse, Farbe farbe, bool fett) =>
        kontext.DrawText(Formatiert(text, groesse, farbe, fett), new Point(x, y));

    /// <inheritdoc/>
    public void TextSenkrecht(string text, double x, double y, double groesse, Farbe farbe)
    {
        var punkt = new Point(x, y);
        Matrix drehung = Matrix.CreateTranslation(-x, -y) * Matrix.CreateRotation(-Math.PI / 2) * Matrix.CreateTranslation(x, y);
        using (kontext.PushTransform(drehung))
        {
            kontext.DrawText(Formatiert(text, groesse, farbe, false), punkt);
        }
    }

    /// <inheritdoc/>
    public void Linie(double x1, double y1, double x2, double y2, Farbe farbe, double staerke) =>
        kontext.DrawLine(new ImmutablePen(Brush(farbe), staerke), new Point(x1, y1), new Point(x2, y2));

    /// <inheritdoc/>
    public void Rechteck(double x, double y, double breite, double hoehe, Farbe farbe) =>
        kontext.FillRectangle(Brush(farbe), new Rect(x, y, breite, hoehe));

    /// <inheritdoc/>
    public void Kreis(double mx, double my, double radius, Farbe? fuellung, Farbe? rand, double staerke) =>
        kontext.DrawEllipse(
            fuellung is Farbe f ? Brush(f) : null,
            rand is Farbe r ? new ImmutablePen(Brush(r), staerke) : null,
            new Point(mx, my),
            radius,
            radius);

    /// <inheritdoc/>
    public void Vieleck(IReadOnlyList<(double X, double Y)> punkte, Farbe? fuellung, Farbe? rand, double staerke, bool geschlossen)
    {
        ArgumentNullException.ThrowIfNull(punkte);
        if (punkte.Count < 2)
        {
            return;
        }

        var geometrie = new StreamGeometry();
        using (StreamGeometryContext g = geometrie.Open())
        {
            g.BeginFigure(new Point(punkte[0].X, punkte[0].Y), fuellung is not null);
            for (int i = 1; i < punkte.Count; i++)
            {
                g.LineTo(new Point(punkte[i].X, punkte[i].Y));
            }

            g.EndFigure(geschlossen);
        }

        IPen? stift = rand is Farbe r ? new ImmutablePen(Brush(r), staerke, lineCap: PenLineCap.Flat, lineJoin: PenLineJoin.Miter) : null;
        kontext.DrawGeometry(fuellung is Farbe f ? Brush(f) : null, stift, geometrie);
    }

    /// <inheritdoc/>
    public void TextMitRand(string text, double x, double y, double groesse, Farbe farbe, bool fett, Farbe rand, double staerke)
    {
        FormattedText formatiert = Formatiert(text, groesse, farbe, fett);
        var ort = new Point(x, y);
        if (formatiert.BuildGeometry(ort) is Geometry umriss)
        {
            kontext.DrawGeometry(Brush(rand), new ImmutablePen(Brush(rand), staerke, lineJoin: PenLineJoin.Round), umriss);
        }

        kontext.DrawText(formatiert, ort);
    }

    /// <inheritdoc/>
    public IDisposable Deckkraft(double wert) => kontext.PushOpacity(wert);

    /// <inheritdoc/>
    public IDisposable Begrenzen(double x, double y, double breite, double hoehe) => kontext.PushClip(new Rect(x, y, breite, hoehe));

    private static IImmutableSolidColorBrush Brush(Farbe farbe) =>
        Pinsel.GetOrAdd(farbe, f => new ImmutableSolidColorBrush(Color.FromRgb(f.R, f.G, f.B)));

    private FormattedText Formatiert(string text, double groesse, Farbe farbe, bool fett) =>
        new(text, Deutsch, FlowDirection.LeftToRight, fett ? fettschrift : normal, groesse, Brush(farbe));
}
