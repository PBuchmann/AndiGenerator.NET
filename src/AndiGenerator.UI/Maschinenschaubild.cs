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
/// Schaubild der Maschine im Maschinenraum: Inseln → Koordinator → bester Plan, im Automodus mit dem Automodus als
/// Steuerpult, bei der Kostenoptimierung mit der Spezial-Insel. Je Insel steht grob, was sie gerade tut, und ein schmaler
/// Balken, wie nah sie einem Neustart ist. Bewegung gibt es nur bei Ereignissen: Wird der beste Plan besser, wandert ein
/// grüner Punkt von seiner Insel über den Koordinator zum besten Plan; eine Insel, die neu beginnt, wird kurz grau; die
/// Spezial-Insel gibt ihren Plan weiter; der Automodus hebt Gewichte an. Unter dem besten Plan steht nach einer
/// Verbesserung einige Sekunden in einem Kasten, was sich geändert hat (grün besser, rot schlechter).
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
            context.DrawText(Bild.Text("Das Schaubild erscheint, sobald gerechnet wird.", Bild.Farbe("Grau"), 11, false, 0), new Point(4, 4));
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
        double hoehe = (2 * Bild.Rand) + (bild?.Automodus == true ? 44 : 0) + (zeilen * Bild.Zeilenhoehe) + ((zeilen - 1) * Bild.Zeilenabstand);
        double breite = double.IsFinite(availableSize.Width) ? availableSize.Width : 600;
        return new Size(breite, Math.Max(300, hoehe));
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

    /// <summary>Zeichnet das Schaubild einmal (Lage der Kästen, Linien, Inseln, Ereignisse).</summary>
    private sealed class Bild
    {
        /// <summary>Abstand zum Rand.</summary>
        public const double Rand = 6;

        /// <summary>Höhe einer Inselzeile (Name und Wert, darunter Tätigkeit und Balken).</summary>
        public const double Zeilenhoehe = 38;

        /// <summary>Abstand zwischen zwei Inselzeilen.</summary>
        public const double Zeilenabstand = 6;

        private const double KastenHoehe = 48;

        /// <summary>So lange (Sekunden) wandert der grüne Punkt von der Insel bis zum besten Plan.</summary>
        private const double Wanderzeit = Presentation.Schaubild.Wanderzeit;

        /// <summary>So lange (Sekunden) nach der Ankunft des letzten Punkts steht, was sich geändert hat.</summary>
        private const double Aenderungsdauer = Presentation.Schaubild.Anzeigedauer;

        private readonly DrawingContext context;
        private readonly Schaubild bild;
        private readonly long jetzt;
        private readonly Rect[] inseln;
        private readonly Rect? spezial;
        private readonly Rect koordinator;
        private readonly Rect bester;
        private readonly Rect? steuerpult;

        public Bild(DrawingContext context, Schaubild bild, Size groesse)
        {
            this.context = context;
            this.bild = bild;
            Groesse = groesse;
            jetzt = Stopwatch.GetTimestamp();
            double w = groesse.Width;
            double h = groesse.Height;
            double mitte = bild.Automodus ? h * 0.6 : h / 2;
            koordinator = Kasten(w * 0.70, mitte, w * 0.20, KastenHoehe);
            bester = Kasten(w * 0.905, mitte, w * 0.17, KastenHoehe + 8);
            steuerpult = bild.Automodus ? Kasten(w * 0.70, Rand + 20, w * 0.20, 36) : null;
            (inseln, spezial) = Inselkaesten(w, h, mitte);
        }

        private Size Groesse { get; }

        public static IBrush Farbe(string name) =>
            global::Avalonia.Application.Current?.TryFindResource(name, out object? wert) == true && wert is IBrush b ? b : Brushes.Gray;

        public static FormattedText Text(string text, IBrush farbe, double groesse, bool fett, double breite)
        {
            var formatiert = new FormattedText(text, Deutsch, FlowDirection.LeftToRight, new Typeface(Schriftart(), FontStyle.Normal, fett ? FontWeight.SemiBold : FontWeight.Normal), groesse, farbe);
            if (breite > 0)
            {
                formatiert.MaxTextWidth = breite;
                formatiert.Trimming = TextTrimming.CharacterEllipsis;
                formatiert.MaxLineCount = 1;
            }

            return formatiert;
        }

        /// <summary>Gibt an, ob gerade steht, was die letzte Verbesserung geändert hat (der Takt muss es ausblenden).</summary>
        public static bool ZeigtAenderungen(Schaubild bild) =>
            bild.Meldungen.Any(m => Stopwatch.GetElapsedTime(m.Zeitpunkt).TotalSeconds < Wanderzeit + Aenderungsdauer + 0.2);

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

            Kastenzeichnen(koordinator, "Koordinator", "gleicht ab, startet neu", null);
            if (steuerpult is Rect pult)
            {
                Kastenzeichnen(pult, "Automodus", "Gewichte · bester Plan", Leuchten(Schaubildereignis.Anhebung, 0, 1.5) is double l and > 0 ? (Farbe("Akzent"), l) : null);
            }

            // Der beste Plan leuchtet auf, wenn der grüne Punkt angekommen ist.
            double angekommen = Leuchten(Schaubildereignis.BesterPlan, Wanderzeit, 1.2);
            Kastenzeichnen(bester, "Bester Plan", bild.Bester, angekommen > 0 ? (Farbe("Gut"), angekommen) : null, gross: true);
            Aenderungen();
            Ereignisse();
        }

        private static FontFamily Schriftart() =>
            global::Avalonia.Application.Current?.TryFindResource("Grundschrift", out object? schrift) == true && schrift is FontFamily f ? f : FontFamily.Default;

        private static Rect Kasten(double mitteX, double mitteY, double breite, double hoehe) =>
            new(mitteX - (breite / 2), mitteY - (hoehe / 2), breite, hoehe);

        private static Point Rechts(Rect r) => new(r.Right, r.Center.Y);

        private static Point Links(Rect r) => new(r.Left, r.Center.Y);

        private static IBrush Mit(IBrush farbe, double deckkraft) =>
            farbe is ISolidColorBrush s ? new SolidColorBrush(s.Color, Math.Clamp(deckkraft, 0, 1)) : farbe;

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

        private (Rect[] Inseln, Rect? Spezial) Inselkaesten(double w, double h, double mitte)
        {
            int anzahl = Math.Max(1, bild.Inseln.Count);
            int zeilen = anzahl + (bild.Spezial ? 1 : 0);
            double oben = bild.Automodus ? Rand + 44 : Rand;
            double platz = h - oben - Rand;
            double hoehe = Math.Min(Zeilenhoehe, (platz - (Zeilenabstand * (zeilen - 1))) / zeilen);
            double gesamt = (zeilen * hoehe) + (Zeilenabstand * (zeilen - 1));
            double y = Math.Max(oben, mitte - (gesamt / 2));
            var kaesten = new Rect[anzahl];
            for (int i = 0; i < anzahl; i++)
            {
                kaesten[i] = new Rect(Rand, y, w * 0.5, hoehe);
                y += hoehe + Zeilenabstand;
            }

            Rect? sonder = bild.Spezial ? new Rect(Rand, y, w * 0.5, hoehe) : null;
            return (kaesten, sonder);
        }

        private void Linien()
        {
            var stift = new Pen(Farbe("Rand"), 1.5);
            foreach (Rect insel in inseln)
            {
                context.DrawLine(stift, Rechts(insel), Links(koordinator));
            }

            if (spezial is Rect s)
            {
                context.DrawLine(new Pen(Farbe("Rand"), 1.5, new DashStyle([3, 3], 0)), Rechts(s), Links(koordinator));
            }

            context.DrawLine(stift, Rechts(koordinator), Links(bester));
            if (steuerpult is Rect pult)
            {
                // Der Automodus hebt Gewichte an (zum Koordinator) und wählt den besten Plan nach A, B und C aus.
                context.DrawLine(stift, new Point(pult.Center.X, pult.Bottom), new Point(koordinator.Center.X, koordinator.Top));
                context.DrawLine(stift, Rechts(pult), new Point(bester.Center.X, pult.Center.Y));
                context.DrawLine(stift, new Point(bester.Center.X, pult.Center.Y), new Point(bester.Center.X, bester.Top));
            }
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
            context.DrawRectangle(Farbe("Flaeche"), new Pen(beste ? Farbe("Gut") : Farbe("Linie"), beste ? 2 : 1), kasten, 8, 8);
            double breite = kasten.Width - 12;
            FormattedText titel = Text("Insel " + (i + 1).ToString(Deutsch), Farbe("Tinte"), 11, true, breite / 2);
            FormattedText wert = Text(zustand?.Wert ?? string.Empty, Farbe("Tinte"), 10, false, breite / 2);
            context.DrawText(titel, new Point(kasten.Left + 6, kasten.Top + 3));
            context.DrawText(wert, new Point(kasten.Right - 6 - wert.Width, kasten.Top + 4));
            if (zustand is not null && kasten.Height >= 30)
            {
                double balkenBreite = breite * 0.3;
                IBrush tintenfarbe = zustand.Taetigkeit == "kommt voran" ? Farbe("Gut") : Farbe("Grau");
                FormattedText taetigkeit = Text(zustand.Taetigkeit, tintenfarbe, 10, false, breite - balkenBreite - 8);
                double y = kasten.Top + 4 + titel.Height;
                context.DrawText(taetigkeit, new Point(kasten.Left + 6, y));
                var spur = new Rect(kasten.Right - 6 - balkenBreite, y + (taetigkeit.Height / 2) - 2, balkenBreite, 4);
                context.DrawRectangle(Farbe("Linie"), null, spur, 2, 2);
                context.DrawRectangle(Farbe("Grau"), null, spur.WithWidth(balkenBreite * zustand.Stillstand), 2, 2);
            }

            double neu = Leuchten(Schaubildereignis.Neustart, 0, 1.4, i);
            if (neu > 0)
            {
                context.DrawRectangle(Mit(Farbe("Grau"), 0.3 * neu), null, kasten, 8, 8);
            }
        }

        /// <summary>
        /// Direkt unter dem besten Plan ein Kasten mit dem, was die letzte Verbesserung geändert hat: grün, was besser, rot,
        /// was schlechter wurde. Er erscheint, wenn der grüne Punkt beim besten Plan ankommt, wird mit jedem weiteren
        /// angekommenen Punkt aktualisiert (mit einem kurzen Aufleuchten) und blendet 10 Sekunden nach der letzten Ankunft aus
        /// (in der letzten Sekunde verblassend). Jeder Punkt trägt seine eigene Änderung; solange ein neuer Punkt wandert, bleibt
        /// die des zuletzt angekommenen stehen.
        /// </summary>
        private void Aenderungen()
        {
            // Die Meldung des zuletzt angekommenen Punkts; Punkte, die noch wandern, zählen noch nicht.
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
            double rechts = bester.Right;
            double breite = Math.Min(Math.Max(bester.Width, Groesse.Width * 0.34), rechts - koordinator.Left);
            const double Innen = 6;
            FormattedText kopf = Text(meldung.Titel, Mit(Farbe("Gut"), deckkraft), 11, true, breite - (2 * Innen));
            var zeilen = new List<FormattedText>();
            double hoehe = (2 * Innen) + kopf.Height + 2;
            double platz = Groesse.Height - Rand - (bester.Bottom + 4);
            foreach (Planaenderung aenderung in liste)
            {
                FormattedText zeile = Text(aenderung.Text, Mit(Farbe(aenderung.Besser ? "Gut" : "Schlecht"), deckkraft), 11, true, breite - (2 * Innen));
                if (hoehe + zeile.Height > platz)
                {
                    break;
                }

                zeilen.Add(zeile);
                hoehe += zeile.Height;
            }

            var kasten = new Rect(rechts - breite, bester.Bottom + 4, breite, hoehe);
            context.DrawRectangle(Mit(Farbe("Flaeche"), deckkraft), new Pen(Mit(Farbe("Gut"), deckkraft), 1.5 + (1.5 * aufleuchten)), kasten, 8, 8);
            context.DrawRectangle(Mit(Farbe("Gut"), (0.08 + (0.2 * aufleuchten)) * deckkraft), null, kasten, 8, 8);
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

        /// <summary>Punkte auf den Linien: der bessere beste Plan (grün), die Übergabe der Spezial-Insel, die Anhebung.</summary>
        private void Ereignisse()
        {
            foreach (Schaubildimpuls impuls in bild.Impulse)
            {
                double alter = Stopwatch.GetElapsedTime(impuls.Zeitpunkt, jetzt).TotalSeconds;
                Point[]? weg = Weg(impuls);
                double dauer = impuls.Art switch
                {
                    Schaubildereignis.BesterPlan => Wanderzeit,
                    Schaubildereignis.Uebergabe => 1.4,
                    _ => 0.9,
                };
                if (weg is null || alter > dauer)
                {
                    continue;
                }

                IBrush farbe = impuls.Art == Schaubildereignis.BesterPlan ? Farbe("Gut") : Farbe("Akzent");
                Point ort = Auf(weg, Sanft(alter / dauer));
                context.DrawEllipse(Mit(farbe, 0.2), null, ort, 8, 8);
                context.DrawEllipse(farbe, null, ort, 4, 4);
            }
        }

        private Point[]? Weg(Schaubildimpuls impuls)
        {
            bool inselGueltig = impuls.Insel >= 0 && impuls.Insel < inseln.Length;
            return impuls.Art switch
            {
                Schaubildereignis.BesterPlan when inselGueltig =>
                    [Rechts(inseln[impuls.Insel]), Links(koordinator), Rechts(koordinator), Links(bester)],
                Schaubildereignis.BesterPlan => [Links(koordinator), Rechts(koordinator), Links(bester)],
                Schaubildereignis.Uebergabe when inselGueltig && spezial is Rect s =>
                    [Rechts(s), Links(koordinator), new Point(koordinator.Left - 12, inseln[impuls.Insel].Center.Y), Rechts(inseln[impuls.Insel])],
                Schaubildereignis.Anhebung when steuerpult is Rect pult => [new Point(pult.Center.X, pult.Bottom), new Point(koordinator.Center.X, koordinator.Top)],
                _ => null,
            };
        }

        private void Kastenzeichnen(Rect kasten, string titel, string zeile, (IBrush Farbe, double Staerke)? glanz, bool gestrichelt = false, bool gross = false)
        {
            var stift = new Pen(Farbe("Linie"), 1, gestrichelt ? new DashStyle([3, 3], 0) : null);
            context.DrawRectangle(Farbe("Flaeche"), stift, kasten, 8, 8);
            if (glanz is ({ } farbe, double staerke))
            {
                context.DrawRectangle(Mit(farbe, 0.12 * staerke), new Pen(Mit(farbe, staerke), 2), kasten, 8, 8);
            }

            double breite = kasten.Width - 12;
            FormattedText oben = Text(titel, Farbe("Tinte"), 11, true, breite);
            FormattedText unten = Text(zeile, gross ? Farbe("Tinte") : Farbe("Grau"), gross ? 13 : 10, gross, breite);
            double hoehe = oben.Height + unten.Height;
            double y = kasten.Center.Y - (hoehe / 2);
            context.DrawText(oben, new Point(kasten.Left + 6, y));
            context.DrawText(unten, new Point(kasten.Left + 6, y + oben.Height));
        }
    }
}
