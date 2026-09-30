// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Gemeinsam;

namespace AndiGenerator.Persistence.Plandaten;

/// <summary>
/// Gemerkter Plan („Plan merken“ im Hauptfenster, Original <c>TPlan.SaveScheduleToXml</c>/<c>LoadScheduleFromXml</c>):
/// <c>&lt;andigenerator&gt;&lt;plan&gt;&lt;schedule&gt;&lt;game datetime hometeamname guestteamname/&gt;…</c>
/// im Staffelordner (<see cref="Staffelordner"/>), eine Datei je Plan, Dateiname = kodierter Planname + <c>.xml</c>.
/// Spiellokale werden nicht gespeichert.
/// </summary>
public static class GespeicherterPlanDatei
{
    /// <summary>Erzeugt den Datenbaum; die Spiele werden wie im Original nach Termin und „Heim Gast“ sortiert.</summary>
    /// <param name="spiele">Spiele des Plans.</param>
    /// <returns>Der Wurzelknoten <c>plan</c> mit dem Knoten <c>schedule</c>.</returns>
    public static DatenKnoten Erzeugen(IEnumerable<GespeichertesSpiel> spiele)
    {
        ArgumentNullException.ThrowIfNull(spiele);
        var sortiert = spiele.ToList();

        // Original: TSchedule.SortByDate – Termin aufsteigend (nicht terminiert = 0 = zuerst),
        // bei gleichem Termin ordinaler Vergleich von "Heim Gast" (Delphi CompareStr).
        sortiert.Sort(static (a, b) =>
        {
            int vergleich = (a.Zeitpunkt ?? DelphiKompatibel.DelphiNull).CompareTo(b.Zeitpunkt ?? DelphiKompatibel.DelphiNull);
            return vergleich != 0 ? vergleich : string.CompareOrdinal(a.Heim + " " + a.Gast, b.Heim + " " + b.Gast);
        });

        var plan = new DatenKnoten("plan");
        var schedule = new DatenKnoten("schedule", plan);
        foreach (GespeichertesSpiel spiel in sortiert)
        {
            var game = new DatenKnoten("game", schedule);
            game.Setzen("datetime", DelphiKompatibel.DatumZeitSchreiben(spiel.Zeitpunkt));
            game.Setzen("hometeamname", spiel.Heim);
            game.Setzen("guestteamname", spiel.Gast);
        }

        return plan;
    }

    /// <summary>Erzeugt den Dateiinhalt (UTF-8 ohne BOM, wie <see cref="PlanDatenDatei"/>).</summary>
    /// <param name="spiele">Spiele des Plans.</param>
    /// <returns>Der Dateiinhalt.</returns>
    public static byte[] ErzeugenBytes(IEnumerable<GespeichertesSpiel> spiele) => PlanDatenDatei.ErzeugenBytes(Erzeugen(spiele));

    /// <summary>Schreibt die Datei (erst temporär, dann ersetzen).</summary>
    /// <param name="pfad">Pfad der Datei.</param>
    /// <param name="spiele">Spiele des Plans.</param>
    public static void Speichern(string pfad, IEnumerable<GespeichertesSpiel> spiele)
    {
        byte[] inhalt = ErzeugenBytes(spiele);
        string temp = pfad + ".tmp";
        File.WriteAllBytes(temp, inhalt);
        File.Move(temp, pfad, overwrite: true);
    }

    /// <summary>Lädt die Spiele eines gemerkten Plans.</summary>
    /// <param name="pfad">Pfad der Datei.</param>
    /// <returns>Die Spiele in Dateireihenfolge.</returns>
    /// <exception cref="PlanDatenFormatException">Die Datei ist ungültig; das Original markiert den Plan dann als ungültig.</exception>
    public static IReadOnlyList<GespeichertesSpiel> Laden(string pfad)
    {
        using FileStream strom = File.OpenRead(pfad);
        return Laden(strom);
    }

    /// <inheritdoc cref="Laden(string)"/>
    public static IReadOnlyList<GespeichertesSpiel> Laden(Stream strom) => AusKnoten(PlanDatenDatei.Laden(strom));

    /// <summary>
    /// Liest die Spiele aus dem <c>schedule</c>-Knoten. Ungültige Termine lösen wie im Original einen Fehler aus.
    /// Spiele mit unbekannten Mannschaften verwirft erst die Zuordnung zum Plan (Original <c>LoadGameDates</c>).
    /// </summary>
    /// <param name="plan">Wurzelknoten <c>plan</c>.</param>
    /// <returns>Die Spiele in Dateireihenfolge; leer, wenn es keinen <c>schedule</c>-Knoten gibt.</returns>
    /// <exception cref="PlanDatenFormatException">Ein Termin ist ungültig.</exception>
    public static IReadOnlyList<GespeichertesSpiel> AusKnoten(DatenKnoten plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        DatenKnoten? schedule = plan.ErstesKind("schedule");
        if (schedule is null)
        {
            return [];
        }

        var spiele = new List<GespeichertesSpiel>();
        foreach (DatenKnoten game in schedule.KinderMitNamen("game"))
        {
            DateTime? zeitpunkt;
            try
            {
                zeitpunkt = DelphiKompatibel.DatumZeitStreng(game.Lesen("datetime"));
            }
            catch (FormatException ex)
            {
                throw new PlanDatenFormatException($"Ungültiger Termin '{game.Lesen("datetime")}'.", ex);
            }

            spiele.Add(new GespeichertesSpiel(zeitpunkt, game.Lesen("hometeamname"), game.Lesen("guestteamname")));
        }

        return spiele;
    }
}
