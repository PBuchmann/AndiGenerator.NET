// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Rendering;

/// <summary>
/// Zeitachse der Terminwünsche wie im Original (<c>PaintSpielPlanMeldungen</c>): Kalenderwochen oben, je Mannschaft eine
/// Zeile mit Strichen für Heimspielwünsche (schwarz), Koppeltermine (blau) und Sperrtermine (rot). Dieselbe Routine
/// zeichnet auf den Bildschirm und in den Ausdruck und misst ohne Zeichenfläche.
/// </summary>
public sealed class Terminwunschzeichner
{
    private const double NamenBreite = 300;
    private const double ProTag = 4;
    private const double Zeilenhoehe = 30;
    private const double Kopfhoehe = 25;
    private const double Schrift = 12;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    // Töne der Gestaltung von AndiGenerator.NET statt Hellgrau, reinem Blau und Rot.
    private static readonly Farbe Grau = Farbe.Flaeche;
    private static readonly Farbe Linienfarbe = Farbe.Linie;
    private static readonly Farbe Koppel = Farbe.Akzent;
    private static readonly Farbe Sperre = Farbe.Schlecht;

    private readonly Terminwunschuebersicht u;
    private readonly IZeichenflaeche? f;
    private readonly bool mitLegende;

    /// <summary>Initialisiert den Zeichner.</summary>
    /// <param name="uebersicht">Die Terminwünsche.</param>
    /// <param name="flaeche">Zeichenfläche oder <c>null</c> zum reinen Messen.</param>
    /// <param name="mitLegende">Legende unter dem Raster zeichnen (Ausdruck wie im Original).</param>
    public Terminwunschzeichner(Terminwunschuebersicht uebersicht, IZeichenflaeche? flaeche, bool mitLegende)
    {
        ArgumentNullException.ThrowIfNull(uebersicht);
        u = uebersicht;
        f = flaeche;
        this.mitLegende = mitLegende;
    }

    /// <summary>Zeichnet das Raster.</summary>
    /// <returns>Benötigte Größe; leer, wenn es keine Termine gibt.</returns>
    public Zeichengroesse Zeichnen()
    {
        if (u is not { Erster: DateOnly erster, Letzter: DateOnly letzter })
        {
            return default;
        }

        int anzahl = u.Mannschaften.Count;
        DateOnly montag = Montag(erster);
        double breite = X(montag.AddDays((((letzter.DayNumber - montag.DayNumber) / 7) + 1) * 7), erster);
        for (int i = 0; i < anzahl; i++)
        {
            double y = Kopfhoehe + (i * Zeilenhoehe);
            if (i % 2 == 0)
            {
                f?.Rechteck(0, y, breite, Zeilenhoehe, Grau);
            }

            Text(u.Mannschaften[i].Name, 0, y + 7);
        }

        Text("KW", 0, 3);
        for (DateOnly tag = montag; tag <= letzter; tag = tag.AddDays(7))
        {
            double x = X(tag, erster);
            f?.Linie(x, 0, x, Kopfhoehe + (anzahl * Zeilenhoehe), Linienfarbe, 1);
            Text(ISOWeek.GetWeekOfYear(tag.ToDateTime(TimeOnly.MinValue)).ToString(Deutsch), x + 10, 3);
        }

        for (int i = 0; i < anzahl; i++)
        {
            double y = Kopfhoehe + (i * Zeilenhoehe) + 10;
            foreach (Terminwunschmarke marke in u.Mannschaften[i].Marken)
            {
                // Wünsche stehen im Original mittags auf dem Tag, Sperrtermine am Tagesanfang.
                double x = X(marke.Tag, erster) + (marke.Art == Terminwunschart.Sperrtermin ? 0 : ProTag / 2);
                f?.Rechteck(x - 1, y, 2, 10, Markenfarbe(marke.Art));
            }
        }

        double hoehe = Kopfhoehe + (anzahl * Zeilenhoehe) + 5;
        if (mitLegende)
        {
            Legende(hoehe + 10);
            hoehe += 35;
        }

        return new Zeichengroesse(X(letzter, erster) + 60, hoehe);
    }

    private static DateOnly Montag(DateOnly tag) => tag.AddDays(-(((int)tag.DayOfWeek + 6) % 7));

    private static double X(DateOnly tag, DateOnly erster) => NamenBreite + ((tag.DayNumber - erster.DayNumber) * ProTag);

    private static Farbe Markenfarbe(Terminwunschart art) => art switch
    {
        Terminwunschart.Koppel => Koppel,
        Terminwunschart.Sperrtermin => Sperre,
        _ => Farbe.Tinte,
    };

    /// <summary>Legende wie im Ausdruck des Originals.</summary>
    private void Legende(double y)
    {
        double x = 4;
        foreach ((string text, Terminwunschart art) in Legendeneintraege())
        {
            f?.Rechteck(x, y, 2, 12, Markenfarbe(art));
            Text(text, x + 8, y);
            x += 150;
        }
    }

    private List<(string Text, Terminwunschart Art)> Legendeneintraege()
    {
        var eintraege = new List<(string Text, Terminwunschart Art)> { ("Terminwunsch", Terminwunschart.Wunsch) };
        if (u.HatKoppeltermine)
        {
            eintraege.Add(("Koppeltermin", Terminwunschart.Koppel));
        }

        eintraege.Add(("Sperrtermin", Terminwunschart.Sperrtermin));
        return eintraege;
    }

    private void Text(string text, double x, double y) => f?.Text(text, x, y, Schrift, Farbe.Tinte, false);
}
