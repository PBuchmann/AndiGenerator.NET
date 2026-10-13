// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace AndiGenerator.UI;

/// <summary>
/// Text mit hervorgehobenen Stellen: Was in der <see cref="Auszeichnung"/> zwischen zwei „**“ steht, erscheint in der Farbe
/// <see cref="Hervorhebung"/> und halbfett, z. B. „Fahrten: **3.083 km**“.
/// </summary>
public sealed class Auszeichnungstext : TextBlock
{
    /// <summary>Definiert die Eigenschaft <see cref="Auszeichnung"/>.</summary>
    public static readonly StyledProperty<string?> AuszeichnungProperty = AvaloniaProperty.Register<Auszeichnungstext, string?>(nameof(Auszeichnung));

    /// <summary>Definiert die Eigenschaft <see cref="Hervorhebung"/>.</summary>
    public static readonly StyledProperty<IBrush?> HervorhebungProperty = AvaloniaProperty.Register<Auszeichnungstext, IBrush?>(nameof(Hervorhebung));

    private const string Marke = "**";

    /// <summary>Holt oder setzt den Text mit Auszeichnung („**“ um hervorgehobene Stellen).</summary>
    public string? Auszeichnung
    {
        get => GetValue(AuszeichnungProperty);
        set => SetValue(AuszeichnungProperty, value);
    }

    /// <summary>Holt oder setzt die Farbe der hervorgehobenen Stellen.</summary>
    public IBrush? Hervorhebung
    {
        get => GetValue(HervorhebungProperty);
        set => SetValue(HervorhebungProperty, value);
    }

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == AuszeichnungProperty || change.Property == HervorhebungProperty)
        {
            Aufbauen();
        }
    }

    private void Aufbauen()
    {
        var inlines = new InlineCollection();
        string[] teile = (Auszeichnung ?? string.Empty).Split(Marke);
        for (int i = 0; i < teile.Length; i++)
        {
            if (teile[i].Length == 0)
            {
                continue;
            }

            var lauf = new Run(teile[i]);
            if (i % 2 == 1)
            {
                lauf.FontWeight = FontWeight.SemiBold;
                if (Hervorhebung is IBrush farbe)
                {
                    lauf.Foreground = farbe;
                }
            }

            inlines.Add(lauf);
        }

        Inlines = inlines;
    }
}
