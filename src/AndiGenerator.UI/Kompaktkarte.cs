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

        context.DrawRectangle(Farbe("Schwebe"), null, new Rect(Bounds.Size), 10, 10);
        using (context.PushClip(new RoundedRect(new Rect(Bounds.Size), 10)))
        {
            FontFamily? schrift = global::Avalonia.Application.Current?.TryFindResource("Grundschrift", out object? f) == true && f is FontFamily ff ? ff : null;
            new Kartenzeichner(new AvaloniaZeichenflaeche(context, schrift)).Zeichnen(bild, Bounds.Width, Bounds.Height);
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

        double s = Bounds.Width / bild.Breite;
        Point p = e.GetPosition(this);
        Kartenhalle? naechste = bild.Hallen
            .Where(h => Math.Abs((h.Punkt.X * s) - p.X) < 10 && Math.Abs((h.Punkt.Y * s) - p.Y) < 10)
            .MinBy(h => Math.Abs((h.Punkt.X * s) - p.X) + Math.Abs((h.Punkt.Y * s) - p.Y));
        ToolTip.SetTip(this, naechste?.Ort.Hinweis);
    }

    private static IBrush Farbe(string name) =>
        global::Avalonia.Application.Current?.TryFindResource(name, out object? wert) == true && wert is IBrush b ? b : Brushes.Gray;
}
