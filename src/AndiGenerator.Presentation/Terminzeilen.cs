// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Presentation;

/// <summary>Zeilen des Terminplans und der Nachbartermine wie im Original; für die Ansichten und den Ausdruck.</summary>
internal static class Terminzeilen
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Original <c>PaintTerminPlan</c>: alle Spiele, Leerzeile bei neuer Kalenderwoche.</summary>
    /// <param name="spiele">Die Spiele in Terminreihenfolge.</param>
    /// <returns>Die Zeilen.</returns>
    internal static List<Terminzeile> Spielplan(IReadOnlyList<Terminplanzeile> spiele)
    {
        var ergebnis = new List<Terminzeile>();
        int letzteWoche = -1;
        foreach (Terminplanzeile z in spiele)
        {
            if (z.Kalenderwoche != letzteWoche && letzteWoche >= 0)
            {
                ergebnis.Add(Terminzeile.Leer);
            }

            letzteWoche = z.Kalenderwoche;
            ergebnis.Add(Spielzeile(z));
        }

        return ergebnis;
    }

    /// <summary>Original <c>PaintMannschaftsPlaeneIntern</c>: je Mannschaft ihre Spiele, Leerzeile bei neuer Runde.</summary>
    /// <param name="spiele">Die Spiele in Terminreihenfolge.</param>
    /// <param name="staffel">Die Staffel (Reihenfolge der Mannschaften).</param>
    /// <param name="mitNachbarn">Spiele der Vereinsmannschaften am selben Tag mit anzeigen.</param>
    /// <returns>Die Zeilen.</returns>
    internal static List<Terminzeile> Mannschaftsplaene(IReadOnlyList<Terminplanzeile> spiele, Staffel staffel, bool mitNachbarn)
    {
        var ergebnis = new List<Terminzeile>();
        foreach (string name in staffel.Mannschaften.Select(m => m.Name))
        {
            ergebnis.Add(new Terminzeile(Planzeilenart.Ueberschrift, string.Empty, string.Empty, name, string.Empty, string.Empty));
            int letzteRunde = 0;
            foreach (Terminplanzeile z in spiele.Where(z => z.Heim == name || z.Gast == name))
            {
                if (z.Runde > letzteRunde)
                {
                    ergebnis.Add(Terminzeile.Leer);
                }

                letzteRunde = z.Runde;
                ergebnis.Add(Spielzeile(z));
                if (mitNachbarn)
                {
                    ergebnis.AddRange(z.NachbartermineHeim.Concat(z.NachbartermineGast)
                        .Where(n => Terminplanauswertung.GleicherVerein(n.Heim, name) || Terminplanauswertung.GleicherVerein(n.Gast, name))
                        .Select(Nachbarzeile));
                }
            }

            ergebnis.Add(Terminzeile.Leer);
        }

        return ergebnis;
    }

    /// <summary>Tab „Termine der Nachbarmannschaften“: je Mannschaft die Spiele der Nachbarmannschaften nach Tagen.</summary>
    /// <param name="mannschaften">Die Nachbartermine je Mannschaft.</param>
    /// <returns>Die Zeilen.</returns>
    internal static List<Terminzeile> Nachbartermine(IReadOnlyList<MannschaftsNachbartermine> mannschaften)
    {
        var liste = new List<Terminzeile>();
        foreach (MannschaftsNachbartermine m in mannschaften)
        {
            liste.Add(new Terminzeile(Planzeilenart.Ueberschrift, string.Empty, string.Empty, m.Name + ":", string.Empty, string.Empty));
            foreach (IReadOnlyList<Nachbartermin> tag in m.Tage)
            {
                liste.Add(Terminzeile.Leer);
                liste.AddRange(tag.Select((n, i) => Nachbartag(n, ersteDesTages: i == 0)));
            }

            liste.Add(Terminzeile.Leer);
        }

        return liste;
    }

    private static Terminzeile Spielzeile(Terminplanzeile z) => new(
        Planzeilenart.Spiel,
        z.Zeitpunkt is DateTime t ? t.ToString("ddd dd.MM.yyyy", Deutsch) : "------",
        z.Zeitpunkt is DateTime u ? u.ToString("HH:mm", Deutsch) : string.Empty,
        z.Heim,
        z.Gast,
        z.Hinweis);

    private static Terminzeile Nachbarzeile(Nachbartermin n) => new(
        n.EchteNachbarmannschaft ? Planzeilenart.EchtesNachbarspiel : Planzeilenart.Nachbarspiel,
        string.Empty,
        n.Zeitpunkt.ToString("HH:mm", Deutsch),
        $"({n.Geschlecht}) {n.Heim}",
        n.Gast,
        Lokal(n));

    private static Terminzeile Nachbartag(Nachbartermin n, bool ersteDesTages) => new(
        n.EchteNachbarmannschaft ? Planzeilenart.EchtesNachbarspiel : Planzeilenart.Spiel,
        ersteDesTages ? n.Zeitpunkt.ToString("ddd dd.MM.yyyy", Deutsch) : string.Empty,
        n.Zeitpunkt.ToString("HH:mm", Deutsch),
        $"({n.Geschlecht}) {n.Heim}",
        n.Gast,
        Lokal(n));

    private static string Lokal(Nachbartermin n) => n.Spiellokal.Length > 0 ? "Spiellokal: " + n.Spiellokal : string.Empty;
}
