// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Rendering;

/// <summary>
/// Diagramme wie im Original (<c>PaintVerteilung</c>): Spieltage, Wechsel Heim/Auswärts, Spielverteilung, Abstand
/// Heimspiel zu Auswärtsspiel, Anzahl Spiele pro Woche und Setzliste. Inhalt und Koordinaten wie im Original, Farben und
/// Linien in der Gestaltung von AndiGenerator.NET (ruhige Bänder, Heimspiel gefüllt im Akzentblau, Auswärtsspiel hohl).
/// Dieselbe Routine zeichnet auf den Bildschirm und in den Ausdruck und misst ohne Zeichenfläche.
/// </summary>
public sealed partial class Diagrammzeichner
{
    private const double Links = 300;
    private const double Normal = 12;
    private const double Gross = 16;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    // Farben der Gestaltung (Stil.axaml): Tinte, Grau, Linie, Band, Akzent, Gut, Schlecht.
    private static readonly Farbe Tinte = new(0x1B, 0x1F, 0x24);
    private static readonly Farbe Beschriftung = new(0x5B, 0x64, 0x70);
    private static readonly Farbe Band = new(0xF7, 0xF6, 0xF2);
    private static readonly Farbe LinieGrau = new(0xE2, 0xE0, 0xDA);
    private static readonly Farbe Heimspiel = new(0x1D, 0x5F, 0xA8);
    private static readonly Farbe Weiss = new(0xFF, 0xFF, 0xFF);
    private static readonly Farbe Gut = new(0x1D, 0x6B, 0x3A);
    private static readonly Farbe Schlecht = new(0xB4, 0x23, 0x18);
    private static readonly Farbe Ueberlappung = new(0xE8, 0xAA, 0x9E);

    private readonly Diagrammdaten d;
    private readonly IZeichenflaeche? f;
    private readonly bool mitTiteln;

    /// <summary>Initialisiert den Zeichner mit Titeln über jedem Diagramm (Ausdruck).</summary>
    /// <param name="daten">Die Diagrammdaten.</param>
    /// <param name="flaeche">Zeichenfläche oder <c>null</c> zum reinen Messen.</param>
    public Diagrammzeichner(Diagrammdaten daten, IZeichenflaeche? flaeche)
        : this(daten, flaeche, mitTiteln: true)
    {
    }

    /// <summary>Initialisiert den Zeichner.</summary>
    /// <param name="daten">Die Diagrammdaten.</param>
    /// <param name="flaeche">Zeichenfläche oder <c>null</c> zum reinen Messen.</param>
    /// <param name="mitTiteln">Titel über jedem Diagramm zeichnen; die Ansicht zeigt ihn selbst an.</param>
    public Diagrammzeichner(Diagrammdaten daten, IZeichenflaeche? flaeche, bool mitTiteln)
    {
        ArgumentNullException.ThrowIfNull(daten);
        d = daten;
        f = flaeche;
        this.mitTiteln = mitTiteln;
    }

    private int Anzahl => d.Mannschaften.Count;

    /// <summary>Die Diagramme, die für diese Daten gezeichnet werden, in der Reihenfolge des Originals.</summary>
    /// <param name="daten">Die Diagrammdaten.</param>
    /// <returns>Die Diagramme; leer, wenn keine Spiele terminiert sind.</returns>
    public static IReadOnlyList<Diagrammteil> Teile(Diagrammdaten daten)
    {
        ArgumentNullException.ThrowIfNull(daten);
        if (!daten.HatSpiele)
        {
            return [];
        }

        // Bei Kompaktspieltagen zählt nicht der Wechsel, sondern wer ausrichtet und wo das Heimrecht liegt.
        Diagrammteil wechsel = daten.Kompakttage is { Count: > 0 } ? Diagrammteil.Ausrichter : Diagrammteil.WechselHeimAuswaerts;
        var teile = new List<Diagrammteil> { Diagrammteil.Spieltage, wechsel, Diagrammteil.Spielverteilung };
        if (daten.MitRueckrunde)
        {
            teile.Add(Diagrammteil.AbstandHeimAuswaerts);
        }

        teile.Add(Diagrammteil.SpieleProWoche);
        if (daten.HatSetzliste)
        {
            teile.Add(Diagrammteil.Setzliste);
        }

        return teile;
    }

    /// <summary>Original <c>PaintVerteilung</c>: alle Diagramme untereinander.</summary>
    /// <returns>Benötigte Größe.</returns>
    public Zeichengroesse Alles()
    {
        if (!d.HatSpiele)
        {
            Text("Keine Spiele terminiert", 0, 0);
            return new Zeichengroesse(600, 40);
        }

        double xMax = 500;
        double y = -40;
        foreach (Diagrammteil teil in Teile(d))
        {
            Zeichengroesse s = Zeichnen(teil, y + 40);
            xMax = Math.Max(xMax, s.Breite);
            y = s.Hoehe;
        }

        return new Zeichengroesse(xMax + 100, y);
    }

    /// <summary>Zeichnet ein Diagramm ab einer Höhe.</summary>
    /// <param name="teil">Das Diagramm.</param>
    /// <param name="y">Oberkante.</param>
    /// <returns>Benötigte Breite und die Unterkante.</returns>
    public Zeichengroesse Zeichnen(Diagrammteil teil, double y) => teil switch
    {
        Diagrammteil.Spieltage => Spieltage(y),
        Diagrammteil.WechselHeimAuswaerts => WechselHeimAuswaerts(y),
        Diagrammteil.Ausrichter => Ausrichter(y),
        Diagrammteil.Spielverteilung => Spielverteilung(y),
        Diagrammteil.AbstandHeimAuswaerts => AbstandHinRueck(y),
        Diagrammteil.SpieleProWoche => SpieleProWoche(y),
        Diagrammteil.Setzliste => Setzliste(y),
        _ => throw new ArgumentOutOfRangeException(nameof(teil), teil, "Unbekanntes Diagramm."),
    };

    /// <summary>Original <c>xPosByDate</c>.</summary>
    private double X(DateTime zeitpunkt) => Links + Math.Truncate((zeitpunkt - d.Erster).TotalDays * 4);

    private DateTime ErsterMontag()
    {
        DateTime tag = d.Erster.Date;
        return tag.AddDays(-(((int)tag.DayOfWeek + 6) % 7));
    }

    /// <summary>Original <c>PaintDateLine</c>: Kalenderwochen.</summary>
    private void Datumszeile(double y)
    {
        Text("KW", 0, y, Normal, Beschriftung);
        for (DateTime tag = ErsterMontag(); tag < d.Letzter; tag = tag.AddDays(7))
        {
            double x1 = X(tag);
            double x2 = X(tag.AddDays(7));
            Linie(LinieGrau, x1, y, x1, y + 15);
            Linie(LinieGrau, x2, y, x2, y + 15);
            Text(ISOWeek.GetWeekOfYear(tag).ToString(Deutsch), x1 + 10, y, Normal, Beschriftung);
        }
    }

    /// <summary>Original <c>PaintTableBackground</c>: jede zweite Zeile grau.</summary>
    private void Tabellenhintergrund(double y, double hoehe, double breite)
    {
        for (int zeile = 0; zeile < Anzahl; zeile += 2)
        {
            double yZeile = y + (zeile * hoehe);
            Rechteck(Band, 0, yZeile, breite, yZeile + hoehe);
        }
    }

    private void Titel(string text, double y)
    {
        if (mitTiteln)
        {
            f?.Text(text, 0, y, Gross, Tinte, true);
        }
    }

    private void Text(string text, double x, double y, double groesse = Normal, Farbe? farbe = null) =>
        f?.Text(text, x, y, groesse, farbe ?? Tinte, false);

    /// <summary>Schreibt einen Mannschaftsnamen in die linke Spalte, bei Bedarf gekürzt, damit er nicht ins Diagramm läuft.</summary>
    private void Name(string name, double y) => Text(Textkuerzung.Kuerzen(f, name, Links - 16, Normal, false), 0, y);

    private void Linie(Farbe farbe, double x1, double y1, double x2, double y2, double staerke = 1) => f?.Linie(x1, y1, x2, y2, farbe, staerke);

    private void Rechteck(Farbe farbe, double x1, double y1, double x2, double y2) => f?.Rechteck(x1, y1, x2 - x1, y2 - y1, farbe);
}
