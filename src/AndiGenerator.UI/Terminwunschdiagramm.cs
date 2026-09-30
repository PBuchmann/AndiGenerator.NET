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
/// Zeitachse der Terminwünsche wie im Original (<c>PaintSpielPlanMeldungen</c>), gezeichnet mit dem
/// <see cref="Terminwunschzeichner"/>, derselben Routine wie im Ausdruck.
/// </summary>
public sealed class Terminwunschdiagramm : Control
{
    /// <summary>Definiert die Eigenschaft <see cref="Uebersicht"/>.</summary>
    public static readonly StyledProperty<Terminwunschuebersicht?> UebersichtProperty =
        AvaloniaProperty.Register<Terminwunschdiagramm, Terminwunschuebersicht?>(nameof(Uebersicht));

    static Terminwunschdiagramm()
    {
        AffectsRender<Terminwunschdiagramm>(UebersichtProperty);
        AffectsMeasure<Terminwunschdiagramm>(UebersichtProperty);
    }

    /// <summary>Holt oder setzt die darzustellenden Terminwünsche.</summary>
    public Terminwunschuebersicht? Uebersicht
    {
        get => GetValue(UebersichtProperty);
        set => SetValue(UebersichtProperty, value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Uebersicht is Terminwunschuebersicht u)
        {
            new Terminwunschzeichner(u, new AvaloniaZeichenflaeche(context), mitLegende: false).Zeichnen();
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Uebersicht is not Terminwunschuebersicht u)
        {
            return default;
        }

        Zeichengroesse groesse = new Terminwunschzeichner(u, null, mitLegende: false).Zeichnen();
        return new Size(groesse.Breite, groesse.Hoehe);
    }
}
