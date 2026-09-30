// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.ComponentModel;
using System.Windows.Input;
using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace AndiGenerator.UI.Ansichten;

/// <summary>
/// Kostenansicht: Kennzahlen des Plans als Chips unter der Leiste, darunter die Kostenmatrix (Mannschaften × Kostenarten,
/// nach Kosten sortiert, Zellen nach ihrem Anteil an den Gesamtkosten eingefärbt, senkrechte Spaltenköpfe), daneben die
/// Verteilung der Kosten und rechts die Details zur Auswahl. Chips und Matrix haben je nach Plan unterschiedlich viele
/// Felder und werden deshalb hier im Code aufgebaut.
/// </summary>
public partial class KostenAnsicht : UserControl
{
    private const double Spaltenbreite = 60;
    private const double LeereSpaltenbreite = 34;
    private const double Zeilenhoehe = 28;
    private const double Kopfschrift = 12;
    private const double Kartenabstand = 12;

    private static readonly Cursor Hand = new(StandardCursorType.Hand);
    private static readonly IImmutableSolidColorBrush Minus = Farbe(0xB4, 0x23, 0x18);
    private static readonly IImmutableSolidColorBrush Plus = Farbe(0x1D, 0x6B, 0x3A);
    private static readonly IImmutableSolidColorBrush Akzent = Farbe(0x1D, 0x5F, 0xA8);
    private static readonly IImmutableSolidColorBrush AkzentHell = Farbe(0xE6, 0xEE, 0xF8);
    private static readonly IImmutableSolidColorBrush Tinte = Farbe(0x1B, 0x1F, 0x24);
    private static readonly IImmutableSolidColorBrush Grau = Farbe(0x5B, 0x64, 0x70);
    private static readonly IImmutableSolidColorBrush Tinte2Farbe = Farbe(0x4A, 0x53, 0x60);
    private static readonly IImmutableSolidColorBrush Blass = Farbe(0x9C, 0xA2, 0xAA);
    private static readonly IImmutableSolidColorBrush Linie = Farbe(0xE2, 0xE0, 0xDA);
    private static readonly IImmutableSolidColorBrush Schwebe = Farbe(0xF0, 0xEF, 0xEA);
    private static readonly IImmutableSolidColorBrush Leer = Farbe(0xFA, 0xFA, 0xF8);
    private static readonly IImmutableSolidColorBrush Balkenrot = Farbe(0xC9, 0x4A, 0x3C);
    private static readonly IImmutableSolidColorBrush[] Anteilsfarben =
    [
        Farbe(0xC9, 0x4A, 0x3C), Farbe(0xD9, 0x70, 0x5F), Farbe(0xE4, 0x94, 0x84), Farbe(0xED, 0xB5, 0xA8), Farbe(0xF3, 0xCF, 0xC6), Farbe(0xF8, 0xE3, 0xDD),
    ];

    private static readonly FontFamily Ziffern = FontFamily.Parse("avares://AndiGenerator.UI/Assets/Fonts#IBM Plex Mono, Consolas, monospace");

    private KostenAnsichtViewModel? modell;
    private bool angezeigt;

    /// <summary>Initialisiert die Ansicht.</summary>
    public KostenAnsicht()
    {
        InitializeComponent();
        Mausradzoom.Anmelden(this);
        LayoutUpdated += (_, _) => KartenAnordnen();
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Verbinden();
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        angezeigt = true;
        Verbinden();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        angezeigt = false;
        Verbinden();
    }

    private static ImmutableSolidColorBrush Farbe(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

    /// <summary>Einfärbung nach dem Anteil an den Gesamtkosten: von fast weiß (kaum Anteil) bis kräftig rot (ab 20 %).</summary>
    private static (IBrush Hintergrund, IBrush Schrift) Einfaerbung(double anteil)
    {
        if (anteil <= 0)
        {
            return (Leer, Blass);
        }

        double f = Math.Min(1, Math.Sqrt(anteil / 0.2));
        byte Mischen(byte von, byte bis) => (byte)Math.Round(von + ((bis - von) * f));
        var farbe = new ImmutableSolidColorBrush(Color.FromRgb(Mischen(0xFD, 0xC9), Mischen(0xF1, 0x4A), Mischen(0xEF, 0x3C)));
        return (farbe, f > 0.55 ? Brushes.White : Tinte);
    }

    private static IBrush Markenfarbe(string markierung) => markierung switch
    {
        "X" => Brushes.Black,
        _ when markierung.StartsWith('-') => Minus,
        _ => Plus,
    };

    private static Border Marke(string text)
    {
        var marke = new Border
        {
            Background = Akzent,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = text, FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, FontFamily = Ziffern },
        };
        return marke;
    }

    private static Border Balken(double anteil, double breite)
    {
        var innen = new Border { Background = Balkenrot, CornerRadius = new CornerRadius(2), Width = Math.Max(0, breite * anteil), HorizontalAlignment = HorizontalAlignment.Left };
        return new Border { Background = Schwebe, CornerRadius = new CornerRadius(2), Width = breite, Height = 4, Child = innen, VerticalAlignment = VerticalAlignment.Center };
    }

    /// <summary>Breite eines Kopftexts (für die Höhe der senkrechten Spaltenköpfe).</summary>
    private static double Textbreite(string text, FontWeight gewicht)
    {
        var probe = new TextBlock { Text = text, FontSize = Kopfschrift, FontWeight = gewicht };
        probe.Measure(Size.Infinity);
        return probe.DesiredSize.Width;
    }

    private static TextBlock Spaltenkopf(string text, HorizontalAlignment ausrichtung) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        Foreground = Grau,
        HorizontalAlignment = ausrichtung,
        VerticalAlignment = VerticalAlignment.Bottom,
        Margin = new Thickness(8, 0, 8, 6),
    };

    private static void Setzen(Grid raster, Control element, int zeile, int spalte)
    {
        Grid.SetRow(element, zeile);
        Grid.SetColumn(element, spalte);
        raster.Children.Add(element);
    }

    /// <summary>Macht ein Element anklickbar (Hand, Linksklick führt den Befehl aus) und gibt ihm einen Hinweis.</summary>
    private static void Klickbar(Control element, ICommand befehl, object? parameter, string? hinweis)
    {
        element.Cursor = Hand;
        element.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left && befehl.CanExecute(parameter))
            {
                befehl.Execute(parameter);
            }
        };
        if (!string.IsNullOrEmpty(hinweis))
        {
            ToolTip.SetTip(element, hinweis);
        }
    }

    private static string UmschalterText(KostenAnsichtViewModel m, Kostenmatrix matrix) =>
        m.LeereZeigen ? "− leere ausblenden" : $"+ {matrix.OhneKosten} ohne Kosten";

    /// <summary>Eine Kostenart der Verteilung: Farbe, Name und Anteil; anklickbar wie der Balkenabschnitt.</summary>
    private static Grid Legendeneintrag(KostenAnsichtViewModel m, Kostenanteil anteil, IBrush farbe)
    {
        var eintrag = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Background = Brushes.Transparent };
        var punkt = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(3), Background = farbe, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        var name = new TextBlock { Text = anteil.Name, FontSize = 12, Foreground = Tinte2Farbe, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var prozent = new TextBlock { Text = anteil.Prozent, FontSize = 11, FontFamily = Ziffern, Foreground = Grau, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        Setzen(eintrag, punkt, 0, 0);
        Setzen(eintrag, name, 0, 1);
        Setzen(eintrag, prozent, 0, 2);
        Klickbar(eintrag, m.AnteilWaehlenCommand, anteil, $"{anteil.Name}: {anteil.Prozent}");
        return eintrag;
    }

    /// <summary>Meldet sich nur an, solange die Ansicht angezeigt wird.</summary>
    private void Verbinden()
    {
        if (modell is not null)
        {
            modell.PropertyChanged -= BeiAenderung;
        }

        modell = angezeigt ? DataContext as KostenAnsichtViewModel : null;
        if (modell is not null)
        {
            modell.PropertyChanged += BeiAenderung;
        }

        KennzahlenAufbauen();
        MatrixAufbauen();
    }

    private void BeiAenderung(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(KostenAnsichtViewModel.Kennzahlen) or nameof(KostenAnsichtViewModel.Detail) or nameof(KostenAnsichtViewModel.Quelle))
        {
            KennzahlenAufbauen();
        }

        if (e.PropertyName == nameof(KostenAnsichtViewModel.Matrix))
        {
            MatrixAufbauen();
        }
    }

    private void KennzahlenAufbauen()
    {
        IReadOnlyList<Kennzahl> kennzahlen = modell?.Kennzahlen ?? [];
        Kennzahlen.Children.Clear();

        // Die Gesamtkosten der laufenden Generierung stehen schon groß im Kopf des Hauptfensters, der Chip entfällt dann.
        // Bei einem click-TT-Plan oder einem gemerkten Plan wird er gebraucht, denn der Kopf zeigt immer die Generierung.
        bool laufend = modell?.Quelle?.Art == PlanQuellenArt.LaufendeGenerierung;
        for (int i = laufend ? 1 : 0; i < kennzahlen.Count; i++)
        {
            Kennzahlen.Children.Add(Kachel(kennzahlen[i], haupt: i == 0));
        }
    }

    /// <summary>Eine Kennzahl als flacher Chip: Name klein, daneben der Wert und die Gewichtungsmarke.</summary>
    private Border Kachel(Kennzahl kennzahl, bool haupt)
    {
        var wert = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var zahl = new TextBlock { Text = kennzahl.Wert.Text, VerticalAlignment = VerticalAlignment.Center };
        zahl.Classes.Add("wert");
        wert.Children.Add(zahl);
        wert.Children.Add(new TextBlock
        {
            Text = kennzahl.Wert.Markierung,
            FontSize = 11,
            Margin = new Thickness(4, 0, 0, 0),
            Foreground = Markenfarbe(kennzahl.Wert.Markierung),
            VerticalAlignment = VerticalAlignment.Top,
        });

        var name = new TextBlock { Text = kennzahl.Name };
        name.Classes.Add("name");
        var inhalt = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        inhalt.Children.Add(name);
        inhalt.Children.Add(wert);

        var kachel = new Border { Child = inhalt };
        kachel.Classes.Add("karte");
        kachel.Classes.Add("kennzahl");
        kachel.Classes.Set("haupt", haupt);
        if (modell is { } m && kennzahl.Wert.Ziel is not null)
        {
            kachel.Classes.Add("klickbar");
            if (m.Detail is { Art: "KENNZAHL DES PLANS" } d && d.Titel == kennzahl.Name)
            {
                kachel.BorderBrush = Akzent;
                kachel.BorderThickness = new Thickness(2);
            }

            Klickbar(kachel, m.KennzahlWaehlenCommand, kennzahl, "Klicken: Details und Gewichtung rechts");
        }

        return kachel;
    }

    private void MatrixAufbauen()
    {
        Kostenmatrix matrix = modell?.Matrix ?? Kostenmatrix.Leer;
        VerteilungAufbauen(matrix);
        Tabelle.Children.Clear();
        Tabelle.RowDefinitions.Clear();
        Tabelle.ColumnDefinitions.Clear();
        if (modell is not { } m || matrix.Zeilen.Count == 0)
        {
            return;
        }

        Tabelle.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(210)));
        foreach (Matrixspalte spalte in matrix.Spalten)
        {
            Tabelle.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(spalte.HatKosten ? Spaltenbreite : LeereSpaltenbreite)));
        }

        bool umschalter = matrix.OhneKosten > 0;
        if (umschalter)
        {
            Tabelle.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(LeereSpaltenbreite + 6)));
        }

        Tabelle.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(120)));
        for (int i = 0; i <= matrix.Zeilen.Count; i++)
        {
            Tabelle.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        // Senkrechte Köpfe: so hoch wie der längste Name (statt fest 170 px), auch der Text des Umschalters.
        double texthoehe = matrix.Spalten.Select(sp => Textbreite(sp.Name, FontWeight.SemiBold)).DefaultIfEmpty(0).Max() + 4;
        if (umschalter)
        {
            texthoehe = Math.Max(texthoehe, Textbreite(UmschalterText(m, matrix), FontWeight.SemiBold) - 20);
        }

        int gesamtSpalte = matrix.Spalten.Count + (umschalter ? 2 : 1);
        if (umschalter)
        {
            Setzen(Tabelle, LeereUmschalter(m, matrix, texthoehe), 0, matrix.Spalten.Count + 1);
        }

        Setzen(Tabelle, Spaltenkopf("MANNSCHAFT", HorizontalAlignment.Left), 0, 0);
        for (int s = 0; s < matrix.Spalten.Count; s++)
        {
            Setzen(Tabelle, Kopfzelle(m, matrix.Spalten[s], texthoehe), 0, s + 1);
        }

        Setzen(Tabelle, Spaltenkopf("GESAMT", HorizontalAlignment.Right), 0, gesamtSpalte);
        for (int z = 0; z < matrix.Zeilen.Count; z++)
        {
            Matrixzeile zeile = matrix.Zeilen[z];
            Setzen(Tabelle, Namenszelle(m, zeile), z + 1, 0);
            for (int s = 0; s < zeile.Zellen.Count; s++)
            {
                Setzen(Tabelle, Zelle(m, zeile.Zellen[s]), z + 1, s + 1);
            }

            var gesamt = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8, 0) };
            gesamt.Children.Add(Balken(zeile.Balken, 36));
            gesamt.Children.Add(new TextBlock { Text = zeile.Gesamt, FontFamily = Ziffern, FontSize = 12, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            Setzen(Tabelle, gesamt, z + 1, gesamtSpalte);
        }
    }

    /// <summary>Kopf einer Kostenart: Name senkrecht, darunter Gewichtungsmarke und Anteilsbalken.</summary>
    private Border Kopfzelle(KostenAnsichtViewModel m, Matrixspalte spalte, double texthoehe)
    {
        var inhalt = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center };
        inhalt.Children.Add(new LayoutTransformControl
        {
            LayoutTransform = new RotateTransform(-90),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = new TextBlock
            {
                Text = spalte.Name,
                FontSize = Kopfschrift,
                FontWeight = spalte.HatKosten ? FontWeight.SemiBold : FontWeight.Normal,
                Foreground = spalte.HatKosten ? Tinte : Blass,
            },
        });
        var marke = new Panel { Height = 18, HorizontalAlignment = HorizontalAlignment.Center };
        if (spalte.Marke.Length > 0)
        {
            marke.Children.Add(Marke(spalte.Marke));
        }

        inhalt.Children.Add(marke);
        inhalt.Children.Add(Balken(spalte.Balken, spalte.HatKosten ? 40 : 20));
        var kopf = new Border
        {
            Child = inhalt,
            Height = texthoehe + 44,
            Margin = new Thickness(1, 0, 1, 4),
            Padding = new Thickness(0, 4, 0, 6),
            CornerRadius = new CornerRadius(8),
            Background = spalte.Gewaehlt ? AkzentHell : Brushes.Transparent,
            BorderBrush = spalte.Gewaehlt ? Akzent : Brushes.Transparent,
            BorderThickness = new Thickness(spalte.Gewaehlt ? 2 : 0),
        };
        Klickbar(kopf, m.SpalteWaehlenCommand, spalte, spalte.Name + " – klicken: Details und Gewichtung für alle Mannschaften");
        return kopf;
    }

    /// <summary>
    /// Kopf der schmalen Spalte hinter den Kostenarten: blendet die Kostenarten ohne Kosten dort ein, wo sie erscheinen,
    /// bzw. wieder aus.
    /// </summary>
    private Border LeereUmschalter(KostenAnsichtViewModel m, Kostenmatrix matrix, double texthoehe)
    {
        bool offen = m.LeereZeigen;
        string text = UmschalterText(m, matrix);
        var inhalt = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center };
        inhalt.Children.Add(new LayoutTransformControl
        {
            LayoutTransform = new RotateTransform(-90),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Akzent },
        });
        var kopf = new Border
        {
            Child = inhalt,
            Height = texthoehe + 44,
            Margin = new Thickness(4, 0, 2, 4),
            Padding = new Thickness(0, 6),
            CornerRadius = new CornerRadius(8),
            Background = AkzentHell,
        };
        string hinweis = offen
            ? "Kostenarten ohne Kosten wieder ausblenden"
            : $"{matrix.OhneKosten} Kostenarten ohne Kosten einblenden (erscheinen als schmale graue Spalten)";
        Klickbar(kopf, m.LeereUmschaltenCommand, null, hinweis);
        return kopf;
    }

    private Border Namenszelle(KostenAnsichtViewModel m, Matrixzeile zeile)
    {
        var inhalt = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        inhalt.Children.Add(new TextBlock
        {
            Text = zeile.Name,
            FontSize = 13,
            FontWeight = zeile.Gewaehlt ? FontWeight.Bold : FontWeight.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = zeile.Marke.Length > 0 ? 160 : 190,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (zeile.Marke.Length > 0)
        {
            inhalt.Children.Add(Marke(zeile.Marke));
        }

        var name = new Border
        {
            Child = inhalt,
            Height = Zeilenhoehe,
            Margin = new Thickness(0, 1, 6, 1),
            Padding = new Thickness(8, 0),
            CornerRadius = new CornerRadius(6),
            Background = zeile.Gewaehlt ? AkzentHell : Brushes.Transparent,
        };
        Klickbar(name, m.ZeileWaehlenCommand, zeile, zeile.Name + " – klicken: Details und Gewichtung der ganzen Mannschaft");
        return name;
    }

    private Border Zelle(KostenAnsichtViewModel m, Matrixzelle zelle)
    {
        (IBrush hintergrund, IBrush schrift) = Einfaerbung(zelle.Anteil);
        var inhalt = new Panel();
        inhalt.Children.Add(new TextBlock
        {
            Text = zelle.Text,
            FontFamily = Ziffern,
            FontSize = 11,
            Foreground = schrift,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (zelle.Geaendert)
        {
            inhalt.Children.Add(new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = Akzent,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 3, 3, 0),
            });
        }

        var rahmen = new Border
        {
            Child = inhalt,
            Height = Zeilenhoehe,
            Margin = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Background = hintergrund,
            BorderBrush = zelle.Gewaehlt ? Akzent : Linie,
            BorderThickness = new Thickness(zelle.Gewaehlt ? 2 : 0),
        };
        Klickbar(rahmen, m.ZelleWaehlenCommand, zelle, zelle.Hinweis);
        return rahmen;
    }

    /// <summary>
    /// Stellt die Verteilung neben die Matrix, wenn beide in die sichtbare Breite passen (im Maßstab des Zooms), sonst
    /// darunter. Läuft nach jedem Layout, ändert aber nur bei Bedarf etwas.
    /// </summary>
    private void KartenAnordnen()
    {
        double faktor = (DataContext as KostenAnsichtViewModel)?.Zoom.Faktor ?? 1;
        double verfuegbar = Rollbereich.Viewport.Width / Math.Max(faktor, 0.01);
        bool daneben = MatrixKarte.Bounds.Width + VerteilungKarte.Width + Kartenabstand <= verfuegbar;
        int zeile = daneben ? 0 : 1;
        int spalte = daneben ? 1 : 0;
        if (Grid.GetRow(VerteilungKarte) != zeile || Grid.GetColumn(VerteilungKarte) != spalte)
        {
            Grid.SetRow(VerteilungKarte, zeile);
            Grid.SetColumn(VerteilungKarte, spalte);
            VerteilungKarte.Margin = daneben ? new Thickness(Kartenabstand, 0, 0, 0) : new Thickness(0, Kartenabstand, 0, 0);
        }
    }

    private void VerteilungAufbauen(Kostenmatrix matrix)
    {
        Verteilung.Children.Clear();
        Verteilung.ColumnDefinitions.Clear();
        VerteilungLegende.Children.Clear();
        if (modell is not { } m)
        {
            return;
        }

        for (int i = 0; i < matrix.Verteilung.Count; i++)
        {
            Kostenanteil anteil = matrix.Verteilung[i];
            Verteilung.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Math.Max(anteil.Anteil, 0.002), GridUnitType.Star)));
            var abschnitt = new Border
            {
                Background = Anteilsfarben[Math.Min(i, Anteilsfarben.Length - 1)],
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(0, 0, 2, 0),
            };
            Klickbar(abschnitt, m.AnteilWaehlenCommand, anteil, $"{anteil.Name}: {anteil.Prozent}");
            Grid.SetColumn(abschnitt, i);
            Verteilung.Children.Add(abschnitt);

            if (i < Anteilsfarben.Length)
            {
                VerteilungLegende.Children.Add(Legendeneintrag(m, anteil, Anteilsfarben[i]));
            }
        }
    }
}
