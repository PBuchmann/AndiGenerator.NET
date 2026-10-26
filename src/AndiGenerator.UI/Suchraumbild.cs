// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace AndiGenerator.UI;

/// <summary>
/// Die Anzeige „Suchraum“ der Planung im normalen Modus (Entwurf 2O, Peter 30.10.2026): links eine Fläche voller Punkte, die
/// sich mit jeder Verbesserung zusammenzieht – ein Sinnbild: Sie schrumpft mit dem Abstand zwischen dem besten Plan und der
/// bewiesenen Grenze, der schraffierte Kern ist unvermeidbar. Rechts die Treppenkurve des besten Plans (echte Werte) mit der
/// bewiesenen Grenze und dem Streifen dazwischen, der noch zu holen ist. Bei jeder Verbesserung pulst ein grüner Rahmen.
/// </summary>
public sealed class Suchraumbild : Control
{
    /// <summary>Definiert die Eigenschaft <see cref="Verlauf"/>.</summary>
    public static readonly StyledProperty<IReadOnlyList<Gueteschritt>?> VerlaufProperty =
        AvaloniaProperty.Register<Suchraumbild, IReadOnlyList<Gueteschritt>?>(nameof(Verlauf));

    /// <summary>Definiert die Eigenschaft <see cref="Sekunden"/>.</summary>
    public static readonly StyledProperty<double> SekundenProperty = AvaloniaProperty.Register<Suchraumbild, double>(nameof(Sekunden));

    /// <summary>Definiert die Eigenschaft <see cref="Laeuft"/>.</summary>
    public static readonly StyledProperty<bool> LaeuftProperty = AvaloniaProperty.Register<Suchraumbild, bool>(nameof(Laeuft));

    private const int Punkte = 230;
    private const double Pulsdauer = 1.4;
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");
    private static readonly double[] Schritte = [1, 2, 5, 10, 20, 50, 100, 200, 500, 1000];
    private readonly DispatcherTimer takt = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly (double Radius, double Winkel)[] punkte = Streuung();
    private readonly Stopwatch uhr = Stopwatch.StartNew();
    private double bewegung;
    private double skala = 1;
    private double pulsStart = -100;
    private int letzteAnzahl;

    static Suchraumbild() => AffectsRender<Suchraumbild>(VerlaufProperty, SekundenProperty, LaeuftProperty);

    /// <summary>Initialisiert die Anzeige; ein Takt bewegt sie, solange sie sichtbar ist.</summary>
    public Suchraumbild()
    {
        takt.Tick += (_, _) =>
        {
            if (!IsEffectivelyVisible)
            {
                return;
            }

            if (Laeuft)
            {
                bewegung += 0.04;
            }

            skala += (ZielSkala() - skala) * 0.07;
            InvalidateVisual();
        };
    }

    /// <summary>Holt oder setzt den Güteverlauf des besten Plans.</summary>
    public IReadOnlyList<Gueteschritt>? Verlauf
    {
        get => GetValue(VerlaufProperty);
        set => SetValue(VerlaufProperty, value);
    }

    /// <summary>Holt oder setzt die Rechenzeit am Ende der Zeitachse.</summary>
    public double Sekunden
    {
        get => GetValue(SekundenProperty);
        set => SetValue(SekundenProperty, value);
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob gerechnet wird (sonst steht die Bewegung still).</summary>
    public bool Laeuft
    {
        get => GetValue(LaeuftProperty);
        set => SetValue(LaeuftProperty, value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        double b = Bounds.Width;
        double h = Bounds.Height;
        if (b < 160 || h < 120)
        {
            return;
        }

        IReadOnlyList<Gueteschritt> verlauf = Verlauf ?? [];
        int anzahl = verlauf.Count;
        if (anzahl > letzteAnzahl)
        {
            pulsStart = uhr.Elapsed.TotalSeconds;
        }

        letzteAnzahl = anzahl;
        double seite = Math.Min(h - 8, Math.Min(b * 0.40, 300));
        ZeichneRaum(context, new Rect(4, (h - seite) / 2, seite, seite), verlauf);
        var diagramm = new Rect(seite + 56, 14, Math.Max(40, b - seite - 56 - 14), h - 14 - 28);
        ZeichneKurve(context, diagramm, verlauf);
        double phase = (uhr.Elapsed.TotalSeconds - pulsStart) / Pulsdauer;
        if (phase is >= 0 and < 1)
        {
            var farbe = Pinsel.Mit(Pinsel.Farbe("Gut"), 0.85 * (1 - phase));
            context.DrawRectangle(null, new Pen(farbe, 4), new Rect(1, 1, b - 2, h - 2), 10, 10);
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        takt.Start();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        takt.Stop();
    }

    private static (double Radius, double Winkel)[] Streuung() =>
        [.. Enumerable.Range(0, Punkte).Select(i => (Math.Sqrt((i + 0.5) / Punkte), i * 2.399963))];

    private static double Schritt(double spanne)
    {
        double grob = spanne / 4;
        return Schritte.FirstOrDefault(s => s >= grob, Schritte[^1]);
    }

    private static string Zeit(double sekunden)
    {
        int s = (int)Math.Round(sekunden);
        return string.Create(Deutsch, $"{s / 60}:{s % 60:00}");
    }

    private static FormattedText Text(string text, double groesse, IBrush farbe) =>
        new(text, Deutsch, FlowDirection.LeftToRight, Typeface.Default, groesse, farbe);

    private double ZielSkala()
    {
        IReadOnlyList<Gueteschritt>? verlauf = Verlauf;
        if (verlauf is not { Count: > 0 })
        {
            return 1;
        }

        double start = verlauf[0].Wert;
        double best = verlauf[^1].Wert;
        double grenze = verlauf[^1].Grenze ?? 0;
        double anteil = Math.Clamp((best - grenze) / Math.Max(1, start - grenze), 0, 1);
        return 0.30 + (0.70 * anteil);
    }

    private void ZeichneRaum(DrawingContext context, Rect feld, IReadOnlyList<Gueteschritt> verlauf)
    {
        var mitte = feld.Center;
        double radius = (feld.Width / 2) - 4;
        context.DrawEllipse(Pinsel.Farbe("Grund"), new Pen(Pinsel.Farbe("Linie"), 1), mitte, radius + 8, radius + 8);
        IBrush blau = Pinsel.Mit(Pinsel.Farbe("Akzent"), 0.85);
        IBrush grau = Pinsel.Mit(Pinsel.Farbe("Grau"), 0.35);
        for (int i = 0; i < punkte.Length; i++)
        {
            (double r, double w) = punkte[i];
            double wackeln = Math.Sin(bewegung + i) * 0.8;
            var ort = new Point(mitte.X + (radius * r * Math.Cos(w)) + wackeln, mitte.Y + (radius * r * Math.Sin(w)) + (wackeln * 0.7));
            context.DrawEllipse(r <= skala ? blau : grau, null, ort, 2.2, 2.2);
        }

        double rand = radius * skala;
        var umriss = new StreamGeometry();
        using (StreamGeometryContext g = umriss.Open())
        {
            const int Ecken = 48;
            for (int i = 0; i <= Ecken; i++)
            {
                double w = i / (double)Ecken * 2 * Math.PI;
                double rr = rand * (1 + (0.03 * Math.Sin((3 * w) + (bewegung * 0.8))) + (0.02 * Math.Sin((5 * w) - (bewegung * 1.1))));
                var p = new Point(mitte.X + (rr * Math.Cos(w)), mitte.Y + (rr * Math.Sin(w)));
                if (i == 0)
                {
                    g.BeginFigure(p, true);
                }
                else
                {
                    g.LineTo(p);
                }
            }

            g.EndFigure(true);
        }

        context.DrawGeometry(Pinsel.Mit(Pinsel.Farbe("Akzent"), 0.07), new Pen(Pinsel.Farbe("Akzent"), 2, new DashStyle([3, 2.5], -bewegung * 9)), umriss);
        if (verlauf.Count > 0 && verlauf[^1].Grenze is int grenze && grenze > 0)
        {
            double kern = radius * Math.Min(skala * 0.9, 0.12 + (0.18 * Math.Clamp(grenze / (double)Math.Max(1, verlauf[0].Wert), 0, 1)));
            var kreis = new EllipseGeometry(new Rect(mitte.X - kern, mitte.Y - kern, 2 * kern, 2 * kern));
            context.DrawGeometry(new SolidColorBrush(Color.Parse("#D5D8DD")), new Pen(new SolidColorBrush(Color.Parse("#8A919B")), 1), kreis);
            using (context.PushGeometryClip(kreis))
            {
                var strich = new Pen(new SolidColorBrush(Color.Parse("#8A919B")), 2);
                for (double x = -2 * kern; x < 2 * kern; x += 6)
                {
                    context.DrawLine(strich, new Point(mitte.X + x, mitte.Y + kern), new Point(mitte.X + x + (2 * kern), mitte.Y - kern));
                }
            }
        }
    }

    private void ZeichneKurve(DrawingContext context, Rect feld, IReadOnlyList<Gueteschritt> verlauf)
    {
        IBrush linie = Pinsel.Farbe("Linie");
        IBrush beschriftung = Pinsel.Farbe("Tinte2");
        if (verlauf.Count == 0)
        {
            FormattedText leer = Text("Sobald der erste Plan steht, wächst hier die Kurve.", 12, beschriftung);
            context.DrawText(leer, new Point(feld.Left, feld.Center.Y - (leer.Height / 2)));
            return;
        }

        double vmax = verlauf.Max(p => p.Wert);
        double vmin = Math.Min(verlauf.Min(p => p.Wert), verlauf.Where(p => p.Grenze is not null).Select(p => (double)p.Grenze!.Value).DefaultIfEmpty(double.MaxValue).Min());
        double spanne = Math.Max(5, vmax - vmin);
        double schritt = Schritt(spanne * 1.3);
        double unten = Math.Max(0, Math.Floor((vmin - (spanne * 0.15)) / schritt) * schritt);
        double oben = Math.Ceiling((vmax + (spanne * 0.1)) / schritt) * schritt;
        double tmax = Math.Max(10, Math.Max(Sekunden, verlauf[^1].Sekunden));
        double X(double t) => feld.Left + (t / tmax * feld.Width);
        double Y(double v) => feld.Bottom - ((v - unten) / Math.Max(1, oben - unten) * feld.Height);

        for (double v = unten; v <= oben + 0.001; v += schritt)
        {
            context.DrawLine(new Pen(linie, 1), new Point(feld.Left, Y(v)), new Point(feld.Right, Y(v)));
            FormattedText zahl = Text(v.ToString("0", Deutsch), 10, beschriftung);
            context.DrawText(zahl, new Point(feld.Left - 6 - zahl.Width, Y(v) - (zahl.Height / 2)));
        }

        FormattedText von = Text("0:00", 10, beschriftung);
        FormattedText bis = Text(Zeit(tmax), 10, beschriftung);
        context.DrawText(von, new Point(feld.Left, feld.Bottom + 6));
        context.DrawText(bis, new Point(feld.Right - bis.Width, feld.Bottom + 6));

        // Streifen zwischen Kurve und Grenze: das, was noch zu holen ist.
        var streifen = new StreamGeometry();
        using (StreamGeometryContext g = streifen.Open())
        {
            g.BeginFigure(new Point(X(verlauf[0].Sekunden), Y(verlauf[0].Wert)), true);
            for (int i = 1; i < verlauf.Count; i++)
            {
                g.LineTo(new Point(X(verlauf[i].Sekunden), Y(verlauf[i - 1].Wert)));
                g.LineTo(new Point(X(verlauf[i].Sekunden), Y(verlauf[i].Wert)));
            }

            g.LineTo(new Point(X(tmax), Y(verlauf[^1].Wert)));
            g.LineTo(new Point(X(tmax), Y(verlauf[^1].Grenze ?? unten)));
            for (int i = verlauf.Count - 1; i >= 1; i--)
            {
                g.LineTo(new Point(X(verlauf[i].Sekunden), Y(verlauf[i].Grenze ?? unten)));
                g.LineTo(new Point(X(verlauf[i].Sekunden), Y(verlauf[i - 1].Grenze ?? unten)));
            }

            g.LineTo(new Point(X(verlauf[0].Sekunden), Y(verlauf[0].Grenze ?? unten)));
            g.EndFigure(true);
        }

        context.DrawGeometry(Pinsel.Mit(Pinsel.Farbe("Akzent"), 0.10), null, streifen);

        // Bewiesene Grenze (gestrichelt, nur wo bekannt) und beste Lösung (Treppe).
        var grenzstrich = new Pen(Pinsel.Farbe("Grau"), 2, new DashStyle([4, 3], 0));
        for (int i = 0; i < verlauf.Count; i++)
        {
            if (verlauf[i].Grenze is not int g)
            {
                continue;
            }

            double bis2 = i + 1 < verlauf.Count ? X(verlauf[i + 1].Sekunden) : X(tmax);
            context.DrawLine(grenzstrich, new Point(X(verlauf[i].Sekunden), Y(g)), new Point(bis2, Y(g)));
        }

        var treppe = new StreamGeometry();
        using (StreamGeometryContext g = treppe.Open())
        {
            g.BeginFigure(new Point(X(verlauf[0].Sekunden), Y(verlauf[0].Wert)), false);
            for (int i = 1; i < verlauf.Count; i++)
            {
                g.LineTo(new Point(X(verlauf[i].Sekunden), Y(verlauf[i - 1].Wert)));
                g.LineTo(new Point(X(verlauf[i].Sekunden), Y(verlauf[i].Wert)));
            }

            g.LineTo(new Point(X(tmax), Y(verlauf[^1].Wert)));
            g.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(Pinsel.Farbe("Akzent"), 2.6, lineJoin: PenLineJoin.Round), treppe);
        IBrush gruen = Pinsel.Farbe("Gut");
        for (int i = 1; i < verlauf.Count; i++)
        {
            context.DrawEllipse(gruen, null, new Point(X(verlauf[i].Sekunden), Y(verlauf[i].Wert)), 4, 4);
        }

        double phase = (uhr.Elapsed.TotalSeconds - pulsStart) / Pulsdauer;
        if (phase is >= 0 and < 1 && verlauf.Count > 1)
        {
            var ring = new Pen(Pinsel.Mit(gruen, 1 - phase), 2);
            context.DrawEllipse(null, ring, new Point(X(verlauf[^1].Sekunden), Y(verlauf[^1].Wert)), 4 + (16 * phase), 4 + (16 * phase));
        }
    }
}
