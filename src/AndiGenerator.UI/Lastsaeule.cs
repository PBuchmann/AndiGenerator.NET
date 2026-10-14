// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AndiGenerator.UI;

/// <summary>
/// Säule eines Rechenkerns im Leitstand: Die Spur zeigt die volle Höhe (= so schnell wie der schnellste Kern, 100 %),
/// der Balken darin den Anteil. Die Säule füllt die verfügbare Höhe, die Skala passt also immer.
/// </summary>
public sealed class Lastsaeule : Control
{
    /// <summary>Definiert die Eigenschaft <see cref="Anteil"/>.</summary>
    public static readonly StyledProperty<double> AnteilProperty = AvaloniaProperty.Register<Lastsaeule, double>(nameof(Anteil));

    /// <summary>Definiert die Eigenschaft <see cref="Farbe"/>.</summary>
    public static readonly StyledProperty<IBrush?> FarbeProperty = AvaloniaProperty.Register<Lastsaeule, IBrush?>(nameof(Farbe));

    /// <summary>Definiert die Eigenschaft <see cref="Spur"/>.</summary>
    public static readonly StyledProperty<IBrush?> SpurProperty = AvaloniaProperty.Register<Lastsaeule, IBrush?>(nameof(Spur));

    /// <summary>Rundung der Ecken in Pixeln.</summary>
    private const double Rundung = 3;

    static Lastsaeule() => AffectsRender<Lastsaeule>(AnteilProperty, FarbeProperty, SpurProperty);

    /// <summary>Holt oder setzt den Anteil von 0 bis 1 (1 = so schnell wie der schnellste Kern).</summary>
    public double Anteil
    {
        get => GetValue(AnteilProperty);
        set => SetValue(AnteilProperty, value);
    }

    /// <summary>Holt oder setzt die Farbe des Balkens.</summary>
    public IBrush? Farbe
    {
        get => GetValue(FarbeProperty);
        set => SetValue(FarbeProperty, value);
    }

    /// <summary>Holt oder setzt die Farbe der Spur (volle Höhe = 100 %).</summary>
    public IBrush? Spur
    {
        get => GetValue(SpurProperty);
        set => SetValue(SpurProperty, value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        Rect flaeche = new(Bounds.Size);
        if (flaeche.Width <= 0 || flaeche.Height <= 0)
        {
            return;
        }

        if (Spur is IBrush spur)
        {
            context.DrawRectangle(spur, null, flaeche, Rundung, Rundung);
        }

        double hoehe = flaeche.Height * Math.Clamp(Anteil, 0, 1);
        if (hoehe > 0)
        {
            context.DrawRectangle(Farbe ?? Brushes.SteelBlue, null, new Rect(0, flaeche.Height - hoehe, flaeche.Width, hoehe), Rundung, Rundung);
        }
    }
}
