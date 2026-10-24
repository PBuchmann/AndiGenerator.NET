// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>
/// Zeichnet den Hinweis zu einer überfahrenen Stelle eines Diagramms: erst die Marken (Ringe oder Rahmen), dann den Text in
/// einem Kasten – rechts neben der Stelle und ihren Marken, wo Platz ist, sonst links davon, damit er nichts davon verdeckt.
/// </summary>
public static class Hinweiszeichner
{
    private const double Groesse = 12;
    private const double Rand = 8;
    private const double Abstand = 14;

    private static readonly Farbe Markenfarbe = new(0xC2, 0x41, 0x0C);
    private static readonly Farbe Kastenrand = new(0xC9, 0xCE, 0xD6);
    private static readonly Farbe Weiss = new(0xFF, 0xFF, 0xFF);
    private static readonly Farbe Tinte = new(0x1B, 0x1F, 0x24);

    /// <summary>Zeichnet Marken und Kasten.</summary>
    /// <param name="f">Die Zeichenfläche.</param>
    /// <param name="h">Der Hinweis.</param>
    /// <param name="breite">Breite der Zeichnung (der Kasten bleibt darin).</param>
    /// <param name="hoehe">Höhe der Zeichnung.</param>
    /// <returns>Linke und obere Kante des Kastens.</returns>
    public static (double X, double Y) Zeichnen(IZeichenflaeche f, Diagrammhinweis h, double breite, double hoehe)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(h);
        IReadOnlyList<Diagrammmarke> marken = h.Marken ?? [];
        foreach (Diagrammmarke m in marken)
        {
            if (m.Breite > 0)
            {
                f.Vieleck(Rundrechteck.Punkte(m.X - (m.Breite / 2), m.Y - (m.Hoehe / 2), m.X + (m.Breite / 2), m.Y + (m.Hoehe / 2), m.Radius), null, Markenfarbe, 2.5, geschlossen: true);
            }
            else
            {
                f.Kreis(m.X, m.Y, m.Radius, null, Markenfarbe, 2.5);
            }
        }

        string[] zeilen = h.Text.Split(Environment.NewLine);
        double zeile = Groesse * Seitenumbruch.Zeilenfaktor;

        // Die erste Zeile steht fett und ist damit breiter als gleich lange übrige Zeilen.
        double w = zeilen.Select((z, i) => f.TextBreite(z, Groesse, i == 0)).Max() + (2 * Rand);
        double k = (zeilen.Length * zeile) + (2 * Rand);

        // Was frei bleiben soll: die Stelle und alle Marken.
        double links = marken.Select(m => m.X - m.HalbeBreite).Append(h.Links).Min();
        double rechts = marken.Select(m => m.X + m.HalbeBreite).Append(h.Rechts).Max();
        double x = rechts + Abstand;
        if (x + w > breite)
        {
            x = links - Abstand - w;
        }

        if (x < 0)
        {
            x = Math.Max(0, Math.Min(h.Rechts + Abstand, breite - w));
        }

        double y = Math.Clamp(h.Y - (k / 2), 0, Math.Max(0, hoehe - k));
        f.Rechteck(x, y, w, k, Weiss);
        f.Vieleck([(x, y), (x + w, y), (x + w, y + k), (x, y + k)], null, Kastenrand, 1, geschlossen: true);
        for (int i = 0; i < zeilen.Length; i++)
        {
            f.Text(zeilen[i], x + Rand, y + Rand + (i * zeile), Groesse, Tinte, i == 0);
        }

        return (x, y);
    }
}
