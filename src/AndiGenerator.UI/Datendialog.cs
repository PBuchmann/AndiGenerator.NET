// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.ComponentModel;
using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

namespace AndiGenerator.UI;

/// <summary>
/// Dialog „Spielplandaten bearbeiten“ (Original <c>TDialogForMultiplePanel</c>): Seitenliste links, Seite rechts,
/// unten Meldung, „Standard wiederherstellen“, OK und Abbrechen. Mit einer einzigen Seite dient er als
/// <c>TDialogForSinglePanel</c> (Erststart-Assistent).
/// </summary>
internal sealed class Datendialog : Window
{
    private readonly DatenDialogViewModel dialog;
    private readonly ListBox liste;
    private readonly ContentControl inhalt;
    private readonly TextBlock meldung;

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="dialog">ViewModel.</param>
    public Datendialog(DatenDialogViewModel dialog)
    {
        this.dialog = dialog;

        // Mit nur einer Seite wie das Original TDialogForSinglePanel: ohne Seitenliste, Titel der Seite.
        bool einzeln = dialog.Seiten.Count == 1;
        Title = einzeln ? dialog.Seiten[0].Titel : "Spielplandaten bearbeiten";
        Width = einzeln ? 900 : 1360;
        Height = 820;
        MinWidth = 700;
        MinHeight = 450;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Classes.Add("dialog");
        Opened += (_, _) => AnBildschirmAnpassen();

        liste = new ListBox
        {
            ItemsSource = dialog.Seiten,
            SelectedItem = dialog.Seite,
            ItemTemplate = new FuncDataTemplate<DatenSeiteViewModel>((s, _) => new TextBlock { Text = s?.Titel, TextWrapping = TextWrapping.Wrap }),
            Width = 230,
        };
        liste.Classes.Add("seitenliste");
        liste.SelectionChanged += (_, _) => SeiteWaehlen();
        inhalt = new ContentControl { Content = dialog.Seite, ContentTemplate = new SeitenLocator(), Margin = new Thickness(8, 8, 8, 0) };
        inhalt.Classes.Add("seite");
        meldung = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        meldung.Classes.Add("fehler");

        var standard = new Button { Content = "Standard wiederherstellen", Command = dialog.StandardCommand, IsVisible = dialog.StandardSichtbar };
        standard.Classes.Add("gross");
        var ok = new Button { Content = "OK", IsDefault = true };
        ok.Classes.AddRange(["gross", "primaer"]);
        ok.Click += (_, _) =>
        {
            if (dialog.Bestaetigen())
            {
                Close(true);
            }
        };
        var abbrechen = new Button { Content = "Abbrechen", IsCancel = true };
        abbrechen.Classes.Add("gross");
        abbrechen.Click += (_, _) => Close(false);

        var knoepfe = new DockPanel();
        DockPanel.SetDock(standard, Avalonia.Controls.Dock.Left);
        knoepfe.Children.Add(standard);
        var rechts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        rechts.Children.Add(abbrechen);
        rechts.Children.Add(ok);
        knoepfe.Children.Add(rechts);
        var fuss = new Border { Child = knoepfe };
        fuss.Classes.Add("dialogfuss");

        var unten = new StackPanel { Margin = new Thickness(16, 8, 16, 16) };
        unten.Children.Add(meldung);
        unten.Children.Add(fuss);

        var rahmen = new DockPanel();
        DockPanel.SetDock(unten, Avalonia.Controls.Dock.Bottom);
        rahmen.Children.Add(unten);
        var links = new Border { Child = liste, IsVisible = !einzeln };
        links.Classes.Add("seitenleiste");
        DockPanel.SetDock(links, Avalonia.Controls.Dock.Left);
        rahmen.Children.Add(links);
        rahmen.Children.Add(new ScrollViewer { Content = inhalt, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        Content = rahmen;

        dialog.PropertyChanged += Geaendert;
        MeldungAnzeigen();
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        dialog.PropertyChanged -= Geaendert;
        base.OnClosed(e);
    }

    /// <summary>Verkleinert den Dialog, falls er größer als der Arbeitsbereich des Bildschirms ist, und zentriert ihn neu.</summary>
    private void AnBildschirmAnpassen()
    {
        if (Screens.ScreenFromVisual(this) is not Screen bildschirm)
        {
            return;
        }

        PixelRect bereich = bildschirm.WorkingArea;
        double skalierung = bildschirm.Scaling;
        double breite = Math.Min(Width, (bereich.Width / skalierung) - 40);
        double hoehe = Math.Min(Height, (bereich.Height / skalierung) - 40);
        if (breite < Width || hoehe < Height)
        {
            Width = breite;
            Height = hoehe;
            Position = new PixelPoint(
                bereich.X + (int)((bereich.Width - (breite * skalierung)) / 2),
                bereich.Y + (int)((bereich.Height - (hoehe * skalierung)) / 2));
        }
    }

    private void SeiteWaehlen()
    {
        if (liste.SelectedItem is not DatenSeiteViewModel gewaehlt || ReferenceEquals(gewaehlt, dialog.Seite))
        {
            return;
        }

        dialog.Seite = gewaehlt;
        if (!ReferenceEquals(gewaehlt, dialog.Seite))
        {
            // Ungültige Eingaben: Auswahl zurücksetzen, wenn die Liste ihre Auswahländerung abgeschlossen hat.
            Dispatcher.UIThread.Post(() => liste.SelectedItem = dialog.Seite);
        }
    }

    private void Geaendert(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DatenDialogViewModel.Seite))
        {
            inhalt.Content = dialog.Seite;
        }

        MeldungAnzeigen();
    }

    private void MeldungAnzeigen()
    {
        meldung.Text = dialog.Meldung;
        meldung.IsVisible = dialog.HatMeldung;
    }
}
