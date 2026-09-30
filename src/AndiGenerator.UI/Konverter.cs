// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;
using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace AndiGenerator.UI;

/// <summary>Wertkonverter für die Views.</summary>
public static class Konverter
{
    // Farben aus Stil.axaml (Gut, Schlecht, Grau, Akzent, Tinte), damit Konverter und Oberfläche zusammenpassen.
    private static readonly IImmutableSolidColorBrush Gut = new ImmutableSolidColorBrush(Color.FromRgb(0x1D, 0x6B, 0x3A));
    private static readonly IImmutableSolidColorBrush Schlecht = new ImmutableSolidColorBrush(Color.FromRgb(0xB4, 0x23, 0x18));
    private static readonly IImmutableSolidColorBrush Warnung = new ImmutableSolidColorBrush(Color.FromRgb(0xB5, 0x6A, 0x00));
    private static readonly IImmutableSolidColorBrush Grau = new ImmutableSolidColorBrush(Color.FromRgb(0x5B, 0x64, 0x70));
    private static readonly IImmutableSolidColorBrush Akzent = new ImmutableSolidColorBrush(Color.FromRgb(0x1D, 0x5F, 0xA8));
    private static readonly IImmutableSolidColorBrush Tinte = new ImmutableSolidColorBrush(Color.FromRgb(0x1B, 0x1F, 0x24));
    private static readonly IImmutableSolidColorBrush AkzentHell = new ImmutableSolidColorBrush(Color.FromRgb(0xE6, 0xEE, 0xF8));
    private static readonly IImmutableSolidColorBrush AkzentDunkel = new ImmutableSolidColorBrush(Color.FromRgb(0x16, 0x4A, 0x84));
    private static readonly IImmutableSolidColorBrush ProblemHell = new ImmutableSolidColorBrush(Color.FromRgb(0xFB, 0xED, 0xEB));
    private static readonly IImmutableSolidColorBrush ProblemDunkel = new ImmutableSolidColorBrush(Color.FromRgb(0x7A, 0x1A, 0x12));
    private static readonly IImmutableSolidColorBrush WarnungHell = new ImmutableSolidColorBrush(Color.FromRgb(0xFB, 0xF1, 0xDC));
    private static readonly IImmutableSolidColorBrush WarnungDunkel = new ImmutableSolidColorBrush(Color.FromRgb(0x6E, 0x44, 0x00));
    private static readonly IImmutableSolidColorBrush GrauHell = new ImmutableSolidColorBrush(Color.FromRgb(0xF0, 0xEF, 0xEA));
    private static readonly IImmutableSolidColorBrush GutHell = new ImmutableSolidColorBrush(Color.FromRgb(0xE4, 0xF1, 0xE8));

    /// <summary>Holt die Farbe zum Zustand eines Qualitätskriteriums (grün, orange, rot).</summary>
    public static IValueConverter ZustandFarbe { get; } = new FuncValueConverter<Qualitaetszustand, IBrush>(z => z switch
    {
        Qualitaetszustand.Erfuellt => Gut,
        Qualitaetszustand.PflichtVerletzt => Schlecht,
        _ => Warnung,
    });

    /// <summary>Holt das Symbol zum Zustand eines Qualitätskriteriums.</summary>
    public static IValueConverter ZustandSymbol { get; } = new FuncValueConverter<Qualitaetszustand, string>(z => z switch
    {
        Qualitaetszustand.Erfuellt => "✓",
        Qualitaetszustand.PflichtVerletzt => "✗",
        _ => "!",
    });

    /// <summary>Holt die Farbe der Zusammenfassung (grün, wenn die Pflichtstufe erfüllt ist, sonst rot).</summary>
    public static IValueConverter PflichtFarbe { get; } = new FuncValueConverter<bool, IBrush>(erfuellt => erfuellt ? Gut : Schlecht);

    /// <summary>Holt die Schriftfarbe einer Terminplanzeile (Nachbarspiele grau, echte Nachbarmannschaften blau).</summary>
    public static IValueConverter ZeilenFarbe { get; } = new FuncValueConverter<Planzeilenart, IBrush>(art => art switch
    {
        Planzeilenart.Nachbarspiel => Grau,
        Planzeilenart.EchtesNachbarspiel => Akzent,
        _ => Tinte,
    });

    /// <summary>Holt die Schriftstärke einer Terminplanzeile (Überschriften fett).</summary>
    public static IValueConverter ZeilenGewicht { get; } =
        new FuncValueConverter<Planzeilenart, FontWeight>(art => art == Planzeilenart.Ueberschrift ? FontWeight.SemiBold : FontWeight.Normal);

    /// <summary>Holt die Schriftstärke einer Meldungszeile (Mannschaftsnamen fett).</summary>
    public static IValueConverter MeldungGewicht { get; } = new FuncValueConverter<bool, FontWeight>(ueberschrift => ueberschrift ? FontWeight.SemiBold : FontWeight.Normal);

    /// <summary>Holt die Schriftgröße einer Meldungszeile (Mannschaftsnamen größer wie im Original).</summary>
    public static IValueConverter MeldungGroesse { get; } = new FuncValueConverter<bool, double>(ueberschrift => ueberschrift ? 14 : 12);

    /// <summary>Holt den Abstand einer Meldungszeile (Abstand vor jedem Mannschaftsnamen).</summary>
    public static IValueConverter MeldungAbstand { get; } = new FuncValueConverter<bool, Thickness>(ueberschrift => ueberschrift ? new Thickness(0, 14, 0, 4) : new Thickness(16, 1, 0, 1));

    /// <summary>Holt einen Vergleich mit dem Konverterparameter (z. B. aktive Ansicht = „Kosten“) für die Hervorhebung links.</summary>
    public static IValueConverter Gleich { get; } =
        new FuncValueConverter<string, string, bool>((wert, vergleich) => string.Equals(wert, vergleich, StringComparison.Ordinal));

    /// <summary>Holt die Skalierung einer vergrößerbaren Ansicht zu ihrem Zoomfaktor (für <c>LayoutTransformControl</c>).</summary>
    public static IValueConverter Skalierung { get; } = new FuncValueConverter<double, ITransform>(faktor => new ScaleTransform(faktor, faktor));

    /// <summary>Holt die Schriftfarbe einer Terminwunschzeile (Hervorhebungen des Originals: gelb, rot, grün).</summary>
    public static IValueConverter WunschFarbe { get; } = new FuncValueConverter<Terminwunschfarbe, IBrush>(farbe => farbe switch
    {
        Terminwunschfarbe.Gelb => Warnung,
        Terminwunschfarbe.Rot => Schlecht,
        Terminwunschfarbe.Gruen => Gut,
        _ => Tinte,
    });

    /// <summary>Holt den Hintergrund einer hervorgehobenen Terminwunschzeile bzw. Statusmarke (ocker, rot, grün).</summary>
    public static IValueConverter WunschHintergrund { get; } = new FuncValueConverter<Terminwunschfarbe, IBrush>(farbe => farbe switch
    {
        Terminwunschfarbe.Gelb => WarnungHell,
        Terminwunschfarbe.Rot => ProblemHell,
        Terminwunschfarbe.Gruen => GutHell,
        _ => Brushes.Transparent,
    });

    /// <summary>Holt die Schrift einer hervorgehobenen Terminwunschzeile bzw. Statusmarke (dunkle Töne passend zum Hintergrund).</summary>
    public static IValueConverter WunschSchrift { get; } = new FuncValueConverter<Terminwunschfarbe, IBrush>(farbe => farbe switch
    {
        Terminwunschfarbe.Gelb => WarnungDunkel,
        Terminwunschfarbe.Rot => ProblemDunkel,
        Terminwunschfarbe.Gruen => Gut,
        _ => Tinte,
    });

    /// <summary>Holt den Innenabstand einer Zeile der Terminwunsch-Karte (hervorgehoben: wie ein Chip).</summary>
    public static IValueConverter EintragAbstand { get; } =
        new FuncValueConverter<bool, Thickness>(hervorgehoben => hervorgehoben ? new Thickness(8, 3) : new Thickness(0, 1));

    /// <summary>Holt den Einzug einer Terminwunschzeile nach ihrer Ebene.</summary>
    public static IValueConverter WunschEinzug { get; } = new FuncValueConverter<int, Thickness>(ebene => new Thickness(ebene * 16, ebene == 0 ? 8 : 0, 0, 0));

    /// <summary>Holt den Hintergrund eines Hinweis-Chips im Terminplan (rot, ocker, blau, grau).</summary>
    public static IValueConverter HinweisHintergrund { get; } = new FuncValueConverter<Hinweisart, IBrush>(art => art switch
    {
        Hinweisart.Problem => ProblemHell,
        Hinweisart.Warnung => WarnungHell,
        Hinweisart.Info => AkzentHell,
        _ => GrauHell,
    });

    /// <summary>Holt die Schriftfarbe eines Hinweis-Chips im Terminplan.</summary>
    public static IValueConverter HinweisSchrift { get; } = new FuncValueConverter<Hinweisart, IBrush>(art => art switch
    {
        Hinweisart.Problem => ProblemDunkel,
        Hinweisart.Warnung => WarnungDunkel,
        Hinweisart.Info => AkzentDunkel,
        _ => Grau,
    });

    /// <summary>Holt die Schriftstärke einer Terminwunschzeile (Mannschaften und Zwischenüberschriften fett).</summary>
    public static IValueConverter WunschGewicht { get; } =
        new FuncValueConverter<int, FontWeight>(ebene => ebene < 2 ? FontWeight.SemiBold : FontWeight.Normal);
}
