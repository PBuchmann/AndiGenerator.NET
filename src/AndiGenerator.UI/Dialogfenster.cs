// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AndiGenerator.UI;

/// <summary>Einfacher modaler Dialog für Meldungen, Ja/Nein-Fragen und Texteingaben.</summary>
internal sealed class Dialogfenster : Window
{
    private readonly TextBox? eingabefeld;

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="titel">Fenstertitel.</param>
    /// <param name="text">Meldung, Frage oder Beschriftung.</param>
    /// <param name="eingabe">Vorbelegung des Eingabefelds; <c>null</c> = ohne Eingabefeld.</param>
    /// <param name="mitAbbrechen">Mit „Abbrechen“/„Nein“-Schaltfläche.</param>
    public Dialogfenster(string titel, string text, string? eingabe, bool mitAbbrechen)
        : this(titel, text, eingabe, [], JaText(mitAbbrechen, eingabe), mitAbbrechen ? NeinText(eingabe) : null)
    {
    }

    /// <summary>Initialisiert eine Entscheidungsfrage mit Detailtext und eigenen Schaltflächen.</summary>
    /// <param name="titel">Fenstertitel.</param>
    /// <param name="text">Einleitender Text.</param>
    /// <param name="details">Zeilen für das Textfeld darunter.</param>
    /// <param name="ja">Beschriftung der Zustimmung.</param>
    /// <param name="nein">Beschriftung der Ablehnung.</param>
    public Dialogfenster(string titel, string text, IReadOnlyList<string> details, string ja, string nein)
        : this(titel, text, null, details, ja, nein)
    {
    }

    private Dialogfenster(string titel, string text, string? eingabe, IReadOnlyList<string> details, string ja, string? nein)
    {
        Title = titel;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MinWidth = 420;
        MaxWidth = 760;
        Classes.Add("dialog");

        var inhalt = new StackPanel { Margin = new Thickness(24), Spacing = 14 };
        var textblock = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 23 };
        if (eingabe is null && details.Count == 0 && nein is not null)
        {
            textblock.Classes.Add("frage");
        }

        inhalt.Children.Add(textblock);
        if (details.Count > 0)
        {
            inhalt.Children.Add(new TextBox
            {
                Text = string.Join(Environment.NewLine, details),
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 360,
                MinWidth = 520,
            });
        }

        if (eingabe is not null)
        {
            eingabefeld = new TextBox { Text = eingabe };
            inhalt.Children.Add(eingabefeld);
        }

        // Wie in allen Dialogen: „Abbrechen“/„Nein“ links daneben, die Bestätigung ganz rechts.
        var knoepfe = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        if (nein is not null)
        {
            var abbrechen = new Button { Content = nein, IsCancel = true };
            abbrechen.Classes.Add("gross");
            abbrechen.Click += (_, _) => Close(false);
            knoepfe.Children.Add(abbrechen);
        }

        var ok = new Button { Content = ja, IsDefault = true, IsCancel = nein is null };
        ok.Classes.AddRange(["gross", "primaer"]);
        ok.Click += (_, _) => Close(true);
        knoepfe.Children.Add(ok);
        var fuss = new Border { Child = knoepfe };
        fuss.Classes.Add("dialogfuss");
        inhalt.Children.Add(fuss);
        Content = inhalt;
        Opened += (_, _) => eingabefeld?.Focus();
    }

    /// <summary>Holt den eingegebenen Text.</summary>
    public string Eingabe => eingabefeld?.Text ?? string.Empty;

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(Window);

    private static string JaText(bool mitAbbrechen, string? eingabe) => mitAbbrechen && eingabe is null ? "Ja" : "OK";

    private static string NeinText(string? eingabe) => eingabe is null ? "Nein" : "Abbrechen";
}
