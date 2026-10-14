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
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(sichtbar);
        var fahrten = sichtbar.Where(s => s.Ausrichter is not null)
            .SelectMany(s => s.Gaeste.Where(g => team is null || g == team).Select(g => (Von: g, Nach: s.Ausrichter!, Kraeftig: !alleZeigen, Einzel: s.Form == Turnierform.Einzel)))
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
        return Kartenbau.Bauen(orte, fahrten, Kartendaten.Laender, Kartendaten.Staedte);
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
}
