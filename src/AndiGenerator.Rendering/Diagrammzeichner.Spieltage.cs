// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace AndiGenerator.Rendering;

/// <summary>Das Diagramm „Spieltage“ als Zeitleiste: eine Zeile je Spieltag (nicht im Original).</summary>
public sealed partial class Diagrammzeichner
{
    private const double Zeilenhoehe = 26;
    private const double Klein = 10;
    private const double Klammerx = 150;

    private static readonly Farbe Spannengrau = new(0xC9, 0xCE, 0xD6);
    private static readonly Farbe Flaeche = new(0xF0, 0xEF, 0xEA);
    private static readonly Farbe Zartrot = new(0xF4, 0xCF, 0xC6);
    private static readonly Farbe Klammergrau = new(0x8C, 0x95, 0xA1);

    private readonly List<Diagrammhinweis> hinweise = [];

    /// <summary>Holt die Kreise des zuletzt gezeichneten Diagramms mit ihren Texten zum Überfahren mit der Maus.</summary>
    public IReadOnlyList<Diagrammhinweis> Hinweise => hinweise;

    /// <summary>Ungefähre Breite eines Textes; gleich beim Messen und Zeichnen, damit die Größe stimmt.</summary>
    private static double Schaetzen(string text, double groesse) => text.Length * groesse * 0.58;

    /// <summary>Die Spanne als Text: „4 Wo.“; leer, wenn alle in einer Woche spielen.</summary>
    private static string Spannentext(int wochen) =>
        wochen == 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"{wochen} Wo.");

    private static string Mittext(Spieltagszeile z) =>
        z.Mit.Count == 0 ? string.Empty : "überschneidet " + Spieltagszeitleiste.Bereiche(z.Mit);

    /// <summary>Der Text zu einem Kreis: Spieltag und Woche, darunter je Tag die Mannschaften mit Heim oder Gast.</summary>
    private static string Hinweistext(int nummer, Spieltagstermin t)
    {
        var zeilen = new List<string>
        {
            string.Create(Deutsch, $"Spieltag {nummer} · KW {ISOWeek.GetWeekOfYear(t.Woche.ToDateTime(TimeOnly.MinValue))} ({t.Woche:dd.MM.} – {t.Woche.AddDays(6):dd.MM.})"),
        };
        foreach (IGrouping<DateOnly, Spieltagsmannschaft> tag in t.Spiele.GroupBy(s => s.Tag))
        {
            zeilen.Add(tag.Key.ToString("dddd, dd.MM.", Deutsch));
            zeilen.AddRange(tag.Select(s => "   " + s.Name + (s.Heimspiel ? " (Heim)" : " (Gast)")));
        }

        return string.Join(Environment.NewLine, zeilen);
    }

    private static Farbe Gleichzeitigfarbe(int anzahl) => anzahl switch
    {
        <= 1 => Flaeche,
        2 => Zartrot,
        _ => Ueberlappung,
    };

    /// <summary>
    /// Je Spieltag eine Zeile: ein Kreis je Woche (die Zahl darin: Mannschaften in dieser Woche), die Spanne von der ersten
    /// bis zur letzten Woche – rot, wo andere Spieltage laufen – und rechts Länge und Überschneidungen. Oben, wie viele
    /// Spieltage in jeder Woche gleichzeitig laufen; eine Klammer verbindet Spieltage am selben Termin (Kompakt-,
    /// Doppelspieltage). So bleiben auch zwei Spiele an einem Tag lesbar.
    /// </summary>
    private Zeichengroesse Spieltage(double y)
    {
        IReadOnlyList<Spieltagszeile> zeilen = Spieltagszeitleiste.Ermitteln(d);
        Titel("Spieltage", y);
        y += 30;
        Datumszeile(y);
        y += 22;
        DateTime ende = Achsenende(zeilen);
        double rechts = X(ende) + 30;
        double breite = rechts + 70 + zeilen.Select(z => Schaetzen(Mittext(z), Normal)).DefaultIfEmpty(0).Max();
        breite = Math.Max(breite, rechts + Schaetzen("Spanne · Überschneidung (zu lang 10, überlappend 10)", Normal));
        GleichzeitigZeichnen(zeilen, y, ende);
        string kopf = "Spanne · Überschneidung";
        if (d.Verstoesse is { } v)
        {
            kopf += string.Create(CultureInfo.InvariantCulture, $" (zu lang {v.ZuLangeSpieltage.Count}, überlappend {v.UeberlappendeSpieltage.Count})");
        }

        Text(kopf, rechts, y, Normal, Beschriftung);
        y += 28;

        double oben = y;
        double unten = oben + (zeilen.Count * Zeilenhoehe);
        for (int i = 1; i < zeilen.Count; i += 2)
        {
            Rechteck(Band, 0, oben + (i * Zeilenhoehe), breite, oben + ((i + 1) * Zeilenhoehe));
        }

        for (DateTime tag = ErsterMontag(); tag <= ende; tag = tag.AddDays(7))
        {
            Linie(LinieGrau, X(tag), oben, X(tag), unten);
        }

        for (int i = 0; i < zeilen.Count; i++)
        {
            Zeile(zeilen[i], oben + (i * Zeilenhoehe) + (Zeilenhoehe / 2), rechts);
        }

        y = unten + 16;
        double legende = Legende(y, zeilen.Any(z => z.WieVorher));
        return new Zeichengroesse(Math.Max(breite, legende), y + 24);
    }

    /// <summary>Montag nach dem letzten Wunschtermin bzw. Spiel: Ende der Zeitachse.</summary>
    private DateTime Achsenende(IReadOnlyList<Spieltagszeile> zeilen)
    {
        DateTime letzter = d.Letzter.Date;
        if (zeilen.Count > 0)
        {
            DateTime spiel = zeilen.Max(z => z.Bis).ToDateTime(TimeOnly.MinValue);
            letzter = spiel > letzter ? spiel : letzter;
        }

        DateTime tag = ErsterMontag();
        while (tag <= letzter)
        {
            tag = tag.AddDays(7);
        }

        return tag;
    }

    /// <summary>Je Woche ein Feld: wie viele Spieltage gleichzeitig laufen (leer, wenn keiner).</summary>
    private void GleichzeitigZeichnen(IReadOnlyList<Spieltagszeile> zeilen, double y, DateTime ende)
    {
        Text("gleichzeitig", Klammerx, y, Normal, Beschriftung);
        for (DateTime tag = ErsterMontag(); tag < ende; tag = tag.AddDays(7))
        {
            int anzahl = Spieltagszeitleiste.Gleichzeitig(zeilen, DateOnly.FromDateTime(tag));
            if (anzahl == 0)
            {
                continue;
            }

            double x1 = X(tag) + 1;
            double x2 = X(tag.AddDays(7)) - 1;
            Rechteck(Gleichzeitigfarbe(anzahl), x1, y, x2, y + 17);
            Mittig(anzahl.ToString(CultureInfo.InvariantCulture), (x1 + x2) / 2, y + 8.5, Klein, Tinte, false);
        }
    }

    private void Zeile(Spieltagszeile z, double mitte, double rechts)
    {
        Text(string.Create(CultureInfo.InvariantCulture, $"Spieltag {z.Nummer}"), 0, mitte - 8);
        if (z.WieVorher)
        {
            double vorher = mitte - Zeilenhoehe;
            Linie(Klammergrau, Klammerx, vorher - 6, Klammerx + 6, vorher - 6, 1.5);
            Linie(Klammergrau, Klammerx + 6, vorher - 6, Klammerx + 6, mitte + 6, 1.5);
            Linie(Klammergrau, Klammerx + 6, mitte + 6, Klammerx, mitte + 6, 1.5);
            Text("am selben Termin", Klammerx + 12, mitte - (Zeilenhoehe / 2) - 7, Klein, Beschriftung);
        }

        double von = Wochenx(z.Von);
        double bis = Wochenx(z.Bis);
        if (bis > von)
        {
            Rechteck(Spannengrau, von, mitte - 4, bis, mitte + 4);
            foreach (Spieltagsabschnitt a in z.Ueberschneidungen)
            {
                double x1 = Math.Max(von, Wochenx(a.Von) - 14);
                Rechteck(Ueberlappung, x1, mitte - 4, Math.Min(bis, Wochenx(a.Bis) + 14), mitte + 4);
            }
        }

        foreach (Spieltagstermin t in z.Termine)
        {
            double radius = Math.Min(12, 6 + (0.7 * t.Mannschaften));
            f?.Kreis(Wochenx(t.Woche), mitte, radius, Heimspiel, Weiss, 1.5);
            hinweise.Add(new Diagrammhinweis(Wochenx(t.Woche), mitte, radius, Hinweistext(z.Nummer, t)));
            Mittig(t.Mannschaften.ToString(CultureInfo.InvariantCulture), Wochenx(t.Woche), mitte, Klein, Weiss, true);
        }

        // Rot, was die Bewertung als zu lang bzw. überlappend zählt (ohne Bewertung: Spanne ab drei Wochen).
        bool zuLang = d.Verstoesse is null ? z.Wochen >= 3 : d.Verstoesse.ZuLangeSpieltage.Contains(z.Nummer - 1);
        bool ueberlappt = d.Verstoesse?.UeberlappendeSpieltage.Contains(z.Nummer - 1) == true;
        string spanne = Spannentext(z.Wochen);
        if (spanne.Length > 0 || zuLang)
        {
            f?.Text(spanne.Length > 0 ? spanne : "zu lang", rechts, mitte - 8, Normal, zuLang ? Schlecht : Tinte, true);
        }

        string mit = Mittext(z);
        if (mit.Length == 0 && ueberlappt)
        {
            mit = "überlappt";
        }

        f?.Text(mit, rechts + 70, mitte - 8, Normal, ueberlappt ? Schlecht : Beschriftung, ueberlappt);
    }

    /// <summary>Zeichnet die Legende.</summary>
    /// <returns>Rechte Kante der Legende.</returns>
    private double Legende(double y, bool mitKlammer)
    {
        double x = 0;
        f?.Kreis(x + 9, y + 8, 9, Heimspiel, Weiss, 1.5);
        Mittig("3", x + 9, y + 8, Klein, Weiss, true);
        x = Legendentext("Woche, Zahl = Mannschaften in dieser Woche", x + 24, y);
        Rechteck(Spannengrau, x, y + 4, x + 36, y + 12);
        x = Legendentext("Spanne des Spieltags", x + 42, y);
        Rechteck(Ueberlappung, x, y + 4, x + 36, y + 12);
        x = Legendentext("überschneidet andere Spieltage", x + 42, y);
        Rechteck(Flaeche, x, y + 1, x + 14, y + 15);
        Rechteck(Zartrot, x + 16, y + 1, x + 30, y + 15);
        Rechteck(Ueberlappung, x + 32, y + 1, x + 46, y + 15);
        x = Legendentext("Spieltage gleichzeitig in der Woche", x + 52, y);
        if (mitKlammer)
        {
            Linie(Klammergrau, x, y, x + 6, y, 1.5);
            Linie(Klammergrau, x + 6, y, x + 6, y + 16, 1.5);
            Linie(Klammergrau, x + 6, y + 16, x, y + 16, 1.5);
            x = Legendentext("am selben Termin wie der vorige", x + 12, y);
        }

        return x;
    }

    private double Legendentext(string text, double x, double y)
    {
        Text(text, x, y, Normal, Beschriftung);
        return x + Schaetzen(text, Normal) + 24;
    }

    /// <summary>Mitte der Woche ab einem Montag auf der Zeitachse.</summary>
    private double Wochenx(DateOnly montag) => X(montag.ToDateTime(new TimeOnly(12, 0)).AddDays(3));

    /// <summary>Schreibt einen Text um einen Mittelpunkt.</summary>
    private void Mittig(string text, double x, double y, double groesse, Farbe farbe, bool fett)
    {
        if (f is null)
        {
            return;
        }

        double b = f.TextBreite(text, groesse, fett);
        f.Text(text, x - (b / 2), y - (groesse * Seitenumbruch.Zeilenfaktor / 2), groesse, farbe, fett);
    }
}
