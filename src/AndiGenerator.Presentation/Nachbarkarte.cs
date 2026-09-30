// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Presentation;

/// <summary>Die Spiele der Nachbarmannschaften einer Mannschaft als Karte.</summary>
/// <param name="Name">Name der Mannschaft.</param>
/// <param name="Bilanz">Z. B. „12 Spiele an 8 Tagen“.</param>
/// <param name="Direkte">Z. B. „3 direkte Nachbarn“ oder leer.</param>
/// <param name="Tage">Die Tage mit ihren Spielen.</param>
public sealed record Nachbarkarte(string Name, string Bilanz, string Direkte, IReadOnlyList<Nachbartag> Tage)
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Holt einen Wert, der angibt, ob Spiele direkter Nachbarmannschaften dabei sind.</summary>
    public bool HatDirekte => Direkte.Length > 0;

    /// <summary>Bildet die Karten in der Reihenfolge des Originals.</summary>
    /// <param name="mannschaften">Die Nachbartermine je Mannschaft.</param>
    /// <returns>Die Karten.</returns>
    public static IReadOnlyList<Nachbarkarte> Bilden(IReadOnlyList<MannschaftsNachbartermine> mannschaften)
    {
        ArgumentNullException.ThrowIfNull(mannschaften);
        return mannschaften.Select(Karte).ToList();
    }

    private static Nachbarkarte Karte(MannschaftsNachbartermine m)
    {
        int spiele = m.Tage.Sum(t => t.Count);
        int direkte = m.Tage.Sum(t => t.Count(n => n.EchteNachbarmannschaft));
        string bilanz = string.Create(Deutsch, $"{spiele} {(spiele == 1 ? "Spiel" : "Spiele")} an {m.Tage.Count} {(m.Tage.Count == 1 ? "Tag" : "Tagen")}");
        string direkt = direkte == 0 ? string.Empty : string.Create(Deutsch, $"{direkte} direkte");
        List<Nachbartag> tage = m.Tage
            .Where(t => t.Count > 0)
            .Select(t => new Nachbartag(
                t[0].Zeitpunkt.ToString("ddd dd.MM.yyyy", Deutsch),
                t.Select(n => new Nachbarspiel(n.Zeitpunkt.ToString("HH:mm", Deutsch), $"({n.Geschlecht}) {n.Heim} – {n.Gast}", n.Spiellokal, n.EchteNachbarmannschaft)).ToList()))
            .ToList();
        return new Nachbarkarte(m.Name, bilanz, direkt, tage);
    }
}
