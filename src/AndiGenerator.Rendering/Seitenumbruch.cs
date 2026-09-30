// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>
/// Verteilt die Bausteine eines Ausdrucks auf Seiten. Anders als das Original (<c>TSimplePrinter.print</c>: nur
/// senkrechter Umbruch, zu breite Inhalte rechts abgeschnitten, Umbruch mitten durch eine Zeile möglich) fällt jeder
/// Umbruch zwischen zwei Zeilen, Tabellen wiederholen ihren Kopf, Überschriften bleiben bei ihrer ersten Zeile und zu
/// breite Tabellen werden verkleinert.
/// </summary>
public sealed class Seitenumbruch
{
    /// <summary>Zeilenhöhe im Verhältnis zur Schriftgröße.</summary>
    public const double Zeilenfaktor = 1.3;

    private readonly Seitenformat format;
    private readonly IZeichenflaeche messung;
    private readonly List<Druckseite> seiten = [];
    private List<IDruckelement> elemente = [];
    private string titel = string.Empty;
    private string seitentitel = string.Empty;
    private bool seiteOffen;
    private double y;

    private Seitenumbruch(Seitenformat format, IZeichenflaeche messung)
    {
        this.format = format;
        this.messung = messung;
    }

    private bool AmSeitenanfang => y <= format.InhaltOben;

    /// <summary>Umbricht einen Ausdruck.</summary>
    /// <param name="dokument">Der Ausdruck.</param>
    /// <param name="format">Das Seitenformat.</param>
    /// <param name="messung">Zeichenfläche, mit der Texte gemessen werden (dieselbe Schrift wie beim Zeichnen).</param>
    /// <returns>Die Seiten; jeder Abschnitt beginnt auf einer neuen Seite.</returns>
    public static IReadOnlyList<Druckseite> Umbrechen(Druckdokument dokument, Seitenformat format, IZeichenflaeche messung)
    {
        ArgumentNullException.ThrowIfNull(dokument);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(messung);
        var umbruch = new Seitenumbruch(format, messung);
        foreach (Abschnitt abschnitt in dokument.Abschnitte)
        {
            umbruch.titel = abschnitt.Titel;
            umbruch.NeueSeite();
            foreach (Baustein baustein in abschnitt.Bausteine)
            {
                umbruch.Setzen(baustein);
            }
        }

        umbruch.Abschliessen();
        return umbruch.seiten;
    }

    /// <summary>Bricht einen Text an Wortgrenzen (und an Zeilenumbrüchen) auf die Breite um.</summary>
    /// <param name="text">Der Text.</param>
    /// <param name="breite">Verfügbare Breite.</param>
    /// <param name="groesse">Schriftgröße.</param>
    /// <param name="fett">Fettschrift.</param>
    /// <param name="messung">Zeichenfläche zum Messen.</param>
    /// <returns>Die Zeilen; ein einzelnes zu langes Wort bleibt ungeteilt.</returns>
    public static IReadOnlyList<string> Umbrechen(string text, double breite, double groesse, bool fett, IZeichenflaeche messung)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(messung);
        var zeilen = new List<string>();
        foreach (string absatz in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string zeile = string.Empty;
            foreach (string wort in absatz.Split(' '))
            {
                string versuch = zeile.Length == 0 ? wort : zeile + " " + wort;
                if (zeile.Length > 0 && messung.TextBreite(versuch, groesse, fett) > breite)
                {
                    zeilen.Add(zeile);
                    zeile = wort;
                }
                else
                {
                    zeile = versuch;
                }
            }

            zeilen.Add(zeile);
        }

        return zeilen;
    }

    private void NeueSeite()
    {
        Abschliessen();
        seiteOffen = true;
        seitentitel = titel;
        elemente = [];
        y = format.InhaltOben;
    }

    private void Abschliessen()
    {
        if (seiteOffen)
        {
            seiten.Add(new Druckseite(seitentitel, elemente));
            seiteOffen = false;
        }
    }

    private bool Passt(double hoehe) => y + hoehe <= format.InhaltUnten + 0.01;

    private void Setzen(Baustein baustein)
    {
        if (!AmSeitenanfang)
        {
            y += baustein.AbstandDavor;
        }

        switch (baustein)
        {
            case Abstand:
                break;
            case Textzeile text:
                Setzen(text);
                break;
            case Tabelle tabelle:
                Setzen(tabelle);
                break;
            case Grafik grafik:
                Setzen(grafik);
                break;
            default:
                throw new ArgumentException("Unbekannter Baustein: " + baustein.GetType().Name, nameof(baustein));
        }
    }

    private void Setzen(Textzeile text)
    {
        double zeilenhoehe = text.Groesse * Zeilenfaktor;
        IReadOnlyList<string> zeilen = Umbrechen(text.Text, format.Nutzbreite - text.Einzug, text.Groesse, text.Fett, messung);
        foreach (string zeile in zeilen)
        {
            if (!Passt(zeilenhoehe))
            {
                NeueSeite();
            }

            elemente.Add(new TextElement(zeile, format.Rand + text.Einzug, y, text.Groesse, text.Farbe, text.Fett));
            y += zeilenhoehe;
        }
    }

    private void Setzen(Grafik grafik)
    {
        if (grafik.Breite <= 0 || grafik.Hoehe <= 0)
        {
            return;
        }

        double skala = Math.Min(1, Math.Min(format.Nutzbreite / grafik.Breite, (format.InhaltUnten - format.InhaltOben) / grafik.Hoehe));
        double hoehe = grafik.Hoehe * skala;
        if (!Passt(hoehe))
        {
            NeueSeite();
        }

        elemente.Add(new GrafikElement(format.Rand, y, skala, grafik.Zeichnung));
        y += hoehe;
    }

    private void Setzen(Tabelle tabelle)
    {
        var layout = new Tabellenlayout(tabelle, format.Nutzbreite, messung);
        int zeilenAufSeite = -1;
        for (int i = 0; i < tabelle.Zeilen.Count; i++)
        {
            Tabellenzeile zeile = tabelle.Zeilen[i];
            if (zeile.Art == Zeilenart.Abstand)
            {
                if (zeilenAufSeite > 0)
                {
                    y += layout.Hoehe(zeile);
                }

                continue;
            }

            double hoehe = layout.Hoehe(zeile);
            if (zeile.Art == Zeilenart.Ueberschrift && i + 1 < tabelle.Zeilen.Count && tabelle.Zeilen[i + 1].Art == Zeilenart.Normal)
            {
                // Die Überschrift bleibt bei ihrer ersten Zeile.
                hoehe += layout.Hoehe(tabelle.Zeilen[i + 1]);
            }

            if (zeilenAufSeite < 0 || !Passt(hoehe))
            {
                if (zeilenAufSeite >= 0 || !Passt(hoehe + layout.KopfHoehe))
                {
                    NeueSeite();
                }

                Kopf(layout);
                zeilenAufSeite = 0;
            }

            Zeile(layout, zeile);
            zeilenAufSeite++;
        }
    }

    private void Kopf(Tabellenlayout layout)
    {
        if (layout.Kopf is not IReadOnlyList<string> kopf)
        {
            return;
        }

        double unten = y + layout.KopfHoehe - 1;
        for (int s = 0; s < kopf.Count && s < layout.Spalten.Count; s++)
        {
            double x = format.Rand + layout.Spalten[s].X;
            if (layout.Senkrecht(s))
            {
                double mitte = x + (layout.Inhaltsbreite(s) / 2);
                elemente.Add(new SenkrechtElement(kopf[s], mitte - (layout.Groesse * 0.6), unten - 3, layout.Groesse, Farbe.Grau));
            }
            else
            {
                elemente.Add(new TextElement(kopf[s], x, unten - layout.Zeilenhoehe, layout.Groesse, Farbe.Grau, true));
            }
        }

        y = unten;
        elemente.Add(new LinienElement(format.Rand, y - 1, format.Rand + layout.Breite, y - 1, Farbe.Rand, 0.6));
        y += 1;
    }

    private void Zeile(Tabellenlayout layout, Tabellenzeile zeile)
    {
        if (zeile.Art == Zeilenart.Ueberschrift)
        {
            double groesse = layout.Groesse * 1.15;
            y += layout.Zeilenhoehe * 0.3;
            elemente.Add(new TextElement(zeile.Zellen.Count > 0 ? zeile.Zellen[0].Text : string.Empty, format.Rand, y, groesse, zeile.Farbe, true));
            y += groesse * Zeilenfaktor;
            return;
        }

        double hoehe = layout.Hoehe(zeile);
        if (layout.Matrix && zeile.Fett)
        {
            elemente.Add(new LinienElement(format.Rand, y, format.Rand + layout.Breite, y, Farbe.Rand, 0.8));
        }

        for (int s = 0; s < zeile.Zellen.Count && s < layout.Spalten.Count; s++)
        {
            Tabellenzelle zelle = zeile.Zellen[s];
            double x = format.Rand + layout.Spalten[s].X;
            if (zelle.Hintergrund is Farbe hintergrund)
            {
                double links = x - (layout.Abstand / 2);
                elemente.Add(new RechteckElement(links + 0.3, y + 0.3, layout.Spalten[s].Breite - 0.6, hoehe - 0.6, hintergrund));
            }

            double zy = y;
            foreach (string teil in layout.Zeilen(zelle, s, zeile.Fett))
            {
                double tx = layout.Senkrecht(s) ? x + ((layout.Inhaltsbreite(s) - messung.TextBreite(teil, layout.Groesse, zeile.Fett)) / 2) : x;
                elemente.Add(new TextElement(teil, tx, zy, layout.Groesse, zelle.Farbe ?? zeile.Farbe, zeile.Fett));
                zy += layout.Zeilenhoehe;
            }
        }

        y += hoehe;
        if (layout.Matrix)
        {
            elemente.Add(new LinienElement(format.Rand, y, format.Rand + layout.Breite, y, Farbe.Linie, 0.4));
        }
    }
}
