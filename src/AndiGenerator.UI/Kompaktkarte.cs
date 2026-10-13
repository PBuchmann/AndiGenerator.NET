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

    /// <summary>Gelb der Einzelbegegnungen, wie in der Planmatrix (KompaktspieltageAnsicht.axaml).</summary>
    private static readonly IBrush EinzelGelb = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#EAB308"));

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

            // Gerade Linien; mehrere Fahrten zwischen denselben Hallen liegen nebeneinander. Kurze Fahrten, die unter den
            // Hallenpunkten verschwänden, kommen erst nach den Hallen in den Vordergrund.
            foreach (Kartenfahrt fahrt in bild.Fahrten.Where(f => !Kurz(f, s)))
            {
                if (Geteilt(bild, fahrt))
                {
                    HalbeFahrt(context, fahrt, s);
                }
                else
                {
                    Fahrt(context, fahrt, s, umriss: false);
                }
            }

            foreach (Kartenhalle halle in bild.Hallen)
            {
                using (context.PushOpacity(halle.Ort.Gedimmt ? 0.45 : 1))
                {
                    HallePunkt(context, bild, halle, s);
                    FormattedText text = Text(halle.Ort.Kurzname, Farbe("Tinte"), 12, fett: true);
                    var ort = new Point((halle.Schrift.X + 2) * s, halle.Schrift.Y * s);
                    context.DrawGeometry(Farbe("Flaeche"), new Pen(Farbe("Flaeche"), 3, lineJoin: PenLineJoin.Round), text.BuildGeometry(ort)!);
                    context.DrawText(text, ort);
                }
            }

            // Kurze Fahrten im Vordergrund mit hellem Umriss.
            foreach (Kartenfahrt fahrt in bild.Fahrten.Where(f => Kurz(f, s)))
            {
                Fahrt(context, fahrt, s, umriss: true);
            }

            // Pfeilspitzen am Rand des Ziels zeigen die Richtung der Fahrt (bei kurzen Fahrten fehlt der Platz).
            foreach (Kartenfahrt fahrt in bild.Fahrten.Where(f => f.Kraeftig && !Kurz(f, s)))
            {
                if (Geteilt(bild, fahrt))
                {
                    // Mehrere Fahrten zum selben Ziel: ein gemeinsamer Pfeil, jede Fahrt ihre Hälfte in ihrer Farbe.
                    HalberPfeil(context, Linie(fahrt with { Versatz = 0 }, s), Math.Sign(fahrt.Versatz), Linienfarbe(fahrt));
                }
                else
                {
                    Pfeil(context, Linie(fahrt, s), Linienfarbe(fahrt));
                }
            }

            if (bild.MassstabKm > 0)
            {
                double y = Bounds.Height - 14;
                context.DrawLine(new Pen(Farbe("Tinte2"), 2), new Point(14, y), new Point(14 + (bild.MassstabBreite * s), y));
                context.DrawText(Text(bild.MassstabKm.ToString(Deutsch) + " km", Farbe("Tinte2"), 10.5, fett: false), new Point(14, y - 17));
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

    /// <summary>
    /// Zeichnet den Punkt einer Halle. In einer Auswahl ist der Ausgangspunkt der Fahrten grün, ein Ziel in der Farbe seiner
    /// Linie; kommen dort verschieden gefärbte Linien an (z. B. 3er-Spieltag und Einzelbegegnung), wird der Kreis im Winkel der
    /// Linie in Halbkreise (bzw. Sektoren) je Linie geteilt.
    /// </summary>
    private static void HallePunkt(DrawingContext context, Kartenbild bild, Kartenhalle halle, double s)
    {
        var mitte = new Point(halle.Punkt.X * s, halle.Punkt.Y * s);
        var rand = new Pen(Farbe("Flaeche"), 2);
        List<Kartenfahrt> ankommend = bild.Fahrten.Where(f => f.Kraeftig && f.Nach == halle.Punkt).OrderByDescending(f => f.Versatz).ToList();
        bool start = !halle.Ort.Hervorgehoben && bild.Fahrten.Any(f => f.Kraeftig && f.Von == halle.Punkt);
        if (!halle.Ort.Hervorgehoben || ankommend.Count == 0)
        {
            double r = start ? 8 : 5.5;
            context.DrawEllipse(Farbe(start ? "Gut" : "Akzent"), rand, mitte, r, r);
            return;
        }

        const double Radius = 8;
        if (ankommend.Select(f => f.Einzel).Distinct().Count() == 1)
        {
            context.DrawEllipse(ankommend[0].Einzel ? EinzelGelb : Farbe("Akzent"), rand, mitte, Radius, Radius);
            return;
        }

        // Teilung im Winkel der Linie: die Linie mit dem größten Versatz (links der Fahrtrichtung) bekommt die linke Hälfte.
        (Point von, Point nach) = Linie(ankommend[0], s);
        double winkel = Math.Atan2(nach.Y - von.Y, nach.X - von.X) * 180 / Math.PI;
        double teil = 360.0 / ankommend.Count;
        for (int i = 0; i < ankommend.Count; i++)
        {
            context.DrawGeometry(ankommend[i].Einzel ? EinzelGelb : Farbe("Akzent"), null, Sektor(mitte, Radius, winkel + (i * teil), teil));
        }

        // Dünne helle Linie an den Grenzen der Hälften (bzw. Ausschnitte).
        var trenn = new Pen(Farbe("Flaeche"), 1);
        for (int i = 0; i < ankommend.Count; i++)
        {
            double grad = (winkel + (i * teil)) * Math.PI / 180;
            context.DrawLine(trenn, mitte, new Point(mitte.X + (Radius * Math.Cos(grad)), mitte.Y + (Radius * Math.Sin(grad))));
        }

        context.DrawEllipse(null, rand, mitte, Radius, Radius);
    }

    /// <summary>Ein Kreisausschnitt ab einem Winkel (Grad, im Uhrzeigersinn auf dem Bildschirm).</summary>
    private static StreamGeometry Sektor(Point mitte, double r, double anfang, double weite)
    {
        static Point Rand(Point m, double radius, double grad) => new(m.X + (radius * Math.Cos(grad * Math.PI / 180)), m.Y + (radius * Math.Sin(grad * Math.PI / 180)));
        var geometrie = new StreamGeometry();
        using (StreamGeometryContext g = geometrie.Open())
        {
            g.BeginFigure(mitte, true);
            g.LineTo(Rand(mitte, r, anfang));
            g.ArcTo(Rand(mitte, r, anfang + weite), new Size(r, r), 0, weite > 180, SweepDirection.Clockwise);
            g.EndFigure(true);
        }

        return geometrie;
    }

    /// <summary>
    /// Anfang und Ende einer Fahrt in Bildpunkten, quer zur Fahrtrichtung um ihren Versatz verschoben: 2,2 Pixel je Stufe,
    /// sodass mehrere Fahrten zwischen denselben Hallen lückenlos nebeneinander liegen wie ein zweifarbiger Pfeil.
    /// </summary>
    private static (Point Von, Point Nach) Linie(Kartenfahrt fahrt, double s)
    {
        var von = new Point(fahrt.Von.X * s, fahrt.Von.Y * s);
        var nach = new Point(fahrt.Nach.X * s, fahrt.Nach.Y * s);
        Vector richtung = nach - von;
        double laenge = Math.Sqrt((richtung.X * richtung.X) + (richtung.Y * richtung.Y));
        if (fahrt.Versatz == 0 || laenge < 1)
        {
            return (von, nach);
        }

        var quer = new Vector(-richtung.Y / laenge, richtung.X / laenge) * (2.2 * fahrt.Versatz);
        return (von + quer, nach + quer);
    }

    /// <summary>Ob eine Fahrt so kurz ist, dass die Hallenpunkte sie verdecken würden.</summary>
    private static bool Kurz(Kartenfahrt fahrt, double s)
    {
        double dx = (fahrt.Nach.X - fahrt.Von.X) * s;
        double dy = (fahrt.Nach.Y - fahrt.Von.Y) * s;
        return Math.Sqrt((dx * dx) + (dy * dy)) < 30;
    }

    /// <summary>
    /// Zeichnet eine Fahrt. Kurze Fahrten (über den Hallenpunkten) als Pfeil vom Rand des Ausgangspunkts zur Mitte des Ziels
    /// mit seitlich hellem Umriss.
    /// </summary>
    private static void Fahrt(DrawingContext context, Kartenfahrt fahrt, double s, bool umriss)
    {
        double breite = fahrt.Kraeftig ? 2.5 : 1.5;
        (Point von, Point nach) = Linie(fahrt, s);
        using (context.PushOpacity(fahrt.Kraeftig ? 1 : 0.35))
        {
            if (!umriss)
            {
                // Einzelbegegnungen gelb wie in der Planmatrix. Mit Pfeil (in der Auswahl) endet die Linie am Fuß der
                // Spitze, damit Spitze bzw. Pfeilhälften sauber für sich stehen.
                Vector r = nach - von;
                double l = Math.Sqrt((r.X * r.X) + (r.Y * r.Y));
                Point ende = fahrt.Kraeftig && l > 30 ? nach - (r * (15.5 / l)) : nach;
                context.DrawLine(new Pen(Linienfarbe(fahrt), breite, lineCap: PenLineCap.Flat), von, ende);
                return;
            }

            Vector t = nach - von;
            double laenge = Math.Sqrt((t.X * t.X) + (t.Y * t.Y));
            if (laenge < 1)
            {
                return;
            }

            t /= laenge;
            var quer = new Vector(-t.Y, t.X);

            // Vom Rand der farbigen Fläche des Ausgangspunkts (Radius 7 innerhalb des hellen Rands) bis knapp vor die Mitte des
            // Ziels (der helle Rand der Spitze reicht sonst darüber hinaus); heller
            // Umriss seitlich an Schaft und Spitze und ein minimaler am Fuß (1 Pixel), nicht über die Spitze hinaus.
            double ab = Math.Min(7, laenge / 2);
            Point anfang = von + (t * ab);
            double kopf = Math.Min(7, (laenge - ab - 1.5) * 0.6);
            Point spitze = nach - (t * 1.5);
            Point basis = spitze - (t * kopf);
            Point links = basis + (quer * 3.3);
            Point rechts = basis - (quer * 3.3);
            var hell = new Pen(Farbe("Flaeche"), 2, lineCap: PenLineCap.Flat, lineJoin: PenLineJoin.Miter);
            context.DrawLine(new Pen(Farbe("Flaeche"), breite + 2, lineCap: PenLineCap.Flat), anfang - t, basis);

            // Seitenkanten der Spitze als ein Linienzug, damit der helle Rand auch an der Spitze geschlossen ist.
            var kanten = new StreamGeometry();
            using (StreamGeometryContext g = kanten.Open())
            {
                g.BeginFigure(links, false);
                g.LineTo(spitze);
                g.LineTo(rechts);
                g.EndFigure(false);
            }

            context.DrawGeometry(null, hell, kanten);
            context.DrawLine(new Pen(Linienfarbe(fahrt), breite, lineCap: PenLineCap.Flat), anfang, basis);
            var kopfform = new StreamGeometry();
            using (StreamGeometryContext g = kopfform.Open())
            {
                g.BeginFigure(spitze, true);
                g.LineTo(links);
                g.LineTo(rechts);
                g.EndFigure(true);
            }

            context.DrawGeometry(Linienfarbe(fahrt), null, kopfform);
        }
    }

    /// <summary>Kleine Pfeilspitze am Rand des Zielpunkts, in Richtung der Linie.</summary>
    private static void Pfeil(DrawingContext context, (Point Von, Point Nach) linie, IBrush farbe)
    {
        Vector t = linie.Nach - linie.Von;
        double laenge = Math.Sqrt((t.X * t.X) + (t.Y * t.Y));
        if (laenge < 1)
        {
            return;
        }

        t /= laenge;
        var quer = new Vector(-t.Y, t.X);
        Point spitze = linie.Nach - (t * 9.5);
        Point basis = spitze - (t * 7);
        var geometrie = new StreamGeometry();
        using (StreamGeometryContext g = geometrie.Open())
        {
            g.BeginFigure(spitze, true);
            g.LineTo(basis + (quer * 3.8));
            g.LineTo(basis - (quer * 3.8));
            g.EndFigure(true);
        }

        context.DrawGeometry(farbe, null, geometrie);
    }

    /// <summary>
    /// Die Hälfte einer Pfeilspitze auf der Mittelachse: links (<paramref name="seite"/> = 1) oder rechts (-1) der
    /// Fahrtrichtung, minimal über die Achse hinaus, damit zwischen den Hälften keine Fuge zu sehen ist.
    /// </summary>
    private static void HalberPfeil(DrawingContext context, (Point Von, Point Nach) achse, int seite, IBrush farbe)
    {
        Vector t = achse.Nach - achse.Von;
        double laenge = Math.Sqrt((t.X * t.X) + (t.Y * t.Y));
        if (laenge < 1 || seite == 0)
        {
            return;
        }

        t /= laenge;
        Vector quer = new Vector(-t.Y, t.X) * seite;
        Point spitze = achse.Nach - (t * 9.5);
        Point basis = spitze - (t * 7);
        var geometrie = new StreamGeometry();
        using (StreamGeometryContext g = geometrie.Open())
        {
            g.BeginFigure(spitze - (quer * 0.3), true);
            g.LineTo(basis + (quer * 3.8));
            g.LineTo(basis - (quer * 0.3));
            g.EndFigure(true);
        }

        context.DrawGeometry(farbe, null, geometrie);
    }

    /// <summary>Ob eine Fahrt eine von genau zwei kräftigen Fahrten in dieselbe Richtung ist (dann ein Pfeil aus zwei Hälften).</summary>
    private static bool Geteilt(Kartenbild bild, Kartenfahrt fahrt) =>
        fahrt.Kraeftig && bild.Fahrten.Count(f => f.Kraeftig && f.Von == fahrt.Von && f.Nach == fahrt.Nach) == 2;

    /// <summary>
    /// Eine Hälfte des zweifarbigen Pfeils: halbe Breite, auf ihrer Seite der Mittelachse (minimal überlappend), sodass beide
    /// zusammen nicht dicker erscheinen als ein einfacher Pfeil; endet am Fuß der Spitze.
    /// </summary>
    private static void HalbeFahrt(DrawingContext context, Kartenfahrt fahrt, double s)
    {
        (Point von, Point nach) = Linie(fahrt with { Versatz = 0 }, s);
        Vector r = nach - von;
        double l = Math.Sqrt((r.X * r.X) + (r.Y * r.Y));
        if (l < 1)
        {
            return;
        }

        Vector t = r / l;
        Vector quer = new Vector(-t.Y, t.X) * (Math.Sign(fahrt.Versatz) * 0.55);
        Point ende = l > 30 ? nach - (t * 15.5) : nach;
        context.DrawLine(new Pen(Linienfarbe(fahrt), 1.4, lineCap: PenLineCap.Flat), von + quer, ende + quer);
    }

    private static IBrush Linienfarbe(Kartenfahrt fahrt) => fahrt.Einzel ? EinzelGelb : Farbe("Akzent");

    private static IBrush Farbe(string name) =>
        global::Avalonia.Application.Current?.TryFindResource(name, out object? wert) == true && wert is IBrush b ? b : Brushes.Gray;

    private static FormattedText Text(string text, IBrush farbe, double groesse, bool fett)
    {
        FontFamily schrift = global::Avalonia.Application.Current?.TryFindResource("Grundschrift", out object? f) == true && f is FontFamily ff ? ff : FontFamily.Default;
        return new FormattedText(text, Deutsch, FlowDirection.LeftToRight, new Typeface(schrift, FontStyle.Normal, fett ? FontWeight.SemiBold : FontWeight.Normal), groesse, farbe);
    }
}
