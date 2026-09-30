// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace AndiGenerator.UI;

/// <summary>
/// Linien-Symbol aus einem Pfad im 24×24-Raster (Ressourcen <c>Symbol…</c> in <c>Stil.axaml</c>). Zeichnet in der
/// Schriftfarbe seiner Umgebung, passt sich also Knöpfen und Hervorhebungen von selbst an.
/// </summary>
public sealed class Symbol : Control
{
    /// <summary>Definiert die Eigenschaft <see cref="Daten"/>.</summary>
    public static readonly StyledProperty<Geometry?> DatenProperty = AvaloniaProperty.Register<Symbol, Geometry?>(nameof(Daten));

    /// <summary>Definiert die Eigenschaft <see cref="Gefuellt"/>.</summary>
    public static readonly StyledProperty<bool> GefuelltProperty = AvaloniaProperty.Register<Symbol, bool>(nameof(Gefuellt));

    /// <summary>Definiert die Eigenschaft <see cref="Groesse"/>.</summary>
    public static readonly StyledProperty<double> GroesseProperty = AvaloniaProperty.Register<Symbol, double>(nameof(Groesse), 22);

    /// <summary>Definiert die Eigenschaft <see cref="Farbe"/>.</summary>
    public static readonly StyledProperty<IBrush?> FarbeProperty = AvaloniaProperty.Register<Symbol, IBrush?>(nameof(Farbe));

    static Symbol()
    {
        AffectsRender<Symbol>(DatenProperty, GefuelltProperty, FarbeProperty, TextElement.ForegroundProperty);
        AffectsMeasure<Symbol>(GroesseProperty);
    }

    /// <summary>Holt oder setzt den Pfad im 24×24-Raster.</summary>
    public Geometry? Daten
    {
        get => GetValue(DatenProperty);
        set => SetValue(DatenProperty, value);
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob der Pfad gefüllt statt als Linie gezeichnet wird.</summary>
    public bool Gefuellt
    {
        get => GetValue(GefuelltProperty);
        set => SetValue(GefuelltProperty, value);
    }

    /// <summary>Holt oder setzt die Kantenlänge in Pixeln.</summary>
    public double Groesse
    {
        get => GetValue(GroesseProperty);
        set => SetValue(GroesseProperty, value);
    }

    /// <summary>Holt oder setzt eine feste Farbe; ohne sie gilt die Schriftfarbe der Umgebung.</summary>
    public IBrush? Farbe
    {
        get => GetValue(FarbeProperty);
        set => SetValue(FarbeProperty, value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Daten is not Geometry pfad)
        {
            return;
        }

        IBrush farbe = Farbe ?? GetValue(TextElement.ForegroundProperty) ?? Brushes.Black;
        double faktor = Groesse / 24;
        using (context.PushTransform(Matrix.CreateScale(faktor, faktor)))
        {
            if (Gefuellt)
            {
                context.DrawGeometry(farbe, null, pfad);
            }
            else
            {
                context.DrawGeometry(null, new Pen(farbe, 1.8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), pfad);
            }
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) => new(Groesse, Groesse);
}
