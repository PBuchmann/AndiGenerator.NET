// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace AndiGenerator.UI;

/// <summary>
/// Zeichnet ein <see cref="Kartenbild"/> (Ansicht „Kompaktspieltage“, nicht im Original): Bundesländer, Städte, Fahrten und
/// Spielorte. Die Karte nimmt die verfügbare Breite und die passende Höhe ein; über einem Spielort nennt ein Hinweis Mannschaft
/// und Halle.
/// </summary>
public sealed class Kompaktkarte : Control
{
    /// <summary>Definiert die Eigenschaft <see cref="Bild"/>.</summary>
    public static readonly StyledProperty<Kartenbild?> BildProperty = AvaloniaProperty.Register<Kompaktkarte, Kartenbild?>(nameof(Bild));

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

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

        double s = Bounds.Width / bild.Breite;
        context.DrawRectangle(Farbe("Schwebe"), null, new Rect(Bounds.Size), 10, 10);
        using (context.PushClip(new RoundedRect(new Rect(Bounds.Size), 10)))
        {
            var grenze = new Pen(Farbe("Rand"), 1);
            IBrush land = Farbe("Flaeche");
            foreach (IReadOnlyList<Kartenpunkt> flaeche in bild.Flaechen)
            {
                context.DrawGeometry(land, grenze, Linienzug(flaeche, s));
            }

            foreach (Kartenbeschriftung name in bild.Laender)
            {
                FormattedText text = Text(name.Text.ToUpper(Deutsch), Farbe("Rand"), 11, fett: false);
                context.DrawText(text, new Point((name.Punkt.X * s) - (text.Width / 2), (name.Punkt.Y * s) - (text.Height / 2)));
            }

            foreach (Kartenbeschriftung stadt in bild.Staedte)
            {
                context.FillRectangle(Farbe("Tinte2"), new Rect((stadt.Punkt.X * s) - 2.5, (stadt.Punkt.Y * s) - 2.5, 5, 5));
                context.DrawText(Text(stadt.Text, Farbe("Tinte2"), 11, fett: false), new Point(stadt.Schrift.X * s, stadt.Schrift.Y * s));
            }

            foreach (Kartenfahrt fahrt in bild.Fahrten)
            {
                var stift = new Pen(Farbe("Akzent"), fahrt.Kraeftig ? 2.5 : 1.5, lineCap: PenLineCap.Round);
                using (context.PushOpacity(fahrt.Kraeftig ? 1 : 0.35))
                {
                    context.DrawLine(stift, new Point(fahrt.Von.X * s, fahrt.Von.Y * s), new Point(fahrt.Nach.X * s, fahrt.Nach.Y * s));
                }
            }

            foreach (Kartenhalle halle in bild.Hallen)
            {
                using (context.PushOpacity(halle.Ort.Gedimmt ? 0.45 : 1))
                {
                    double r = halle.Ort.Hervorgehoben ? 8 : 5.5;
                    context.DrawEllipse(Farbe(halle.Ort.Hervorgehoben ? "Gut" : "Akzent"), new Pen(Farbe("Flaeche"), 2), new Point(halle.Punkt.X * s, halle.Punkt.Y * s), r, r);
                    FormattedText text = Text(halle.Ort.Kurzname, Farbe("Tinte"), 12, fett: true);
                    var ort = new Point((halle.Schrift.X + 2) * s, halle.Schrift.Y * s);
                    context.DrawGeometry(Farbe("Flaeche"), new Pen(Farbe("Flaeche"), 3, lineJoin: PenLineJoin.Round), text.BuildGeometry(ort)!);
                    context.DrawText(text, ort);
                }
            }

            if (bild.MassstabKm > 0)
            {
                double y = Bounds.Height - 14;
                context.DrawLine(new Pen(Farbe("Tinte2"), 2), new Point(14, y), new Point(14 + (bild.MassstabBreite * s), y));
                context.DrawText(Text(bild.MassstabKm.ToString(Deutsch) + " km Luftlinie", Farbe("Tinte2"), 10.5, fett: false), new Point(14, y - 17));
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

        double s = Bounds.Width / bild.Breite;
        Point p = e.GetPosition(this);
        Kartenhalle? naechste = bild.Hallen
            .Where(h => Math.Abs((h.Punkt.X * s) - p.X) < 10 && Math.Abs((h.Punkt.Y * s) - p.Y) < 10)
            .MinBy(h => Math.Abs((h.Punkt.X * s) - p.X) + Math.Abs((h.Punkt.Y * s) - p.Y));
        ToolTip.SetTip(this, naechste?.Ort.Hinweis);
    }

    private static StreamGeometry Linienzug(IReadOnlyList<Kartenpunkt> punkte, double s)
    {
        var geometrie = new StreamGeometry();
        using StreamGeometryContext g = geometrie.Open();
        g.BeginFigure(new Point(punkte[0].X * s, punkte[0].Y * s), isFilled: true);
        for (int i = 1; i < punkte.Count; i++)
        {
            g.LineTo(new Point(punkte[i].X * s, punkte[i].Y * s));
        }

        g.EndFigure(isClosed: true);
        return geometrie;
    }

    private static IBrush Farbe(string name) =>
        global::Avalonia.Application.Current?.TryFindResource(name, out object? wert) == true && wert is IBrush b ? b : Brushes.Gray;

    private static FormattedText Text(string text, IBrush farbe, double groesse, bool fett)
    {
        FontFamily schrift = global::Avalonia.Application.Current?.TryFindResource("Grundschrift", out object? f) == true && f is FontFamily ff ? ff : FontFamily.Default;
        return new FormattedText(text, Deutsch, FlowDirection.LeftToRight, new Typeface(schrift, FontStyle.Normal, fett ? FontWeight.SemiBold : FontWeight.Normal), groesse, farbe);
    }
}
