// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Rendering;

/// <summary>Das Diagramm „Wechsel Heim/Auswärts“ als Kacheln je Spiel (nicht im Original).</summary>
public sealed partial class Diagrammzeichner
{
    private const double Spielbreite = 30;
    private const double Kachelbreite = 26;
    private const double Kachelhoehe = 18;
    private const double Rundenluecke = 16;
    private const double Bruchbalken = 70;
    private const int Lange = 3;

    private static readonly Farbe Gastgrau = new(0xDD, 0xE2, 0xE8);

    /// <summary>Die Serien einer Mannschaft: je Serie Heim oder Gast, erstes Spiel und Länge, in der Folge der Termine.</summary>
    /// <param name="m">Die Mannschaft.</param>
    /// <returns>Die Serien.</returns>
    public static IReadOnlyList<(bool Heim, int Start, int Laenge)> Serien(Diagrammmannschaft m)
    {
        ArgumentNullException.ThrowIfNull(m);
        List<bool> folge = m.Spiele.Where(s => s.Zeitpunkt is not null).OrderBy(s => s.Zeitpunkt).Select(s => s.Heimspiel).ToList();
        var serien = new List<(bool Heim, int Start, int Laenge)>();
        for (int i = 0; i < folge.Count; i++)
        {
            if (serien.Count > 0 && serien[^1].Heim == folge[i])
            {
                serien[^1] = serien[^1] with { Laenge = serien[^1].Laenge + 1 };
            }
            else
            {
                serien.Add((folge[i], i, 1));
            }
        }

        return serien;
    }

    /// <summary>Linke Kante der Kachel eines Spiels; ab der Rückrunde mit einer Lücke.</summary>
    private static double Kachelx(int k, int rueckrunde) =>
        Links + (k * Spielbreite) + (rueckrunde > 0 && k >= rueckrunde ? Rundenluecke : 0);

    /// <summary>Der Text zu einer Kachel: Spiel, Tag, Heim oder Gast und die Serie, in der es steht.</summary>
    private static string Kachelhinweis(int nummer, Diagrammspiel s, int serie)
    {
        string art = s.Heimspiel ? "Heimspiel" : "Auswärtsspiel";
        string verbindung = s.Heimspiel ? " gegen " : " bei ";
        string gegen = s.Gegner.Length == 0 ? art : art + verbindung + s.Gegner;
        var zeilen = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"{nummer}. Spiel, ") + s.Zeitpunkt!.Value.ToString("ddd dd.MM.yyyy", Deutsch),
            gegen,
        };
        if (s.Koppel)
        {
            zeilen.Add("Koppeltermin (zählt nicht als Bruch)");
        }

        if (serie >= 2)
        {
            string arten = s.Heimspiel ? "Heimspiele" : "Auswärtsspiele";
            zeilen.Add(string.Create(CultureInfo.InvariantCulture, $"{serie} {arten} hintereinander"));
        }

        return string.Join(Environment.NewLine, zeilen);
    }

    /// <summary>
    /// Je Mannschaft eine Reihe gleich breiter Kacheln, eine je Spiel: blau Heim, grau Auswärts. Ideal ist ein gleichmäßiges
    /// Schachbrett. Serien ab drei sind rot unterstrichen, eine Lücke trennt Hin- und Rückrunde; rechts die Brüche (Spiele,
    /// die nicht wechseln) als grauer Balken mit Zahl. Jede Kachel hat einen Hinweis mit Tag, Gegner und Serie.
    /// </summary>
    private Zeichengroesse WechselHeimAuswaerts(double y)
    {
        Titel("Wechsel Heim/Auswärts", y);
        y += 10;
        double rechts = Links + (Spielbreite * d.MaxSpiele) + Rundenluecke + 24;
        double breite = rechts + Bruchbalken + 40;
        List<List<Diagrammspiel>> folgen = d.Mannschaften
            .Select(m => m.Spiele.Where(s => s.Zeitpunkt is not null).OrderBy(s => s.Zeitpunkt).ToList())
            .ToList();
        int meiste = Math.Max(1, d.Mannschaften.Select(Brueche).DefaultIfEmpty(0).Max());
        Tabellenhintergrund(y + 22, 30, breite);
        Text("Brüche", rechts, y + 4, Normal, Beschriftung);
        for (int i = 0; i < d.Mannschaften.Count; i++)
        {
            y += 30;
            Name(d.Mannschaften[i].Name, y);
            List<Diagrammspiel> spiele = folgen[i];
            int rueckrunde = spiele.FindIndex(s => s.Runde > spiele[0].Runde);
            IReadOnlyList<(bool Heim, int Start, int Laenge)> serien = Serien(d.Mannschaften[i]);
            foreach ((bool heim, int start, int laenge) in serien)
            {
                for (int k = start; k < start + laenge; k++)
                {
                    double x = Kachelx(k, rueckrunde);
                    Kachel(heim, x, y);
                    hinweise.Add(new Diagrammhinweis(
                        x + (Kachelbreite / 2), y + 7, Kachelbreite / 2, Kachelhinweis(k + 1, spiele[k], laenge), Kachelbreite, Kachelhoehe));
                }

                if (laenge >= Lange)
                {
                    double von = Kachelx(start, rueckrunde);
                    double bis = Kachelx(start + laenge - 1, rueckrunde) + Kachelbreite;
                    f?.Vieleck(Rundrechteck.Punkte(von, y + 19, bis, y + 22, 1.5), Schlecht, null, 0, geschlossen: true);
                }
            }

            int brueche = Brueche(d.Mannschaften[i]);
            double balken = Bruchbalken * brueche / meiste;
            if (brueche > 0)
            {
                f?.Vieleck(Rundrechteck.Punkte(rechts, y + 3, rechts + balken, y + 11, 4), Spannengrau, null, 0, geschlossen: true);
            }

            f?.Text(brueche.ToString(CultureInfo.InvariantCulture), rechts + balken + 8, y, Normal, Tinte, true);
        }

        y += 40;
        double lx = 0;
        Kachel(true, lx, y);
        lx = Legendentext("Heimspiel", lx + Kachelbreite + 8, y);
        Kachel(false, lx, y);
        lx = Legendentext("Auswärtsspiel", lx + Kachelbreite + 8, y);
        f?.Vieleck(Rundrechteck.Punkte(lx, y + 7, lx + 40, y + 10, 1.5), Schlecht, null, 0, geschlossen: true);
        lx = Legendentext("Serie ab drei", lx + 48, y);
        lx = Legendentext("Lücke = Beginn der Rückrunde · Brüche wie in der Qualitätsansicht (Koppeltermine zählen nicht)", lx, y);
        return new Zeichengroesse(Math.Max(breite, lx), y + 24);
    }

    /// <summary>
    /// Die Brüche (Spiele, die nicht wechseln) wie in der Qualitätsansicht – dort zählen Koppeltermine nicht; ohne Bewertung
    /// alle Spiele, die nicht wechseln.
    /// </summary>
    private int Brueche(Diagrammmannschaft m) => d.Verstoesse?.Von(m.Name)?.Wechsel ?? Serien(m).Sum(s => s.Laenge - 1);

    /// <summary>Eine Kachel mit runden Ecken: blau Heim, grau Auswärts.</summary>
    private void Kachel(bool heim, double x, double y) =>
        f?.Vieleck(Rundrechteck.Punkte(x, y - 2, x + Kachelbreite, y + Kachelhoehe - 2, 4), heim ? Heimspiel : Gastgrau, null, 0, geschlossen: true);
}
