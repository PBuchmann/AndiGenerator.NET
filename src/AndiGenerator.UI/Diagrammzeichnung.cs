// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;
using AndiGenerator.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AndiGenerator.UI;

/// <summary>
/// Diagramme wie im Original (<c>PaintVerteilung</c>) untereinander; gezeichnet mit dem <see cref="Diagrammzeichner"/>,
/// derselben Routine wie im Ausdruck.
/// </summary>
public sealed class Diagrammzeichnung : Control
{
    /// <summary>Definiert die Eigenschaft <see cref="Daten"/>.</summary>
    public static readonly StyledProperty<Diagrammdaten?> DatenProperty =
        AvaloniaProperty.Register<Diagrammzeichnung, Diagrammdaten?>(nameof(Daten));

    /// <summary>Definiert die Eigenschaft <see cref="Teile"/>.</summary>
    public static readonly StyledProperty<IReadOnlyList<Diagrammteil>?> TeileProperty =
        AvaloniaProperty.Register<Diagrammzeichnung, IReadOnlyList<Diagrammteil>?>(nameof(Teile));

    static Diagrammzeichnung()
    {
        AffectsRender<Diagrammzeichnung>(DatenProperty, TeileProperty);
        AffectsMeasure<Diagrammzeichnung>(DatenProperty, TeileProperty);
    }

    /// <summary>Holt oder setzt die darzustellenden Diagrammdaten.</summary>
    public Diagrammdaten? Daten
    {
        get => GetValue(DatenProperty);
        set => SetValue(DatenProperty, value);
    }

    /// <summary>Holt oder setzt die zu zeichnenden Diagramme; <c>null</c> = alle wie im Original.</summary>
    public IReadOnlyList<Diagrammteil>? Teile
    {
        get => GetValue(TeileProperty);
        set => SetValue(TeileProperty, value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Daten is Diagrammdaten daten)
        {
            Zeichnen(new Diagrammzeichner(daten, new AvaloniaZeichenflaeche(context), mitTiteln: Teile is not { Count: 1 }));
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Daten is not Diagrammdaten daten)
        {
            return default;
        }

        Zeichengroesse groesse = Zeichnen(new Diagrammzeichner(daten, null, mitTiteln: Teile is not { Count: 1 }));
        return new Size(groesse.Breite, groesse.Hoehe);
    }

    /// <summary>Zeichnet die gewählten Diagramme untereinander (wie <see cref="Diagrammzeichner.Alles"/>) oder alle.</summary>
    private Zeichengroesse Zeichnen(Diagrammzeichner zeichner)
    {
        if (Teile is not { Count: > 0 } teile)
        {
            return zeichner.Alles();
        }

        double breite = 500;
        double y = -40;
        foreach (Diagrammteil teil in teile)
        {
            Zeichengroesse s = zeichner.Zeichnen(teil, y + 40);
            breite = Math.Max(breite, s.Breite);
            y = s.Hoehe;
        }

        return new Zeichengroesse(breite + 100, y);
    }
}
