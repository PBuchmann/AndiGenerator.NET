// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace AndiGenerator.UI;

/// <summary>
/// Zeigt ein <see cref="Kartenbild"/> (Ansicht „Kompaktspieltage“, nicht im Original); gezeichnet wird es vom
/// <see cref="Kartenzeichner"/>, derselben Zeichnung wie im Ausdruck. Die Karte nimmt die verfügbare Breite und die passende
/// Höhe ein; über einem Spielort nennt ein Hinweis Mannschaft und Halle.
/// </summary>
public sealed class Kompaktkarte : Control
{
    /// <summary>Definiert die Eigenschaft <see cref="Bild"/>.</summary>
    public static readonly StyledProperty<Kartenbild?> BildProperty = AvaloniaProperty.Register<Kompaktkarte, Kartenbild?>(nameof(Bild));

    static Kompaktkarte() => AffectsRender<Kompaktkarte>(BildProperty);

    /// <summary>Initialisiert die Karte.</summary>
    public Kompaktkarte()
    {
        ClipToBounds = true;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == BildProperty)
            {
                InvalidateMeasure();
            }
        };
    }

    /// <summary>Holt oder setzt das Bild.</summary>
    public Kartenbild? Bild
    {
        get => GetValue(BildProperty);
        set => SetValue(BildProperty, value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bild is not Kartenbild bild || Bounds.Width <= 0)
        {
            return;
        }

        context.DrawRectangle(Pinsel.Farbe("Schwebe"), null, new Rect(Bounds.Size), 10, 10);
        using (context.PushClip(new RoundedRect(new Rect(Bounds.Size), 10)))
        {
            // Die ganze Karte zeigen: Ist der Platz breiter als die Karte bei ihrer Höhe, steht sie in der Mitte (vorher wurde sie
            // auf die volle Breite vergrößert und unten abgeschnitten).
            (double s, double links) = Massstab(bild);
            FontFamily? schrift = global::Avalonia.Application.Current?.TryFindResource("Grundschrift", out object? f) == true && f is FontFamily ff ? ff : null;
            using (context.PushTransform(Matrix.CreateTranslation(links, 0)))
            {
                new Kartenzeichner(new AvaloniaZeichenflaeche(context, schrift)).Zeichnen(bild, bild.Breite * s, bild.Hoehe * s);
            }
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        Kartenbild bild = Bild ?? Kartenbild.Leer;
        const double MaxHoehe = 480;
        double breite = double.IsInfinity(availableSize.Width) ? bild.Breite : availableSize.Width;
        if (bild.Breite <= 0 || bild.Hoehe <= 0)
        {
            return new Size(breite, 0);
        }

        // Höchstens so hoch, dass Raster und Karte nebeneinander überschaubar bleiben (Peter, 13.10.2026).
        double hoehe = Math.Min(breite * bild.Hoehe / bild.Breite, Math.Min(availableSize.Height, MaxHoehe));
        return new Size(hoehe * bild.Breite / bild.Hoehe, hoehe);
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Bild is not Kartenbild bild || Bounds.Width <= 0)
        {
            return;
        }

        (double s, double links) = Massstab(bild);
        Point p = e.GetPosition(this) - new Point(links, 0);
        Kartenhalle? naechste = bild.Hallen
            .Where(h => Math.Abs((h.Punkt.X * s) - p.X) < 10 && Math.Abs((h.Punkt.Y * s) - p.Y) < 10)
            .MinBy(h => Math.Abs((h.Punkt.X * s) - p.X) + Math.Abs((h.Punkt.Y * s) - p.Y));
        ToolTip.SetTip(this, naechste?.Ort.Hinweis);
    }

    /// <summary>Vergrößerung und linker Rand, mit denen die ganze Karte in den Platz passt (waagerecht in der Mitte).</summary>
    private (double Massstab, double Links) Massstab(Kartenbild bild)
    {
        double s = Math.Min(Bounds.Width / bild.Breite, bild.Hoehe > 0 ? Bounds.Height / bild.Hoehe : double.PositiveInfinity);
        return (s, Math.Max(0, (Bounds.Width - (bild.Breite * s)) / 2));
    }
}
