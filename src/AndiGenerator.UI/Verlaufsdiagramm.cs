// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace AndiGenerator.UI;

/// <summary>
/// Verlauf der Suche im Maschinenraum: übereinander kleine Diagramme mit gemeinsamer Zeitachse – bei der
/// Kostenoptimierung die Kosten des besten Plans, im Automodus je eines für die Verstöße A, B und C (jedes mit eigener
/// Skala, keine zweite y-Achse) –, darunter die Pläne je Sekunde. Im Automodus zeigen Bänder die Phasen und kleine Striche,
/// wann Gewichte angehoben wurden. Mit der Maus zeigt ein Fadenkreuz die Werte zu einem Zeitpunkt.
/// </summary>
public sealed class Verlaufsdiagramm : Control
{
    /// <summary>Definiert die Eigenschaft <see cref="Verlauf"/>.</summary>
    public static readonly StyledProperty<Suchverlauf?> VerlaufProperty =
        AvaloniaProperty.Register<Verlaufsdiagramm, Suchverlauf?>(nameof(Verlauf));

    /// <summary>Definiert die Eigenschaft <see cref="Kompakt"/>.</summary>
    public static readonly StyledProperty<bool> KompaktProperty =
        AvaloniaProperty.Register<Verlaufsdiagramm, bool>(nameof(Kompakt));

    /// <summary>Definiert die Eigenschaft <see cref="Version"/>.</summary>
    public static readonly StyledProperty<int> VersionProperty =
        AvaloniaProperty.Register<Verlaufsdiagramm, int>(nameof(Version));

    private const double Links = 4;
    private const double Rechts = 64;
    private const double Oben = 18;
    private const double Unten = 18;
    private const double Abstand = 12;
    private const double HoeheTempo = 0.55;
    private const double Schrift = 11;
    private const double SchriftKlein = 10;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private double? zeiger;

    static Verlaufsdiagramm()
    {
        AffectsRender<Verlaufsdiagramm>(VerlaufProperty, VersionProperty, KompaktProperty);
    }

    /// <summary>Holt oder setzt den Verlauf.</summary>
    public Suchverlauf? Verlauf
    {
        get => GetValue(VerlaufProperty);
        set => SetValue(VerlaufProperty, value);
    }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob nur eine kleine Linie ohne Beschriftung gezeichnet wird (Kachel): die
    /// Kosten bzw. im Automodus die Verstöße der wichtigsten Stufe, die noch welche hat.
    /// </summary>
    public bool Kompakt
    {
        get => GetValue(KompaktProperty);
        set => SetValue(KompaktProperty, value);
    }

    /// <summary>Holt oder setzt die Version des Verlaufs (ändert sich mit jedem Punkt; löst das Neuzeichnen aus).</summary>
    public int Version
    {
        get => GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var flaeche = new Rect(Bounds.Size);
        IReadOnlyList<Verlaufspunkt> punkte = Verlauf?.Punkte ?? [];
        if (Kompakt)
        {
            KompaktZeichnen(context, punkte, flaeche);
            return;
        }

        if (punkte.Count < 2 || flaeche.Width < Links + Rechts + 40)
        {
            Text(context, "Der Verlauf erscheint, sobald gerechnet wird.", new Point(Links, Oben), Pinsel("Grau"), Schrift);
            return;
        }

        var zeichnung = new Zeichnung(context, punkte, flaeche);
        zeichnung.Zeichnen(zeiger);
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Kompakt)
        {
            return;
        }

        zeiger = e.GetPosition(this).X;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        zeiger = null;
        InvalidateVisual();
    }

    /// <summary>Kleine Linie ohne Beschriftung über die ganze Fläche (Kachel „Verbesserungen“).</summary>
    private static void KompaktZeichnen(DrawingContext context, IReadOnlyList<Verlaufspunkt> punkte, Rect flaeche)
    {
        Func<Verlaufspunkt, double?> wert = KompaktWert(punkte);
        (double Sekunden, double Wert)[] werte = punkte
            .Select(p => (p.Sekunden, Wert: wert(p)))
            .Where(w => w.Wert is not null)
            .Select(w => (w.Sekunden, w.Wert!.Value))
            .ToArray();
        if (werte.Length < 2)
        {
            return;
        }

        double anfang = werte[0].Sekunden;
        double dauer = Math.Max(1e-9, werte[^1].Sekunden - anfang);
        double min = werte.Min(w => w.Wert);
        double max = Math.Max(werte.Max(w => w.Wert), min + 1);
        Rect innen = flaeche.Deflate(2);
        var geometrie = new StreamGeometry();
        using (StreamGeometryContext g = geometrie.Open())
        {
            for (int i = 0; i < werte.Length; i++)
            {
                var punkt = new Point(
                    innen.Left + (innen.Width * (werte[i].Sekunden - anfang) / dauer),
                    innen.Bottom - (innen.Height * (werte[i].Wert - min) / (max - min)));
                if (i == 0)
                {
                    g.BeginFigure(punkt, false);
                }
                else
                {
                    g.LineTo(punkt);
                }
            }

            g.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(Pinsel("Akzent"), 1.5, lineJoin: PenLineJoin.Round), geometrie);
    }

    /// <summary>Was die kleine Linie zeigt: Kosten bzw. Verstöße der wichtigsten Stufe, die am Ende noch welche hat.</summary>
    private static Func<Verlaufspunkt, double?> KompaktWert(IReadOnlyList<Verlaufspunkt> punkte)
    {
        Verlaufspunkt? letzter = punkte.LastOrDefault(p => p.A is not null);
        if (letzter is null)
        {
            return p => p.Kosten is >= 0 and < 1e10 ? p.Kosten : null;
        }

        if (letzter.A > 0)
        {
            return p => p.A;
        }

        if (letzter.B > 0)
        {
            return p => p.B;
        }

        return p => p.C;
    }

    private static void Text(DrawingContext context, string text, Point ort, IBrush farbe, double groesse, bool fett = false)
    {
        context.DrawText(Formatiert(text, farbe, groesse, fett), ort);
    }

    private static FormattedText Formatiert(string text, IBrush farbe, double groesse, bool fett = false) =>
        new(text, Deutsch, FlowDirection.LeftToRight, new Typeface(Schriftart(), FontStyle.Normal, fett ? FontWeight.SemiBold : FontWeight.Normal), groesse, farbe);

    private static FontFamily Schriftart() =>
        global::Avalonia.Application.Current?.TryFindResource("Grundschrift", out object? schrift) == true && schrift is FontFamily f ? f : FontFamily.Default;

    private static IBrush Pinsel(string name) =>
        global::Avalonia.Application.Current?.TryFindResource(name, out object? wert) == true && wert is IBrush b ? b : Brushes.Gray;

    /// <summary>Ein Teildiagramm: Titel, Werte und Format.</summary>
    private sealed record Teil(string Titel, Func<Verlaufspunkt, double?> Wert, Func<double, string> Format, bool AbNull, double Gewicht);

    /// <summary>Zeichnet einmal den ganzen Verlauf.</summary>
    private sealed class Zeichnung
    {
        private readonly DrawingContext context;
        private readonly IReadOnlyList<Verlaufspunkt> punkte;
        private readonly Rect flaeche;
        private readonly double ende;
        private readonly bool automodus;
        private readonly IBrush akzent = Pinsel("Akzent");
        private readonly IBrush grau = Pinsel("Grau");
        private readonly IBrush tinte = Pinsel("Tinte");
        private readonly IBrush linie = Pinsel("Linie");
        private readonly IBrush band = Pinsel("Schwebe");
        private readonly IBrush grund = Pinsel("Flaeche");

        public Zeichnung(DrawingContext context, IReadOnlyList<Verlaufspunkt> punkte, Rect flaeche)
        {
            this.context = context;
            this.punkte = punkte;
            this.flaeche = flaeche;
            ende = Math.Max(1, punkte[^1].Sekunden);
            automodus = punkte.Any(p => p.A is not null);
        }

        private double Breite => flaeche.Width - Links - Rechts;

        public void Zeichnen(double? zeiger)
        {
            Teil[] teile = automodus
                ?
                [
                    new("Verstöße A", p => p.A, Ganz, true, 1),
                    new("Verstöße B", p => p.B, Ganz, true, 1),
                    new("Verstöße C", p => p.C, Ganz, true, 1),
                    new("Pläne/s", p => p.PlaeneProSekunde, Ganz, true, HoeheTempo),
                ]
                :
                [
                    new("Kosten des besten Plans", p => p.Kosten is >= 0 and < 1e10 ? p.Kosten : null, Kostenanzeige.Kurz, false, 2),
                    new("Pläne/s", p => p.PlaeneProSekunde, Ganz, true, HoeheTempo),
                ];
            Rect[] bereiche = Bereiche(teile);
            Rect alle = new(bereiche[0].TopLeft, bereiche[^2].BottomRight);
            if (automodus)
            {
                Phasen(alle);
                Anhebungen(bereiche[^2]);
            }

            for (int i = 0; i < teile.Length; i++)
            {
                TeilZeichnen(teile[i], bereiche[i]);
            }

            Text(context, "0:00", new Point(Links, flaeche.Bottom - Unten + 3), grau, SchriftKlein);
            FormattedText bis = Formatiert(Zeit(ende), grau, SchriftKlein);
            context.DrawText(bis, new Point(Links + Breite - bis.Width, flaeche.Bottom - Unten + 3));
            if (zeiger is double x && x >= Links && x <= Links + Breite)
            {
                Fadenkreuz(x, teile, new Rect(bereiche[0].TopLeft, bereiche[^1].BottomRight));
            }
        }

        private static string Ganz(double wert) => wert.ToString("N0", Deutsch);

        private static string Zeit(double sekunden)
        {
            var dauer = TimeSpan.FromSeconds(sekunden);
            return dauer.TotalHours >= 1 ? dauer.ToString(@"h\:mm\:ss", Deutsch) : dauer.ToString(@"m\:ss", Deutsch);
        }

        private static string Kurzphase(string phase) => phase switch
        {
            "Basisoptimierung" => "Basis",
            _ when phase.StartsWith("Optimierung ", StringComparison.Ordinal) => phase["Optimierung ".Length..],
            _ => phase,
        };

        private double X(double sekunden) => Links + (Breite * sekunden / ende);

        private Rect[] Bereiche(Teil[] teile)
        {
            double hoehe = flaeche.Height - Oben - Unten - (Abstand * (teile.Length - 1));
            double summe = teile.Sum(t => t.Gewicht);
            var bereiche = new Rect[teile.Length];
            double y = Oben;
            for (int i = 0; i < teile.Length; i++)
            {
                double h = hoehe * teile[i].Gewicht / summe;
                bereiche[i] = new Rect(Links, y, Breite, h);
                y += h + Abstand;
            }

            return bereiche;
        }

        /// <summary>Bänder der Phasen des Automodus hinter den Diagrammen der Verstöße, abwechselnd hinterlegt.</summary>
        private void Phasen(Rect alle)
        {
            int start = 0;
            bool hinterlegt = false;
            for (int i = 1; i <= punkte.Count; i++)
            {
                if (i < punkte.Count && punkte[i].Phase == punkte[start].Phase)
                {
                    continue;
                }

                double von = X(punkte[start].Sekunden);
                double bis = X(punkte[Math.Min(i, punkte.Count - 1)].Sekunden);
                if (hinterlegt)
                {
                    context.FillRectangle(band, new Rect(von, alle.Top - Oben + 2, Math.Max(0, bis - von), alle.Height + Oben - 2));
                }

                FormattedText name = Formatiert(Kurzphase(punkte[start].Phase), grau, SchriftKlein);
                if (bis - von > name.Width + 6)
                {
                    context.DrawText(name, new Point(von + 3, alle.Top - Oben + 3));
                }

                hinterlegt = !hinterlegt;
                start = i;
            }
        }

        /// <summary>Kleine Striche unter dem letzten Diagramm der Verstöße: hier hat der Automodus Gewichte angehoben.</summary>
        private void Anhebungen(Rect bereich)
        {
            var stift = new Pen(akzent, 1.5);
            for (int i = 1; i < punkte.Count; i++)
            {
                if (punkte[i].Anhebungen > punkte[i - 1].Anhebungen)
                {
                    double x = X(punkte[i].Sekunden);
                    context.DrawLine(stift, new Point(x, bereich.Bottom + 2), new Point(x, bereich.Bottom + 7));
                }
            }
        }

        private void TeilZeichnen(Teil teil, Rect bereich)
        {
            Text(context, teil.Titel, new Point(bereich.Left, bereich.Top - 1), grau, SchriftKlein);
            context.DrawLine(new Pen(linie, 1), bereich.BottomLeft, bereich.BottomRight);
            var werte = punkte.Select(p => (p.Sekunden, Wert: teil.Wert(p))).Where(w => w.Wert is not null).Select(w => (w.Sekunden, Wert: w.Wert!.Value)).ToArray();
            if (werte.Length == 0)
            {
                return;
            }

            double min = teil.AbNull ? 0 : werte.Min(w => w.Wert);
            double max = Math.Max(werte.Max(w => w.Wert), min + 1);
            double oben = bereich.Top + 12;
            double Y(double wert) => bereich.Bottom - ((bereich.Bottom - oben) * (wert - min) / (max - min));

            var geometrie = new StreamGeometry();
            using (StreamGeometryContext g = geometrie.Open())
            {
                g.BeginFigure(new Point(X(werte[0].Sekunden), Y(werte[0].Wert)), false);
                foreach ((double sekunden, double wert) in werte.Skip(1))
                {
                    g.LineTo(new Point(X(sekunden), Y(wert)));
                }

                g.EndFigure(false);
            }

            context.DrawGeometry(null, new Pen(akzent, 2, lineJoin: PenLineJoin.Round), geometrie);
            (double letzteZeit, double letzter) = werte[^1];
            Text(context, teil.Format(letzter), new Point(X(letzteZeit) + 6, Y(letzter) - 8), tinte, Schrift, fett: true);
        }

        /// <summary>Senkrechte Linie an der Maus und die Werte des nächsten Punkts in einem Kästchen.</summary>
        private void Fadenkreuz(double x, Teil[] teile, Rect alle)
        {
            double sekunden = (x - Links) / Breite * ende;
            Verlaufspunkt punkt = punkte.MinBy(p => Math.Abs(p.Sekunden - sekunden))!;
            double px = X(punkt.Sekunden);
            context.DrawLine(new Pen(grau, 1), new Point(px, alle.Top), new Point(px, alle.Bottom));

            var zeilen = new List<string> { Zeit(punkt.Sekunden) + (punkt.Phase.Length > 0 ? " · " + Kurzphase(punkt.Phase) : string.Empty) };
            zeilen.AddRange(teile.Select(t => t.Wert(punkt) is double w ? t.Titel + ": " + t.Format(w) : null).OfType<string>());
            FormattedText[] texte = zeilen.Select((z, i) => Formatiert(z, i == 0 ? grau : tinte, Schrift, fett: i == 0)).ToArray();
            double breite = texte.Max(t => t.Width) + 16;
            double hoehe = texte.Sum(t => t.Height) + 10;
            double links = px + 10 + breite > flaeche.Right ? px - 10 - breite : px + 10;
            var kasten = new Rect(links, alle.Top + 4, breite, hoehe);
            context.DrawRectangle(grund, new Pen(linie, 1), kasten, 6, 6);
            double y = kasten.Top + 5;
            foreach (FormattedText text in texte)
            {
                context.DrawText(text, new Point(kasten.Left + 8, y));
                y += text.Height;
            }
        }
    }
}
