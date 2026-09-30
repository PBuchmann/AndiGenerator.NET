// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;

namespace AndiGenerator.UI;

/// <summary>
/// Hilfe für die Anleitung: Strg+Umschalt+F12 speichert das aktive Fenster (Hauptfenster oder Dialog, ohne Titelleiste)
/// als PNG. Ziel ist der Ordner aus der Umgebungsvariablen <c>ANDIGEN_BILDSCHIRMFOTOS</c>, sonst
/// <c>Bilder\AndiGenerator.NET</c> des Benutzers.
/// </summary>
internal static class Bildschirmfoto
{
    private const string Variable = "ANDIGEN_BILDSCHIRMFOTOS";

    /// <summary>Meldet die Tastenkombination für alle Fenster an.</summary>
    public static void Anmelden() =>
        InputElement.KeyDownEvent.AddClassHandler<Window>(BeiTaste, handledEventsToo: true);

    private static void BeiTaste(Window fenster, KeyEventArgs e)
    {
        if (e.Key != Key.F12 || e.KeyModifiers != (KeyModifiers.Control | KeyModifiers.Shift))
        {
            return;
        }

        e.Handled = true;
        try
        {
            Speichern(fenster);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            // Ein Bildschirmfoto ist nur ein Hilfsmittel, das Programm arbeitet ohne es normal weiter.
            System.Diagnostics.Debug.WriteLine(fehler.Message);
        }
    }

    private static void Speichern(Window fenster)
    {
        string ordner = Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } eigener
            ? eigener
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AndiGenerator.NET");
        Directory.CreateDirectory(ordner);
        string pfad = Path.Combine(ordner, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".png");

        double skala = fenster.RenderScaling;
        var groesse = new PixelSize((int)Math.Ceiling(fenster.Bounds.Width * skala), (int)Math.Ceiling(fenster.Bounds.Height * skala));
        using var bild = new RenderTargetBitmap(groesse, new Vector(96 * skala, 96 * skala));
        bild.Render(fenster);

        // Die neue Speicher-Methode verlangt Kodierungsoptionen, die Avalonia 12 noch nicht dokumentiert, daher die bisherige.
#pragma warning disable CS0618
        bild.Save(pfad);
#pragma warning restore CS0618
    }
}
