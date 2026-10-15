// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AndiGenerator.UI;

/// <summary>
/// Verlauf der Prozessorlast im Leitstand: gestapelte Flächen über die letzten zwei Minuten – unten die Suche (blau), darauf
/// HiGHS (orange) und der Rest des Programms (grau). Senkrecht in Kernen: Je Kern eine dünne Linie, so ist zu sehen, wie
/// viele Kerne arbeiten.
/// </summary>
public sealed class Lastverlauf : Control
{
    /// <summary>Definiert die Eigenschaft <see cref="Punkte"/>.</summary>
    public static readonly StyledProperty<IReadOnlyList<Lastpunkt>?> PunkteProperty =
        AvaloniaProperty.Register<Lastverlauf, IReadOnlyList<Lastpunkt>?>(nameof(Punkte));

    /// <summary>Definiert die Eigenschaft <see cref="Kerne"/>.</summary>
    public static readonly StyledProperty<int> KerneProperty = AvaloniaProperty.Register<Lastverlauf, int>(nameof(Kerne));

    static Lastverlauf() => AffectsRender<Lastverlauf>(PunkteProperty, KerneProperty);

    /// <summary>Holt oder setzt die Messungen (älteste zuerst).</summary>
    public IReadOnlyList<Lastpunkt>? Punkte
    {
        get => GetValue(PunkteProperty);
        set => SetValue(PunkteProperty, value);
    }

    /// <summary>Holt oder setzt die Zahl der Kerne (eine Linie je Kern).</summary>
    public int Kerne
    {
        get => GetValue(KerneProperty);
        set => SetValue(KerneProperty, value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var flaeche = new Rect(Bounds.Size);
        if (flaeche.Width <= 0 || flaeche.Height <= 0)
        {
            return;
        }

        context.DrawRectangle(Pinsel.Farbe("Schwebe"), null, flaeche, 3, 3);
        IReadOnlyList<Lastpunkt> punkte = Punkte ?? [];
        if (punkte.Count >= 2)
        {
            long jetzt = Stopwatch.GetTimestamp();
            double dauer = Generierungsanzeige.Verlaufsdauer.TotalSeconds;
            double X(Lastpunkt p) => flaeche.Width * (1 - Math.Clamp(Stopwatch.GetElapsedTime(p.Zeitpunkt, jetzt).TotalSeconds / dauer, 0, 1));
            double Y(double anteil) => flaeche.Height * (1 - Math.Clamp(anteil, 0, 1));
            Schicht(p => 0, p => p.Suche, "Akzent");
            Schicht(p => p.Suche, p => p.Suche + p.Highs, "Highs");
            Schicht(p => p.Suche + p.Highs, p => p.Suche + p.Highs + p.Rest, "Grau");

            void Schicht(Func<Lastpunkt, double> unten, Func<Lastpunkt, double> oben, string farbe)
            {
                var geometrie = new StreamGeometry();
                using (StreamGeometryContext g = geometrie.Open())
                {
                    g.BeginFigure(new Point(X(punkte[0]), Y(oben(punkte[0]))), true);
                    foreach (Lastpunkt p in punkte.Skip(1))
                    {
                        g.LineTo(new Point(X(p), Y(oben(p))));
                    }

                    for (int i = punkte.Count - 1; i >= 0; i--)
                    {
                        g.LineTo(new Point(X(punkte[i]), Y(unten(punkte[i]))));
                    }

                    g.EndFigure(true);
                }

                context.DrawGeometry(Pinsel.Farbe(farbe), null, geometrie);
            }
        }

        // Je Kern eine dünne Linie (bei sehr vielen Kernen nur jede vierte).
        if (Kerne > 1)
        {
            int schritt = Kerne <= 32 ? 1 : 4;
            var stift = new Pen(Pinsel.Mit(Pinsel.Farbe("Flaeche"), 0.75), 1);
            for (int k = schritt; k < Kerne; k += schritt)
            {
                double y = Math.Round(flaeche.Height * (1 - ((double)k / Kerne))) + 0.5;
                context.DrawLine(stift, new Point(0, y), new Point(flaeche.Width, y));
            }
        }
    }
}
