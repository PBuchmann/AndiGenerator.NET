// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AndiGenerator.UI;

/// <summary>
/// Ein Kriterium im normalen Modus als Balken (Entwurf 2D): so lang wie die Verstöße des besten Plans; der Teil bis zur
/// bewiesenen Grenze schraffiert (unvermeidbar), der Rest in der Farbe der Stufe (noch zu holen); der Wert des gespeicherten
/// Plans als senkrechter Strich.
/// </summary>
public sealed class Grenzbalken : Control
{
    /// <summary>Definiert die Eigenschaft <see cref="Anzahl"/>.</summary>
    public static readonly StyledProperty<int> AnzahlProperty = AvaloniaProperty.Register<Grenzbalken, int>(nameof(Anzahl));

    /// <summary>Definiert die Eigenschaft <see cref="Grenze"/>.</summary>
    public static readonly StyledProperty<int> GrenzeProperty = AvaloniaProperty.Register<Grenzbalken, int>(nameof(Grenze));

    /// <summary>Definiert die Eigenschaft <see cref="Vorher"/>.</summary>
    public static readonly StyledProperty<int?> VorherProperty = AvaloniaProperty.Register<Grenzbalken, int?>(nameof(Vorher));

    /// <summary>Definiert die Eigenschaft <see cref="Maximum"/>.</summary>
    public static readonly StyledProperty<int> MaximumProperty = AvaloniaProperty.Register<Grenzbalken, int>(nameof(Maximum), 1);

    /// <summary>Definiert die Eigenschaft <see cref="Stufe"/>.</summary>
    public static readonly StyledProperty<Stufe> StufeProperty = AvaloniaProperty.Register<Grenzbalken, Stufe>(nameof(Stufe));

    private const double Rundung = 3;

    static Grenzbalken() => AffectsRender<Grenzbalken>(AnzahlProperty, GrenzeProperty, VorherProperty, MaximumProperty, StufeProperty);

    /// <summary>Holt oder setzt die Verstöße des besten Plans.</summary>
    public int Anzahl
    {
        get => GetValue(AnzahlProperty);
        set => SetValue(AnzahlProperty, value);
    }

    /// <summary>Holt oder setzt die bewiesene Untergrenze.</summary>
    public int Grenze
    {
        get => GetValue(GrenzeProperty);
        set => SetValue(GrenzeProperty, value);
    }

    /// <summary>Holt oder setzt die Verstöße des gespeicherten Plans (<c>null</c>: kein Strich).</summary>
    public int? Vorher
    {
        get => GetValue(VorherProperty);
        set => SetValue(VorherProperty, value);
    }

    /// <summary>Holt oder setzt den größten Wert der Gruppe (volle Breite).</summary>
    public int Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>Holt oder setzt die Stufe (Farbe des offenen Teils).</summary>
    public Stufe Stufe
    {
        get => GetValue(StufeProperty);
        set => SetValue(StufeProperty, value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        double breite = Bounds.Width;
        double hoehe = Bounds.Height;
        if (breite <= 4 || hoehe <= 0)
        {
            return;
        }

        const double Rand = 3;
        var leiste = new Rect(0, Rand, breite, hoehe - (2 * Rand));
        context.DrawRectangle(Pinsel.Farbe("Schwebe"), null, leiste, Rundung, Rundung);
        double max = Math.Max(1, Maximum);
        double gesamt = breite * Math.Clamp(Anzahl / max, 0, 1);
        double fest = breite * Math.Clamp(Math.Min(Grenze, Anzahl) / max, 0, 1);
        if (gesamt > 0)
        {
            var balken = new Rect(0, Rand, gesamt, leiste.Height);
            context.DrawRectangle(Stufenfarbe(Stufe), null, balken, Rundung, Rundung);
        }

        if (fest > 0)
        {
            // Unvermeidbar: grau schraffiert.
            var feld = new Rect(0, Rand, fest, leiste.Height);
            using (context.PushClip(new RoundedRect(feld, Rundung)))
            {
                context.DrawRectangle(new SolidColorBrush(Color.Parse("#C4C8CE")), null, feld);
                var strich = new Pen(new SolidColorBrush(Color.Parse("#8A919B")), 2.5);
                for (double x = -feld.Height; x < fest + feld.Height; x += 6)
                {
                    context.DrawLine(strich, new Point(x, feld.Bottom), new Point(x + feld.Height, feld.Top));
                }
            }
        }

        if (Vorher is int vorher)
        {
            double x = Math.Round(Math.Clamp(breite * vorher / max, 1, breite - 1)) + 0.5;
            context.DrawLine(new Pen(Pinsel.Farbe("Tinte"), 2), new Point(x, 0), new Point(x, hoehe));
        }
    }

    private static IBrush Stufenfarbe(Stufe stufe) => stufe switch
    {
        Stufe.A => Pinsel.Farbe("Schlecht"),
        Stufe.B => Pinsel.Farbe("Highs"),
        _ => Pinsel.Farbe("Akzent"),
    };
}
