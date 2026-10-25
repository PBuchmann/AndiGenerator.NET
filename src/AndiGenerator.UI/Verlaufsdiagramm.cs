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
/// Verlauf der Suche: übereinander Spuren mit gemeinsamer Zeitachse – im Automodus je eine für die Verstöße A, B und C (jede
/// mit eigener Skala), bei der Kostenoptimierung eine für die Kosten. Der beste Plan steht als Treppe da (er verbessert sich
/// sprunghaft), jede Verbesserung als Punkt in der Farbe ihrer Herkunft (Inseln, HiGHS, Teilneuplanung); große Sprünge aus
/// HiGHS sind beschriftet. Die Grenze aus HiGHS ist gestrichelt: grün, wenn der Plan sie erreicht, sonst orange mit
/// schraffierter Fläche – so viel ist höchstens noch zu holen. Fähnchen markieren die Meilensteine, links stehen je Spur der
/// aktuelle Wert und der Weg seit dem Start, rechts die Verbesserungen je Herkunft, unten wer den Prozessor nutzt. Mit der
/// Maus zeigt ein Kästchen, was eine Verbesserung geändert hat.
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

    private const double Schrift = 11;

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

        if (punkte.Count < 2 || flaeche.Width < 360 || flaeche.Height < 200)
        {
            Text(context, "Der Verlauf erscheint, sobald gerechnet wird.", new Point(4, 4), Pinsel("Grau"), Schrift);
            return;
        }

        var zeichnung = new Verlaufszeichnung(context, Verlauf!, flaeche);
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

    private static void Text(DrawingContext context, string text, Point ort, IBrush farbe, double groesse)
    {
        context.DrawText(new FormattedText(text, Deutsch, FlowDirection.LeftToRight, new Typeface(Verlaufszeichnung.Schriftart("Grundschrift")), groesse, farbe), ort);
    }

    private static IBrush Pinsel(string name) =>
        global::Avalonia.Application.Current?.TryFindResource(name, out object? wert) == true && wert is IBrush b ? b : Brushes.Gray;
}
