// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Engine.Turniere;
using AndiGenerator.Persistence.Geo;

namespace AndiGenerator.Presentation;

/// <summary>Baut die Karte der Fahrten zu Kompaktspieltagen – für die Ansicht und den Ausdruck (nicht im Original).</summary>
public static class Kompaktfahrtenkarte
{
    /// <summary>Baut die Karte der Fahrten zu den sichtbaren Spieltagen.</summary>
    /// <param name="a">Die Auswertung des Plans.</param>
    /// <param name="sichtbar">Die Spieltage, deren Fahrten gezeigt werden.</param>
    /// <param name="team">Nur die Fahrten dieser Mannschaft oder <c>null</c> für alle.</param>
    /// <param name="alleZeigen">Alle Fahrten der Halbrunde (blass, ohne Pfeile) statt einer Auswahl.</param>
    /// <returns>Das Bild.</returns>
    public static Kartenbild Bauen(Kompaktauswertung a, IReadOnlyList<Kompaktspieltag> sichtbar, string? team, bool alleZeigen)
    {
        (List<Kartenort> orte, List<(string Von, string Nach, bool Kraeftig, bool Einzel, int Spieltag)> fahrten) = Eingaben(a, sichtbar, team, alleZeigen);
        return Kartenbau.MitSpieltagen(orte, fahrten, Kartendaten.Laender, Kartendaten.Staedte);
    }

    /// <summary>Die Fahrten einer Mannschaft zu ihren Auswärtsspieltagen (wie ein Klick auf die Mannschaft in der Ansicht).</summary>
    /// <param name="a">Die Auswertung des Plans.</param>
    /// <param name="team">Die Mannschaft.</param>
    /// <returns>Das Bild.</returns>
    public static Kartenbild Mannschaft(Kompaktauswertung a, string team)
    {
        ArgumentNullException.ThrowIfNull(a);
        return Bauen(a, a.Spieltage.Where(s => s.Gaeste.Contains(team)).ToList(), team, alleZeigen: false);
    }

    /// <summary>Wie das einfache Bauen, aber das vorige Bild bleibt, wenn Spielorte und Fahrten gleich sind (Eingaben im Raster).</summary>
    /// <param name="a">Die Auswertung des Plans.</param>
    /// <param name="sichtbar">Die Spieltage, deren Fahrten gezeigt werden.</param>
    /// <param name="team">Nur die Fahrten dieser Mannschaft oder <c>null</c> für alle.</param>
    /// <param name="alleZeigen">Alle Fahrten der Halbrunde statt einer Auswahl.</param>
    /// <param name="vorher">Das vorige Bild mit seinen Eingaben, oder <c>null</c>.</param>
    /// <returns>Das Bild mit seinen Eingaben (zum Merken).</returns>
    internal static (Kartenbild Bild, object Eingaben) Bauen(Kompaktauswertung a, IReadOnlyList<Kompaktspieltag> sichtbar, string? team, bool alleZeigen, (Kartenbild Bild, object Eingaben)? vorher)
    {
        (List<Kartenort> orte, List<(string Von, string Nach, bool Kraeftig, bool Einzel, int Spieltag)> fahrten) = Eingaben(a, sichtbar, team, alleZeigen);
        if (vorher is { Eingaben: (List<Kartenort> alteOrte, List<(string, string, bool, bool, int)> alteFahrten) } v
            && alteOrte.SequenceEqual(orte) && alteFahrten.SequenceEqual(fahrten))
        {
            return v;
        }

        return (Kartenbau.MitSpieltagen(orte, fahrten, Kartendaten.Laender, Kartendaten.Staedte), (orte, fahrten));
    }

    private static (List<Kartenort> Orte, List<(string Von, string Nach, bool Kraeftig, bool Einzel, int Spieltag)> Fahrten) Eingaben(Kompaktauswertung a, IReadOnlyList<Kompaktspieltag> sichtbar, string? team, bool alleZeigen)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(sichtbar);

        // Die Farben der Spieltage (wie im Raster) nur in Karten je Spieltag bzw. Sonntag; je Mannschaft blau, Einzelbegegnung gelb.
        var fahrten = sichtbar.Where(s => s.Ausrichter is not null)
            .SelectMany(s => s.Gaeste.Where(g => team is null || g == team).Select(g => (Von: g, Nach: s.Ausrichter!, Kraeftig: !alleZeigen, Einzel: s.Form == Turnierform.Einzel, Spieltag: alleZeigen || team is not null ? -1 : SpieltagAmTag(a, s))))
            .ToList();
        var ausrichter = sichtbar.Select(s => s.Ausrichter).OfType<string>().ToHashSet(StringComparer.Ordinal);
        List<Kartenort> orte = a.Mannschaften
            .Where(m => m.Lage is not null)
            .Select(m => new Kartenort(
                m.Name,
                m.Name,
                m.Lage.GetValueOrDefault(),
                !alleZeigen && ausrichter.Contains(m.Name),
                team is not null && m.Name != team && !ausrichter.Contains(m.Name),
                m.Halle.Length > 0 ? $"{m.Name} – {m.Halle}" : m.Name))
            .ToList();
        return (orte, fahrten);
    }

    /// <summary>Der wievielte Spieltag seines Sonntags ein Spieltag ist (wie im Raster: 0 = erster).</summary>
    private static int SpieltagAmTag(Kompaktauswertung a, Kompaktspieltag s) => a.Spieltage.Where(x => x.Tag == s.Tag).ToList().IndexOf(s);
}
