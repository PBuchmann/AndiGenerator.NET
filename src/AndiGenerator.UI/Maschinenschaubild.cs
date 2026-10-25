// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using AndiGenerator.Engine.Schranke;
using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace AndiGenerator.UI;

/// <summary>
/// Schaubild der Maschine im Leitstand: in der Mitte der beste Plan, darüber der Kasten, der vergleicht und lenkt (Automodus
/// bzw. bei der Kostenoptimierung der Koordinator), links die Inseln, rechts HiGHS. Beide Seiten liefern Pläne; ein besserer
/// wandert als Punkt zum besten Plan (grün von einer Insel, orange von HiGHS), ein blauer Punkt zeigt, wenn HiGHS den
/// besseren Plan der Inseln übernimmt. Darunter steht nach einer Verbesserung, was sich geändert hat.
/// Je Insel steht grob, was sie gerade tut, und ein schmaler Balken, wie nah sie einem Neustart ist; eine Insel, die neu
/// beginnt, wird kurz grau. Der Kasten von HiGHS zeigt die untere Schranke und die Teilneuplanung mit einem Kästchen je
/// Ausschnitt und dem Erfolg je Größe der Ausschnitte.
/// </summary>
public sealed class Maschinenschaubild : Control
{
    /// <summary>Definiert die Eigenschaft <see cref="Schaubild"/>.</summary>
    public static readonly StyledProperty<Schaubild?> SchaubildProperty =
        AvaloniaProperty.Register<Maschinenschaubild, Schaubild?>(nameof(Schaubild));

    /// <summary>Definiert die Eigenschaft <see cref="Version"/>.</summary>
    public static readonly StyledProperty<int> VersionProperty =
        AvaloniaProperty.Register<Maschinenschaubild, int>(nameof(Version));

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private readonly DispatcherTimer takt = new() { Interval = TimeSpan.FromMilliseconds(40) };

    static Maschinenschaubild()
    {
        AffectsRender<Maschinenschaubild>(SchaubildProperty, VersionProperty);
        AffectsMeasure<Maschinenschaubild>(SchaubildProperty, VersionProperty);
    }

    /// <summary>Initialisiert das Schaubild; ein Takt bewegt die Ereignisse, sonst steht das Bild still.</summary>
    public Maschinenschaubild()
    {
        takt.Tick += (_, _) =>
        {
            if (Schaubild is { } bild && (bild.Impulse.Count > 0 || Bild.ZeigtAenderungen(bild)))
            {
                InvalidateVisual();
            }
        };
    }

    /// <summary>Holt oder setzt den Zustand des Schaubilds.</summary>
    public Schaubild? Schaubild
    {
        get => GetValue(SchaubildProperty);
        set => SetValue(SchaubildProperty, value);
    }

    /// <summary>Holt oder setzt die Version des Zustands (löst das Neuzeichnen aus).</summary>
    public int Version
    {
        get => GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Schaubild is not { Kerne: > 0 } bild || Bounds.Width < 300 || Bounds.Height < 120)
        {
            context.DrawText(Bild.Text("Das Schaubild erscheint, sobald gerechnet wird.", Pinsel.Farbe("Grau"), 11, false, 0), new Point(4, 4));
            return;
        }

        new Bild(context, bild, Bounds.Size).Zeichnen();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Die Höhe wächst mit der Zahl der Inseln (je Insel eine Zeile), statt die Inseln zu stauchen; mindestens 300 Pixel (Platz für die Änderungen unter dem besten Plan).
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        Schaubild? bild = Schaubild;
        int zeilen = Math.Max(1, bild?.Inseln.Count ?? 0) + (bild?.Spezial == true ? 1 : 0);
        double hoehe = (2 * Bild.Rand) + (zeilen * Bild.Zeilenhoehe) + ((zeilen - 1) * Bild.Zeilenabstand);
        double highs = bild is null ? 0 : Bild.Highshoehe(bild) + (2 * Bild.Rand);
        double breite = double.IsFinite(availableSize.Width) ? availableSize.Width : 600;
        return new Size(breite, Math.Max(300, Math.Max(hoehe, highs)));
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

    /// <summary>Zeichnet das Schaubild einmal (Lage der Kästen, Linien, Inseln, HiGHS, Ereignisse).</summary>
    private sealed class Bild
    {
        /// <summary>Abstand zum Rand.</summary>
        public const double Rand = 6;

        /// <summary>Höhe einer Inselzeile (Name und Wert, darunter Tätigkeit und Balken).</summary>
        public const double Zeilenhoehe = 34;

        /// <summary>Abstand zwischen zwei Inselzeilen.</summary>
        public const double Zeilenabstand = 6;

        private const double Innen = 6;

        private const double Lenkungshoehe = 40;

        private const double Besterhoehe = 70;

        /// <summary>Kantenlänge eines Kästchens je Ausschnitt.</summary>
        private const double Kante = 7;

        /// <summary>So lange (Sekunden) wandert der Punkt bis zum besten Plan.</summary>
        private const double Wanderzeit = Presentation.Schaubild.Wanderzeit;

        /// <summary>So lange (Sekunden) nach der Ankunft des letzten Punkts steht, was sich geändert hat.</summary>
        private const double Aenderungsdauer = Presentation.Schaubild.Anzeigedauer;

        private readonly Schaubild bild;
        private readonly long jetzt;
        private readonly Rect[] inseln;
        private readonly Rect? spezial;
        private readonly Rect lenkung;
        private readonly Rect bester;
        private readonly Rect mitte;
        private readonly Rect? highs;
        private DrawingContext context;

        public Bild(DrawingContext context, Schaubild bild, Size groesse)
        {
            this.context = context;
            this.bild = bild;
            Groesse = groesse;
            jetzt = Stopwatch.GetTimestamp();
            double w = groesse.Width;
            double h = groesse.Height;

            // Drei Spalten: links die Inseln, in der Mitte Lenkung und bester Plan, rechts HiGHS (wenn es rechnet).
            bool mitHighs = bild.ZeigtHighs;
            double inselbreite = w * (mitHighs ? 0.30 : 0.40);
            double mitteLinks = w * (mitHighs ? 0.36 : 0.48);
            double mitteBreite = w * (mitHighs ? 0.27 : 0.40);
            mitte = new Rect(mitteLinks, Rand, mitteBreite, h - (2 * Rand));
            lenkung = new Rect(mitteLinks, Rand, mitteBreite, Lenkungshoehe);
            double y = Math.Max(lenkung.Bottom + 24, (h * 0.42) - (Besterhoehe / 2));
            bester = new Rect(mitteLinks, y, mitteBreite, Besterhoehe);
            highs = mitHighs ? Highskasten(w * 0.68, (w * 0.32) - Rand, h) : null;
            (inseln, spezial) = Inselkaesten(inselbreite, h);
        }

        private Size Groesse { get; }

        public static FormattedText Text(string text, IBrush farbe, double groesse, bool fett, double breite, int zeilen = 1)
        {
            var formatiert = new FormattedText(text, Deutsch, FlowDirection.LeftToRight, new Typeface(Schriftart(), FontStyle.Normal, fett ? FontWeight.SemiBold : FontWeight.Normal), groesse, farbe);
            if (breite > 0)
            {
                formatiert.MaxTextWidth = breite;
                formatiert.Trimming = TextTrimming.CharacterEllipsis;
                formatiert.MaxLineCount = zeilen;
            }

            return formatiert;
        }

        /// <summary>Gibt an, ob gerade steht, was die letzte Verbesserung geändert hat (der Takt muss es ausblenden).</summary>
        public static bool ZeigtAenderungen(Schaubild bild) =>
            bild.Meldungen.Any(m => Stopwatch.GetElapsedTime(m.Zeitpunkt).TotalSeconds < Wanderzeit + Aenderungsdauer + 0.2);

        /// <summary>So hoch wird der Kasten von HiGHS für seinen Inhalt (Zeilen wie in <see cref="Highs"/>).</summary>
        public static double Highshoehe(Schaubild bild)
        {
            if (!bild.ZeigtHighs)
            {
                return 0;
            }

            double hoehe = 8 + 16;
            if (bild.Schranke is not null)
            {
                hoehe += 8 + 14 + 7 + 14;
            }

            if (bild.Teilneuplanung is { } t)
            {
                hoehe += 8 + 14 + (2 * (Kante + 2)) + 4 + (3 * 14) + 6 + 14 + ((t.Groessen?.Count ?? 0) * 14);
            }

            return hoehe;
        }

        public void Zeichnen()
        {
            Linien();
            for (int i = 0; i < inseln.Length; i++)
            {
                Insel(i);
            }

            if (spezial is Rect s)
            {
                Kastenzeichnen(s, "Spezial-Insel", "eine Kostenart extrem", null, gestrichelt: true);
            }

            double anhebung = Leuchten(Schaubildereignis.Anhebung, 0, 1.5);
            Kastenzeichnen(lenkung, bild.Lenkung, bild.LenkungText, anhebung > 0 ? (Pinsel.Farbe("Akzent"), anhebung) : null);
            Bester();
            if (highs is Rect kasten)
            {
                _ = Highs(kasten);
            }

            Aenderungen();
            Ereignisse();
        }

        private static FontFamily Schriftart() =>
            global::Avalonia.Application.Current?.TryFindResource("Grundschrift", out object? schrift) == true && schrift is FontFamily f ? f : FontFamily.Default;

        private static Point Rechts(Rect r) => new(r.Right, r.Center.Y);

        private static Point Links(Rect r) => new(r.Left, r.Center.Y);

        /// <summary>Punkt auf einem Weg aus Strecken beim Anteil <paramref name="t"/> (0 … 1) seiner Länge.</summary>
        private static Point Auf(Point[] weg, double t)
        {
            double[] laengen = weg.Zip(weg.Skip(1), (a, b) => Point.Distance(a, b)).ToArray();
            double rest = Math.Clamp(t, 0, 1) * laengen.Sum();
            for (int i = 0; i < laengen.Length; i++)
            {
                if (rest <= laengen[i] || i == laengen.Length - 1)
                {
                    double f = laengen[i] > 0 ? Math.Min(1, rest / laengen[i]) : 0;
                    return new Point(weg[i].X + ((weg[i + 1].X - weg[i].X) * f), weg[i].Y + ((weg[i + 1].Y - weg[i].Y) * f));
                }

                rest -= laengen[i];
            }

            return weg[^1];
        }

        /// <summary>Weicher Verlauf von 0 nach 1 (langsam los, langsam an).</summary>
        private static double Sanft(double t)
        {
            double x = Math.Clamp(t, 0, 1);
            return x * x * (3 - (2 * x));
        }

        /// <summary>Die Inseln untereinander, mittig zum besten Plan, darunter die Spezial-Insel.</summary>
        private (Rect[] Inseln, Rect? Spezial) Inselkaesten(double breite, double h)
        {
            int anzahl = Math.Max(1, bild.Inseln.Count);
            int zeilen = anzahl + (bild.Spezial ? 1 : 0);
            double platz = h - (2 * Rand);
            double hoehe = Math.Min(Zeilenhoehe, (platz - (Zeilenabstand * (zeilen - 1))) / zeilen);
            double gesamt = (zeilen * hoehe) + (Zeilenabstand * (zeilen - 1));
            double y = Math.Clamp(bester.Center.Y - (gesamt / 2), Rand, Math.Max(Rand, h - Rand - gesamt));
            var kaesten = new Rect[anzahl];
            for (int i = 0; i < anzahl; i++)
            {
                kaesten[i] = new Rect(Rand, y, breite, hoehe);
                y += hoehe + Zeilenabstand;
            }

            Rect? sonder = bild.Spezial ? new Rect(Rand, y, breite, hoehe) : null;
            return (kaesten, sonder);
        }

        /// <summary>Senkrechte, an der die Linien der Inseln zusammenlaufen (zwischen Inseln und Mitte).</summary>
        private double Sammelx() => (inseln[0].Right + mitte.Left) / 2;

        private void Linien()
        {
            var stift = new Pen(Pinsel.Farbe("Rand"), 1.5);
            double x = Sammelx();
            foreach (Rect insel in inseln)
            {
                context.DrawLine(stift, Rechts(insel), new Point(x, bester.Center.Y));
            }

            if (spezial is Rect s)
            {
                context.DrawLine(new Pen(Pinsel.Farbe("Rand"), 1.5, new DashStyle([3, 3], 0)), Rechts(s), new Point(x, bester.Center.Y));
            }

            context.DrawLine(stift, new Point(x, bester.Center.Y), Links(bester));

            // Der Kasten oben vergleicht und wählt den besten Plan (senkrecht) und lenkt die Inseln (gestrichelt).
            context.DrawLine(stift, new Point(lenkung.Center.X, lenkung.Bottom), new Point(lenkung.Center.X, bester.Top));
            var gestrichelt = new Pen(Pinsel.Farbe("Rand"), 1.2, new DashStyle([3, 3], 0));
            Point[] weg = Lenkungsweg();
            for (int i = 1; i < weg.Length; i++)
            {
                context.DrawLine(gestrichelt, weg[i - 1], weg[i]);
            }

            if (highs is Rect h)
            {
                // HiGHS nimmt den besten Plan und gibt einen besseren zurück.
                context.DrawLine(new Pen(Pinsel.Farbe("Highs"), 1.5), Rechts(bester), new Point(h.Left, bester.Center.Y));
            }
        }

        /// <summary>Vom Kasten oben zu den Inseln: Er lenkt ihre Gewichte und startet sie neu.</summary>
        private Point[] Lenkungsweg()
        {
            double x = inseln[0].Center.X;
            return [Links(lenkung), new Point(x, lenkung.Center.Y), new Point(x, inseln[0].Top)];
        }

        /// <summary>
        /// Eine Insel: Name und Wert in der ersten Zeile, darunter grob, was sie tut, und ein schmaler Balken, wie nah sie
        /// einem Neustart ist. Die Insel mit dem besten Plan hat einen grünen Rand.
        /// </summary>
        private void Insel(int i)
        {
            Rect kasten = inseln[i];
            Inselzustand? zustand = i < bild.Inseln.Count ? bild.Inseln[i] : null;
            bool beste = i == bild.BesteInsel;
            context.DrawRectangle(Pinsel.Farbe("Flaeche"), new Pen(beste ? Pinsel.Farbe("Gut") : Pinsel.Farbe("Linie"), beste ? 2 : 1), kasten, 8, 8);
            double breite = kasten.Width - 12;
            FormattedText titel = Text("Insel " + (i + 1).ToString(Deutsch), Pinsel.Farbe("Tinte"), 11, true, breite / 2);
            FormattedText wert = Text(zustand?.Wert ?? string.Empty, Pinsel.Farbe("Tinte"), 10, false, breite / 2);
            context.DrawText(titel, new Point(kasten.Left + 6, kasten.Top + 2));
            context.DrawText(wert, new Point(kasten.Right - 6 - wert.Width, kasten.Top + 3));
            if (zustand is not null && kasten.Height >= 28)
            {
                double balkenBreite = breite * 0.3;
                IBrush tintenfarbe = zustand.Taetigkeit == "kommt voran" ? Pinsel.Farbe("Gut") : Pinsel.Farbe("Grau");
                FormattedText taetigkeit = Text(zustand.Taetigkeit, tintenfarbe, 10, false, breite - balkenBreite - 8);
                double y = kasten.Top + 2 + titel.Height;
                context.DrawText(taetigkeit, new Point(kasten.Left + 6, y));
                var spur = new Rect(kasten.Right - 6 - balkenBreite, y + (taetigkeit.Height / 2) - 2, balkenBreite, 4);
                context.DrawRectangle(Pinsel.Farbe("Linie"), null, spur, 2, 2);
                context.DrawRectangle(Pinsel.Farbe("Grau"), null, spur.WithWidth(balkenBreite * zustand.Stillstand), 2, 2);
            }

            double neu = Leuchten(Schaubildereignis.Neustart, 0, 1.4, i);
            if (neu > 0)
            {
                context.DrawRectangle(Pinsel.Mit(Pinsel.Farbe("Grau"), 0.3 * neu), null, kasten, 8, 8);
            }
        }

        /// <summary>
        /// Der beste Plan: sein Wert, im Automodus darunter, woher er zuletzt kam und wie viele Verbesserungen von den Inseln
        /// und von HiGHS stammen. Er leuchtet auf, wenn ein Punkt ankommt (grün von einer Insel, orange von HiGHS).
        /// </summary>
        private void Bester()
        {
            double gruen = Leuchten(Schaubildereignis.BesterPlan, Wanderzeit, 1.2);
            double orange = Leuchten(Schaubildereignis.Teilneuplanung, Wanderzeit, 1.2);
            context.DrawRectangle(Pinsel.Farbe("Flaeche"), new Pen(Pinsel.Farbe("Linie"), 1), bester, 8, 8);
            if (Math.Max(gruen, orange) > 0)
            {
                IBrush farbe = gruen >= orange ? Pinsel.Farbe("Gut") : Pinsel.Farbe("Highs");
                double staerke = Math.Max(gruen, orange);
                context.DrawRectangle(Pinsel.Mit(farbe, 0.12 * staerke), new Pen(Pinsel.Mit(farbe, staerke), 2), bester, 8, 8);
            }

            double breite = bester.Width - (2 * Innen);
            double x = bester.Left + Innen;
            FormattedText titel = Text("Bester Plan", Pinsel.Farbe("Tinte"), 11, true, breite);
            FormattedText wert = Text(bild.Bester, Pinsel.Farbe("Tinte"), 15, true, breite);
            double y = bester.Top + 5;
            context.DrawText(titel, new Point(x, y));
            y += titel.Height;
            context.DrawText(wert, new Point(x, y));
            y += wert.Height + 1;
            double frei = breite;
            if (bild.ZuletztVon is { } von)
            {
                IBrush farbe = von is "HiGHS" or "Teilneuplanung" ? Pinsel.Farbe("HighsDunkel") : Pinsel.Farbe("Gut");
                FormattedText zuletzt = Text("zuletzt von " + von, farbe, 10, true, breite / 2);
                context.DrawText(zuletzt, new Point(x, y));
                frei -= zuletzt.Width + 6;
            }

            if (bild.Herkunft.Length > 0)
            {
                FormattedText anzahl = Text(bild.Herkunft, Pinsel.Farbe("Grau"), 10, false, frei);
                context.DrawText(anzahl, new Point(bester.Right - Innen - anzahl.Width, y));
            }
        }

        /// <summary>
        /// Der Kasten von HiGHS rechts: die untere Schranke mit ihrem Schritt und einem Balken, darunter die Teilneuplanung mit
        /// einem Kästchen je Ausschnitt (grün besser, rot abgelehnt, grau nichts Besseres, blau Plan der Inseln übernommen,
        /// umrandet der laufende), ihrem Plan, dem Erfolg je Größe der Ausschnitte und wie oft ihr Plan bester Plan wurde.
        /// </summary>
        private double Highs(Rect kasten)
        {
            IBrush orange = Pinsel.Farbe("Highs");
            bool rechnet = bild.HighsRechnet;
            context.DrawRectangle(Pinsel.Farbe("Flaeche"), new Pen(orange, rechnet ? 1.5 : 1), kasten, 8, 8);
            context.DrawRectangle(Pinsel.Mit(orange, 0.05), null, kasten, 8, 8);
            double breite = kasten.Width - (2 * Innen);
            double links = kasten.Left + Innen;
            double y = kasten.Top + 4;
            double unten = kasten.Bottom - 4;
            string anteil = bild.Last is { Highs: > 0.001 } last
                ? string.Create(Deutsch, $"rechnet · {last.Highs:P0}")
                : "rechnet";
            y += Zeile("HiGHS", rechnet ? anteil : "wartet", y, Pinsel.Farbe("HighsDunkel"), true);
            if (bild.Schranke is { } s)
            {
                y += 8 + Zeile("Untere Schranke", string.Create(Deutsch, $"Schritt {s.Schritt} von {s.Schritte}"), y, Pinsel.Farbe("Tinte"), true);
                var spur = new Rect(links, y + 1, breite, 4);
                context.DrawRectangle(Pinsel.Farbe("Linie"), null, spur, 2, 2);
                context.DrawRectangle(orange, null, spur.WithWidth(breite * Math.Clamp((s.Schritt - 0.5) / Math.Max(1, s.Schritte), 0, 1)), 2, 2);
                y += 7;
                if (y + 12 < unten)
                {
                    y += Zeile(s.Text, string.Empty, y, Pinsel.Farbe("Grau"), false);
                }
            }

            if (bild.Teilneuplanung is not { } t || y + 30 > unten)
            {
                return y + 4 - kasten.Top;
            }

            string zustand = t.Mannschaften is int m ? string.Create(Deutsch, $"plant {m} Mannschaften neu") : t.Zustand;
            y += 8 + Zeile("Teilneuplanung", string.Create(Deutsch, $"{t.Besser} von {t.Ausschnitte} besser"), y, Pinsel.Farbe("Tinte"), true);
            if (t.Verlauf is { Count: > 0 } || t.Mannschaften is not null)
            {
                y += Ausschnitte(t, links, y + 2, breite) + 4;
            }

            if (y + 12 < unten)
            {
                // Ein Fehler kann länger sein: bis zu drei Zeilen.
                y += Zeile(zustand, string.Empty, y, Pinsel.Farbe("Grau"), false, 3);
            }

            if (y + 12 < unten && t.Aktuell is { } eigener)
            {
                y += Zeile("ihr Plan", string.Create(Deutsch, $"A {eigener.A} · B {eigener.B} · C {eigener.CAnzahl}"), y, Pinsel.Farbe("Tinte"), false);
            }

            if (y + 12 < unten)
            {
                y += Zeile(string.Create(Deutsch, $"→ bester Plan {t.Eingespielt}×"), string.Create(Deutsch, $"← Plan der Inseln {t.Wechsel}×"), y, t.Eingespielt > 0 ? Pinsel.Farbe("HighsDunkel") : Pinsel.Farbe("Grau"), false);
            }

            // Erfolg je Größe der Ausschnitte: Balken = Anteil besser, rechts angenommen/geplant und Sekunden je Ausschnitt.
            if (t.Groessen is { Count: > 0 } groessen && y + 26 < unten)
            {
                y += 6 + Zeile("Erfolg je Größe des Ausschnitts", string.Empty, y, Pinsel.Farbe("Grau"), false);
                foreach (Ausschnittsgroesse g in groessen)
                {
                    if (y + 12 > unten)
                    {
                        break;
                    }

                    y += Groessenzeile(g, links, y, breite);
                }
            }

            return y + 4 - kasten.Top;

            double Zeile(string text, string rechts, double oben, IBrush farbe, bool fett, int zeilen = 1)
            {
                FormattedText r = Text(rechts, Pinsel.Farbe("Grau"), 10, false, breite / 2);
                FormattedText l = Text(text, farbe, fett ? 11 : 10, fett, breite - (rechts.Length > 0 ? r.Width + 6 : 0), zeilen);
                context.DrawText(l, new Point(links, oben));
                if (rechts.Length > 0)
                {
                    context.DrawText(r, new Point(links + breite - r.Width, oben + (l.Height - r.Height)));
                }

                return l.Height + 1;
            }
        }

        /// <summary>
        /// Lage des Kastens von HiGHS: so hoch wie sein Inhalt (einmal unsichtbar gezeichnet und gemessen), senkrecht mittig zur
        /// Linie vom besten Plan, im Bild gehalten.
        /// </summary>
        private Rect Highskasten(double links, double breite, double h)
        {
            DrawingContext echt = context;
            double inhalt;
            var probe = new DrawingGroup();
            using (DrawingContext messen = probe.Open())
            {
                context = messen;
                inhalt = Highs(new Rect(links, 0, breite, 10_000));
            }

            context = echt;
            double hoehe = Math.Min(h - (2 * Rand), inhalt);
            double oben = Math.Clamp(bester.Center.Y - (hoehe / 2), Rand, Math.Max(Rand, h - Rand - hoehe));
            return new Rect(links, oben, breite, hoehe);
        }

        /// <summary>Eine Größe der Ausschnitte: Name, Balken (Anteil besser), angenommen/geplant und Sekunden je Ausschnitt.</summary>
        /// <returns>Die Höhe der Zeile.</returns>
        private double Groessenzeile(Ausschnittsgroesse g, double links, double oben, double breite)
        {
            FormattedText name = Text(string.Create(Deutsch, $"{g.Mannschaften} Mannsch."), Pinsel.Farbe("Tinte"), 10, false, 0);
            string zahlen = string.Create(Deutsch, $"{g.Besser}/{g.Ausschnitte} · {g.Sekunden / Math.Max(1, g.Ausschnitte):0.0} s");
            FormattedText rechts = Text(zahlen, Pinsel.Farbe("Grau"), 10, false, 0);
            double balkenLinks = links + 62;
            double balkenBreite = Math.Max(10, breite - 62 - rechts.Width - 8);
            context.DrawText(name, new Point(links, oben));
            var spur = new Rect(balkenLinks, oben + (name.Height / 2) - 3, balkenBreite, 6);
            context.DrawRectangle(Pinsel.Farbe("Linie"), null, spur, 3, 3);
            double anteil = g.Ausschnitte > 0 ? (double)g.Besser / g.Ausschnitte : 0;
            if (anteil > 0)
            {
                context.DrawRectangle(Pinsel.Farbe("Gut"), null, spur.WithWidth(Math.Max(3, balkenBreite * anteil)), 3, 3);
            }

            context.DrawText(rechts, new Point(links + breite - rechts.Width, oben));
            return name.Height + 1;
        }

        /// <summary>Je Ausschnitt ein Kästchen in bis zu zwei Zeilen, die neuesten unten rechts.</summary>
        /// <returns>Die Höhe der Kästchen.</returns>
        private double Ausschnitte(Teilneuplanungsstand t, double links, double oben, double breite)
        {
            const double Abstand = 2;
            int jeZeile = Math.Max(1, (int)((breite + Abstand) / (Kante + Abstand)));
            IReadOnlyList<Ausschnittsergebnis> verlauf = t.Verlauf ?? [];
            bool laeuft = t.Mannschaften is not null;
            int platz = 2 * jeZeile;
            int fertige = Math.Min(verlauf.Count, platz - (laeuft ? 1 : 0));
            int gesamt = fertige + (laeuft ? 1 : 0);
            int zeilen = gesamt > jeZeile ? 2 : 1;
            int n = 0;
            foreach (Ausschnittsergebnis ergebnis in verlauf.Skip(verlauf.Count - fertige))
            {
                IBrush farbe = ergebnis switch
                {
                    Ausschnittsergebnis.Besser => Pinsel.Farbe("Gut"),
                    Ausschnittsergebnis.Ausgangsplan => Pinsel.Farbe("Akzent"),
                    Ausschnittsergebnis.Abgelehnt => Pinsel.Farbe("Schlecht"),
                    _ => Pinsel.Farbe("Rand"),
                };
                context.DrawRectangle(farbe, null, Kaestchen(n++), 1.5, 1.5);
            }

            if (laeuft)
            {
                Rect k = Kaestchen(n);
                context.DrawRectangle(Pinsel.Farbe("Flaeche"), new Pen(Pinsel.Farbe("Highs"), 1.5), k.Deflate(0.75), 1.5, 1.5);
            }

            return (zeilen * Kante) + ((zeilen - 1) * Abstand);

            Rect Kaestchen(int i) => new(links + ((i % jeZeile) * (Kante + Abstand)), oben + ((i / jeZeile) * (Kante + Abstand)), Kante, Kante);
        }

        /// <summary>
        /// Unter dem besten Plan ein Kasten mit dem, was die letzte Verbesserung geändert hat: grün, was besser, rot, was
        /// schlechter wurde. Er erscheint, wenn der Punkt beim besten Plan ankommt, und blendet 10 Sekunden nach der letzten
        /// Ankunft aus (in der letzten Sekunde verblassend).
        /// </summary>
        private void Aenderungen()
        {
            Verbesserungsmeldung? meldung = bild.Meldungen.LastOrDefault(m => Stopwatch.GetElapsedTime(m.Zeitpunkt, jetzt).TotalSeconds >= Wanderzeit);
            if (meldung is null)
            {
                return;
            }

            IReadOnlyList<Planaenderung> liste = meldung.Aenderungen;
            double alter = Stopwatch.GetElapsedTime(meldung.Zeitpunkt, jetzt).TotalSeconds - Wanderzeit;
            if (liste.Count == 0 || alter > Aenderungsdauer)
            {
                return;
            }

            double deckkraft = Math.Min(1, Aenderungsdauer - alter);
            double aufleuchten = Math.Max(0, 1 - (alter / 0.8));
            double breite = bester.Width;
            FormattedText kopf = Text(meldung.Titel, Pinsel.Mit(Pinsel.Farbe("Gut"), deckkraft), 11, true, breite - (2 * Innen));
            var zeilen = new List<FormattedText>();
            double hoehe = (2 * Innen) + kopf.Height + 2;
            double platz = Groesse.Height - Rand - (bester.Bottom + 6);
            foreach (Planaenderung aenderung in liste)
            {
                FormattedText zeile = Text(aenderung.Text, Pinsel.Mit(Pinsel.Farbe(aenderung.Besser ? "Gut" : "Schlecht"), deckkraft), 11, true, breite - (2 * Innen));
                if (hoehe + zeile.Height > platz)
                {
                    break;
                }

                zeilen.Add(zeile);
                hoehe += zeile.Height;
            }

            var kasten = new Rect(bester.Left, bester.Bottom + 6, breite, hoehe);
            context.DrawRectangle(Pinsel.Mit(Pinsel.Farbe("Flaeche"), deckkraft), new Pen(Pinsel.Mit(Pinsel.Farbe("Gut"), deckkraft), 1.5 + (1.5 * aufleuchten)), kasten, 8, 8);
            context.DrawRectangle(Pinsel.Mit(Pinsel.Farbe("Gut"), (0.08 + (0.2 * aufleuchten)) * deckkraft), null, kasten, 8, 8);
            double y = kasten.Top + Innen;
            context.DrawText(kopf, new Point(kasten.Left + Innen, y));
            y += kopf.Height + 2;
            foreach (FormattedText zeile in zeilen)
            {
                context.DrawText(zeile, new Point(kasten.Left + Innen, y));
                y += zeile.Height;
            }
        }

        /// <summary>
        /// Wie stark ein Ereignis noch leuchtet: 0 bis <paramref name="verzug"/> Sekunden danach, dann 1, nach weiteren
        /// <paramref name="dauer"/> Sekunden wieder 0.
        /// </summary>
        private double Leuchten(Schaubildereignis art, double verzug, double dauer, int insel = -1)
        {
            double staerke = 0;
            foreach (Schaubildimpuls impuls in bild.Impulse.Where(x => x.Art == art && (insel < 0 || x.Insel == insel)))
            {
                double alter = Stopwatch.GetElapsedTime(impuls.Zeitpunkt, jetzt).TotalSeconds - verzug;
                if (alter >= 0)
                {
                    staerke = Math.Max(staerke, 1 - (alter / dauer));
                }
            }

            return staerke;
        }

        /// <summary>Punkte auf den Linien: grün von einer Insel, orange von HiGHS, blau zu HiGHS und zu den Inseln.</summary>
        private void Ereignisse()
        {
            foreach (Schaubildimpuls impuls in bild.Impulse)
            {
                double alter = Stopwatch.GetElapsedTime(impuls.Zeitpunkt, jetzt).TotalSeconds;
                Point[]? weg = Weg(impuls);
                double dauer = impuls.Art switch
                {
                    Schaubildereignis.BesterPlan or Schaubildereignis.Teilneuplanung => Wanderzeit,
                    Schaubildereignis.Uebergabe or Schaubildereignis.Ausgangsplan => 1.4,
                    _ => 0.9,
                };
                if (weg is null || alter > dauer)
                {
                    continue;
                }

                IBrush farbe = impuls.Art switch
                {
                    Schaubildereignis.BesterPlan => Pinsel.Farbe("Gut"),
                    Schaubildereignis.Teilneuplanung => Pinsel.Farbe("Highs"),
                    _ => Pinsel.Farbe("Akzent"),
                };
                Point ort = Auf(weg, Sanft(alter / dauer));
                context.DrawEllipse(Pinsel.Mit(farbe, 0.2), null, ort, 8, 8);
                context.DrawEllipse(farbe, null, ort, 4, 4);
            }
        }

        private Point[]? Weg(Schaubildimpuls impuls)
        {
            bool inselGueltig = impuls.Insel >= 0 && impuls.Insel < inseln.Length;
            var sammel = new Point(Sammelx(), bester.Center.Y);
            Point? zuHighs = highs is Rect h ? new Point(h.Left, bester.Center.Y) : null;
            return impuls.Art switch
            {
                Schaubildereignis.BesterPlan when inselGueltig => [Rechts(inseln[impuls.Insel]), sammel, Links(bester)],
                Schaubildereignis.BesterPlan => [sammel, Links(bester)],
                Schaubildereignis.Teilneuplanung when zuHighs is Point p => [p, Rechts(bester)],
                Schaubildereignis.Ausgangsplan when zuHighs is Point p => [Rechts(bester), p],
                Schaubildereignis.Uebergabe when inselGueltig && spezial is Rect s =>
                    [Rechts(s), new Point(s.Right + 10, s.Center.Y), new Point(s.Right + 10, inseln[impuls.Insel].Center.Y), Rechts(inseln[impuls.Insel])],
                Schaubildereignis.Anhebung => Lenkungsweg(),
                _ => null,
            };
        }

        private void Kastenzeichnen(Rect kasten, string titel, string zeile, (IBrush Farbe, double Staerke)? glanz, bool gestrichelt = false)
        {
            var stift = new Pen(Pinsel.Farbe("Linie"), 1, gestrichelt ? new DashStyle([3, 3], 0) : null);
            context.DrawRectangle(Pinsel.Farbe("Flaeche"), stift, kasten, 8, 8);
            if (glanz is ({ } farbe, double staerke))
            {
                context.DrawRectangle(Pinsel.Mit(farbe, 0.12 * staerke), new Pen(Pinsel.Mit(farbe, staerke), 2), kasten, 8, 8);
            }

            double breite = kasten.Width - 12;
            FormattedText oben = Text(titel, Pinsel.Farbe("Tinte"), 11, true, breite);
            FormattedText unten = Text(zeile, Pinsel.Farbe("Grau"), 10, false, breite);
            double hoehe = oben.Height + unten.Height;
            double y = kasten.Center.Y - (hoehe / 2);
            context.DrawText(oben, new Point(kasten.Left + 6, y));
            context.DrawText(unten, new Point(kasten.Left + 6, y + oben.Height));
        }
    }
}
