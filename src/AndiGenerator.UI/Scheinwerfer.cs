// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace AndiGenerator.UI;

/// <summary>
/// Scheinwerfer der Führung „Schritt für Schritt“: dunkelt das Fenster für einen Moment ab und lässt nur das gezeigte
/// Element hell, mit blauem Rahmen. Klicks gehen durch (die Oberfläche bleibt bedienbar).
/// </summary>
public sealed class Scheinwerfer : Control
{
    /// <summary>So lange bleibt der Scheinwerfer an.</summary>
    public static readonly TimeSpan Dauer = TimeSpan.FromSeconds(2.8);

    private const double Rand = 6;
    private const double Rundung = 12;

    private static readonly IBrush Schatten = new SolidColorBrush(Color.FromArgb(0x66, 0x14, 0x1A, 0x22));
    private static readonly IPen Rahmen = new Pen(new SolidColorBrush(Color.FromRgb(0x1D, 0x5F, 0xA8)), 3);

    private readonly DispatcherTimer uhr;
    private Rect? bereich;
    private bool an;

    /// <summary>Initialisiert den Scheinwerfer.</summary>
    public Scheinwerfer()
    {
        IsHitTestVisible = false;
        Opacity = 0;
        Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(250) }];
        uhr = new DispatcherTimer { Interval = Dauer };
        uhr.Tick += (_, _) => Ausschalten();
    }

    /// <summary>Holt den hell gelassenen Bereich oder <c>null</c>, wenn der Scheinwerfer aus ist.</summary>
    public Rect? Bereich => an ? bereich : null;

    /// <summary>Richtet den Scheinwerfer auf einen Bereich (in eigenen Koordinaten).</summary>
    /// <param name="ziel">Der Bereich des Elements.</param>
    public void Zeigen(Rect ziel)
    {
        bereich = ziel.Inflate(Rand);
        an = true;
        Opacity = 1;
        InvalidateVisual();
        uhr.Stop();
        uhr.Start();
    }

    /// <summary>Schaltet den Scheinwerfer aus.</summary>
    public void Ausschalten()
    {
        // Der Bereich bleibt gezeichnet, solange er ausblendet.
        uhr.Stop();
        an = false;
        Opacity = 0;
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (bereich is not Rect hell)
        {
            return;
        }

        var ganz = new RectangleGeometry(new Rect(Bounds.Size));
        var loch = new RectangleGeometry(hell, Rundung, Rundung);
        context.DrawGeometry(Schatten, null, new CombinedGeometry(GeometryCombineMode.Exclude, ganz, loch));
        context.DrawRectangle(null, Rahmen, hell, Rundung, Rundung);
    }
}
