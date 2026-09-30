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
internal sealed class AvaloniaZeichenflaeche(DrawingContext kontext) : IZeichenflaeche
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");
    private static readonly Typeface Fett = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
    private static readonly ConcurrentDictionary<Farbe, IImmutableSolidColorBrush> Pinsel = new();

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
    public void Kreis(double mx, double my, double radius, Farbe fuellung, Farbe rand, double staerke) =>
        kontext.DrawEllipse(Brush(fuellung), new ImmutablePen(Brush(rand), staerke), new Point(mx, my), radius, radius);

    private static IImmutableSolidColorBrush Brush(Farbe farbe) =>
        Pinsel.GetOrAdd(farbe, f => new ImmutableSolidColorBrush(Color.FromRgb(f.R, f.G, f.B)));

    private static FormattedText Formatiert(string text, double groesse, Farbe farbe, bool fett) =>
        new(text, Deutsch, FlowDirection.LeftToRight, fett ? Fett : Typeface.Default, groesse, Brush(farbe));
}
