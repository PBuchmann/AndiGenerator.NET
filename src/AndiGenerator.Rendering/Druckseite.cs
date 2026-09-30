// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace AndiGenerator.Rendering;

/// <summary>Eine fertig umbrochene Seite: Abschnittstitel und platzierte Elemente.</summary>
/// <param name="Abschnitt">Titel des Abschnitts.</param>
/// <param name="Elemente">Die Elemente des Inhalts.</param>
public sealed record Druckseite(string Abschnitt, IReadOnlyList<IDruckelement> Elemente)
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Zeichnet die Seite mit Kopf- und Fußzeile.</summary>
    /// <param name="flaeche">Die Zeichenfläche.</param>
    /// <param name="dokument">Der Ausdruck (Angaben der Fußzeile).</param>
    /// <param name="format">Das Seitenformat.</param>
    /// <param name="nummer">Seitennummer ab 1.</param>
    /// <param name="anzahl">Anzahl der Seiten.</param>
    public void Zeichnen(IZeichenflaeche flaeche, Druckdokument dokument, Seitenformat format, int nummer, int anzahl)
    {
        ArgumentNullException.ThrowIfNull(flaeche);
        ArgumentNullException.ThrowIfNull(dokument);
        ArgumentNullException.ThrowIfNull(format);
        double links = format.Rand;
        double rechts = format.Breite - format.Rand;
        flaeche.Text(Abschnitt, links, format.Rand, 14, Farbe.Tinte, true);
        double linie = format.Rand + 22;
        flaeche.Linie(links, linie, rechts, linie, Farbe.Linie, 0.8);
        flaeche.Linie(links, linie, links + 36, linie, Farbe.Akzent, 2);

        foreach (IDruckelement element in Elemente)
        {
            element.Zeichnen(flaeche);
        }

        double fuss = format.Hoehe - format.Rand - 8;
        flaeche.Linie(links, fuss - 4, rechts, fuss - 4, Farbe.Linie, 0.5);
        string angaben = string.Join(" · ", new[] { dokument.Staffel, dokument.Plan, dokument.Erstellt.ToString("dd.MM.yyyy HH:mm", Deutsch) }.Where(t => t.Length > 0));
        flaeche.Text(angaben, links, fuss, 7, Farbe.Grau, false);
        string seite = string.Create(Deutsch, $"Seite {nummer} von {anzahl}");
        flaeche.Text(seite, rechts - flaeche.TextBreite(seite, 7, false), fuss, 7, Farbe.Grau, false);
    }
}
