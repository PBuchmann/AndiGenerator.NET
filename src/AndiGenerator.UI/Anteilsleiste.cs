// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AndiGenerator.UI;

/// <summary>
/// Der ganze Prozessor im Leitstand als Leiste: von links die Anteile der Suche (blau), von HiGHS (orange) und des übrigen
/// Programms (grau), rechts frei. Feine Striche teilen die Leiste in die Kerne, so ist auch zu sehen, wie viel eines Kerns
/// HiGHS braucht.
/// </summary>
public sealed class Anteilsleiste : Control
{
    /// <summary>Definiert die Eigenschaft <see cref="Suche"/>.</summary>
    public static readonly StyledProperty<double> SucheProperty = AvaloniaProperty.Register<Anteilsleiste, double>(nameof(Suche));

    /// <summary>Definiert die Eigenschaft <see cref="Highs"/>.</summary>
    public static readonly StyledProperty<double> HighsProperty = AvaloniaProperty.Register<Anteilsleiste, double>(nameof(Highs));

    /// <summary>Definiert die Eigenschaft <see cref="Rest"/>.</summary>
    public static readonly StyledProperty<double> RestProperty = AvaloniaProperty.Register<Anteilsleiste, double>(nameof(Rest));

    /// <summary>Definiert die Eigenschaft <see cref="Frei"/>.</summary>
    public static readonly StyledProperty<double> FreiProperty = AvaloniaProperty.Register<Anteilsleiste, double>(nameof(Frei));

    /// <summary>Definiert die Eigenschaft <see cref="Kerne"/>.</summary>
    public static readonly StyledProperty<int> KerneProperty = AvaloniaProperty.Register<Anteilsleiste, int>(nameof(Kerne));

    private const double Rundung = 3;

    static Anteilsleiste() => AffectsRender<Anteilsleiste>(SucheProperty, HighsProperty, RestProperty, FreiProperty, KerneProperty);

    /// <summary>Holt oder setzt den Anteil der Suche (0 bis 1).</summary>
    public double Suche
    {
        get => GetValue(SucheProperty);
        set => SetValue(SucheProperty, value);
    }

    /// <summary>Holt oder setzt den Anteil von HiGHS (0 bis 1).</summary>
    public double Highs
    {
        get => GetValue(HighsProperty);
        set => SetValue(HighsProperty, value);
    }

    /// <summary>Holt oder setzt den Anteil des übrigen Programms (0 bis 1).</summary>
    public double Rest
    {
        get => GetValue(RestProperty);
        set => SetValue(RestProperty, value);
    }

    /// <summary>Holt oder setzt den freien Anteil (0 bis 1; nur für eine Zeile „frei“, sonst bleibt der Rest der Leiste leer).</summary>
    public double Frei
    {
        get => GetValue(FreiProperty);
        set => SetValue(FreiProperty, value);
    }

    /// <summary>Holt oder setzt die Zahl der Kerne (Striche in der Leiste).</summary>
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

        context.DrawRectangle(Pinsel.Farbe("Linie"), null, flaeche, Rundung, Rundung);
        double x = 0;
        foreach ((double anteil, string farbe) in new[] { (Suche, "Akzent"), (Highs, "Highs"), (Rest, "Grau"), (Frei, "Rand") })
        {
            double breite = flaeche.Width * Math.Clamp(anteil, 0, 1);
            breite = Math.Min(breite, flaeche.Width - x);
            if (breite > 0)
            {
                context.DrawRectangle(Pinsel.Farbe(farbe), null, new Rect(x, 0, breite, flaeche.Height));
                x += breite;
            }
        }

        // Je Kern eine dünne Linie: hell über den belegten Teilen, dunkler im freien Teil (bei sehr vielen Kernen jede vierte).
        if (Kerne > 1)
        {
            int schritt = Kerne <= 64 ? 1 : 4;
            var hell = new Pen(Pinsel.Farbe("Flaeche"), 1);
            var dunkel = new Pen(Pinsel.Farbe("Rand"), 1);
            for (int k = schritt; k < Kerne; k += schritt)
            {
                double kx = Math.Round(flaeche.Width * k / Kerne) + 0.5;
                context.DrawLine(kx < x ? hell : dunkel, new Point(kx, 0), new Point(kx, flaeche.Height));
            }
        }
    }
}
