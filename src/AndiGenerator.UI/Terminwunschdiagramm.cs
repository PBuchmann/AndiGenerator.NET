// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;
using AndiGenerator.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace AndiGenerator.UI;

/// <summary>
/// Die Terminwünsche als Kalender je Mannschaft, gezeichnet mit dem <see cref="Terminwunschzeichner"/>, derselben Routine
/// wie im Ausdruck. Über einer Woche zeigt sie einen Kasten mit den Einträgen der Woche.
/// </summary>
public sealed class Terminwunschdiagramm : Control
{
    /// <summary>Definiert die Eigenschaft <see cref="Uebersicht"/>.</summary>
    public static readonly StyledProperty<Terminwunschuebersicht?> UebersichtProperty =
        AvaloniaProperty.Register<Terminwunschdiagramm, Terminwunschuebersicht?>(nameof(Uebersicht));

    /// <summary>Die Stellen der letzten Zeichnung mit ihren Texten.</summary>
    private IReadOnlyList<Diagrammhinweis> hinweise = [];

    /// <summary>Die Nummer der Stelle, deren Hinweis gerade gezeigt wird; −1 = keiner.</summary>
    private int gezeigt = -1;

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
            var zeichner = new Terminwunschzeichner(u, new AvaloniaZeichenflaeche(context), mitLegende: false);
            zeichner.Zeichnen();
            hinweise = zeichner.Hinweise.ToList();
            if (gezeigt >= 0 && gezeigt < hinweise.Count)
            {
                Hinweiszeichner.Zeichnen(new AvaloniaZeichenflaeche(context), hinweise[gezeigt], Bounds.Width, Bounds.Height);
            }
        }
        else
        {
            hinweise = [];
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

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point p = e.GetPosition(this);
        int neu = -1;
        for (int i = 0; i < hinweise.Count && neu < 0; i++)
        {
            neu = hinweise[i].Trifft(p.X, p.Y) ? i : -1;
        }

        Hinweisen(neu);
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Hinweisen(-1);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UebersichtProperty)
        {
            gezeigt = -1;
        }
    }

    /// <summary>Zeigt den Hinweis einer Woche (der Kasten wird mit der Zeichnung gezeichnet) oder keinen.</summary>
    private void Hinweisen(int stelle)
    {
        if (stelle != gezeigt)
        {
            gezeigt = stelle;
            InvalidateVisual();
        }
    }
}
