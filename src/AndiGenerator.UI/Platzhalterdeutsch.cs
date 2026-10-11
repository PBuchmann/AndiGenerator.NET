// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;

namespace AndiGenerator.UI;

/// <summary>
/// Deutsche Platzhalter für Zeit- und Datumsauswahl: Avalonia schreibt in leere Felder fest „hour“/„minute“ bzw.
/// „day“/„month“/„year“ (im Code, nicht über die Sprache); diese Texte werden beim Setzen ersetzt.
/// </summary>
internal static class Platzhalterdeutsch
{
    private static readonly Dictionary<string, string> Uebersetzungen = new(StringComparer.Ordinal)
    {
        ["hour"] = "Stunde",
        ["minute"] = "Minute",
        ["second"] = "Sekunde",
        ["day"] = "Tag",
        ["month"] = "Monat",
        ["year"] = "Jahr",
    };

    /// <summary>Meldet die Ersetzung für alle Zeit- und Datumsauswahlen der Anwendung an.</summary>
    public static void Anmelden() => TextBlock.TextProperty.Changed.AddClassHandler<TextBlock>(Ersetzen);

    private static void Ersetzen(TextBlock feld, AvaloniaPropertyChangedEventArgs e)
    {
        if (feld.TemplatedParent is TimePicker or DatePicker
            && e.NewValue is string text
            && Uebersetzungen.TryGetValue(text, out string? deutsch))
        {
            feld.Text = deutsch;
        }
    }
}
