// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AndiGenerator.UI;

/// <summary>
/// Dialog zum Ändern einer einzelnen Gewichtung (Original <c>TDialogForSingleGewichtungsOptions</c>): Auswahl der Stufe,
/// „Standard wiederherstellen“ und – bei einer Zelle der Kostentabelle – die Meldungen dieser Mannschaft zur Kostenart.
/// </summary>
internal sealed class Gewichtungsdialog : Window
{
    private readonly ComboBox auswahl;
    private readonly Button standard;

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="titel">Fenstertitel.</param>
    /// <param name="beschriftung">Was gewichtet wird.</param>
    /// <param name="aktuell">Aktuelle Gewichtung.</param>
    /// <param name="meldungen">Meldungen; leer = ohne Meldungsbereich.</param>
    public Gewichtungsdialog(string titel, string beschriftung, Gewichtung aktuell, IReadOnlyList<string> meldungen)
    {
        Title = titel;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MinWidth = 460;
        MaxWidth = 760;
        Classes.Add("dialog");

        auswahl = new ComboBox
        {
            ItemsSource = Gewichtungsnamen.Stufen.Select(Gewichtungsnamen.Name).ToList(),
            SelectedIndex = (int)aktuell,
            MinWidth = 200,
        };
        standard = new Button { Content = "Standard wiederherstellen" };
        standard.Classes.Add("gross");
        standard.Click += (_, _) => auswahl.SelectedIndex = (int)Gewichtung.Normal;
        auswahl.SelectionChanged += (_, _) => StandardAktualisieren();
        StandardAktualisieren();

        var zeile = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        zeile.Children.Add(new TextBlock { Text = beschriftung, VerticalAlignment = VerticalAlignment.Center, MinWidth = 180, TextWrapping = TextWrapping.Wrap });
        zeile.Children.Add(auswahl);

        var inhalt = new StackPanel { Margin = new Thickness(24), Spacing = 14 };
        inhalt.Children.Add(zeile);
        if (meldungen.Count > 0)
        {
            inhalt.Children.Add(new TextBlock { Text = "Meldungen", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
            inhalt.Children.Add(new TextBox
            {
                Text = string.Join(Environment.NewLine, meldungen),
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 260,
                MinHeight = 80,
            });
        }

        var ok = new Button { Content = "OK", IsDefault = true };
        ok.Classes.AddRange(["gross", "primaer"]);
        ok.Click += (_, _) => Close((Gewichtung?)(Gewichtung)auswahl.SelectedIndex);
        var abbrechen = new Button { Content = "Abbrechen", IsCancel = true };
        abbrechen.Classes.Add("gross");
        abbrechen.Click += (_, _) => Close(null);
        var knoepfe = new DockPanel();
        DockPanel.SetDock(standard, Avalonia.Controls.Dock.Left);
        knoepfe.Children.Add(standard);
        var rechts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(24, 0, 0, 0) };
        rechts.Children.Add(abbrechen);
        rechts.Children.Add(ok);
        knoepfe.Children.Add(rechts);
        var fuss = new Border { Child = knoepfe };
        fuss.Classes.Add("dialogfuss");
        inhalt.Children.Add(fuss);
        Content = inhalt;
    }

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(Window);

    private void StandardAktualisieren() => standard.IsEnabled = auswahl.SelectedIndex != (int)Gewichtung.Normal;
}
