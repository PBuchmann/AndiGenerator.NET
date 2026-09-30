// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Referenz;

/// <summary>
/// Inhalt des Tabs „Terminplan“ im Original (<c>PaintSpielplan</c>, <c>PaintMannschaftsPlaene</c>, <c>PaintOneTermin</c>):
/// alle notwendigen Spiele nach Termin sortiert, mit Hinweisen je Spiel und den Spielen der Nachbarmannschaften am selben Tag.
/// </summary>
public static class Terminplanauswertung
{
    /// <summary>Ermittelt den Terminplan des bestehenden Spielplans der Staffel.</summary>
    /// <param name="staffel">Staffel mit dem Plan als bestehendem Spielplan.</param>
    /// <param name="optionen">Berechnungsoptionen.</param>
    /// <returns>Die Spiele in der Reihenfolge des Originals (ohne Termin zuerst, dann nach Termin, Heim und Gast).</returns>
    public static IReadOnlyList<Terminplanzeile> Zeilen(Staffel staffel, Berechnungsoptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        RefPlan plan = RefPlan.Laden(staffel, optionen);
        List<RefSpiel> spiele = plan.SpieleErlaubt.Concat(plan.SpieleNichtErlaubt).Where(s => !s.NichtNotwendig).ToList();
        RefPlan.StabilSortieren(spiele, Vergleichen);
        return spiele.Select(s => Zeile(plan, s)).ToList();
    }

    /// <summary>Original <c>RemoveTeamNumber</c>: Name ohne angehängte römische Mannschaftsnummer (I … XXX).</summary>
    /// <param name="mannschaft">Mannschaftsname, z. B. <c>TTC Beispiel III</c>.</param>
    /// <returns>Vereinsname, z. B. <c>TTC Beispiel</c>.</returns>
    public static string OhneMannschaftsnummer(string mannschaft)
    {
        ArgumentNullException.ThrowIfNull(mannschaft);
        for (int i = 1; i <= 30; i++)
        {
            string endung = " " + Roemisch(i);
            if (mannschaft.EndsWith(endung, StringComparison.Ordinal))
            {
                return mannschaft[..^endung.Length].TrimEnd();
            }
        }

        return mannschaft.TrimEnd();
    }

    /// <summary>Original <c>MannschaftMatchVereinStr</c>: beide Mannschaften gehören zum selben Verein.</summary>
    /// <param name="a">Erste Mannschaft.</param>
    /// <param name="b">Zweite Mannschaft.</param>
    /// <returns><c>true</c> bei gleichem Vereinsnamen.</returns>
    public static bool GleicherVerein(string a, string b) => OhneMannschaftsnummer(a) == OhneMannschaftsnummer(b);

    /// <summary>Original <c>compGameListByDate</c>: Termin, dann „Heim Gast“ (ordinal).</summary>
    private static int Vergleichen(RefSpiel a, RefSpiel b)
    {
        int ergebnis = a.Datum.CompareTo(b.Datum);
        return ergebnis != 0
            ? ergebnis
            : string.CompareOrdinal(a.Heim.TeamName + " " + a.Gast.TeamName, b.Heim.TeamName + " " + b.Gast.TeamName);
    }

    private static Terminplanzeile Zeile(RefPlan plan, RefSpiel spiel)
    {
        DateTime? zeitpunkt = spiel.DatumGueltig ? DelphiDatum.ZuDateTime(spiel.Datum) : null;
        return new Terminplanzeile(
            zeitpunkt,
            spiel.Heim.TeamName,
            spiel.Gast.TeamName,
            spiel.DatumGueltig ? plan.RundeZuDatum(spiel.Datum) : -1,
            zeitpunkt is DateTime z ? ISOWeek.GetWeekOfYear(z) : 0,
            string.Join(", ", Hinweise(plan, spiel)),
            Nachbartermine(plan, spiel, spiel.Heim),
            Nachbartermine(plan, spiel, spiel.Gast));
    }

    /// <summary>Hinweise wie im Original <c>PaintOneTermin</c>.</summary>
    private static List<string> Hinweise(RefPlan plan, RefSpiel spiel)
    {
        var hinweise = new List<string>();
        if (plan.IstSpielfreierTag(spiel.Datum))
        {
            hinweise.Add("spielfreier Tag");
        }

        if (!spiel.DatumGueltig)
        {
            return hinweise;
        }

        RefSpiel? vorgegeben = plan.VorgegebeneSpiele.Find(v => v.IstGleich(spiel));
        if (vorgegeben is not null)
        {
            hinweise.Add("manuell festgelegter Termin");
        }

        if (Gekoppelt(spiel.Heim, spiel))
        {
            hinweise.Add("Heimkoppelspiel/Doppelspieltag");
        }

        if (Gekoppelt(spiel.Gast, spiel))
        {
            hinweise.Add("Auswärtskoppelspiel");
        }

        RefWunschtermin? wunsch = spiel.Heim.Wunschtermine.Find(w => w.Datum == spiel.Datum);
        if (wunsch is not null)
        {
            if (spiel.Heim.IstAusweichtermin(wunsch.Datum))
            {
                hinweise.Add("Ausweichtermin");
            }

            if (Terminoptionen.Hat(wunsch.Optionen, Wunschterminoptionen.AuswaertsKoppelZweitzeit)
                || Terminoptionen.Hat(wunsch.Optionen, Wunschterminoptionen.KoppelZweitzeit))
            {
                hinweise.Add("wegen Koppeltermin geänderte Uhrzeit");
            }

            if (vorgegeben is null && wunsch.Spiellokal.Length > 0)
            {
                hinweise.Add("Spiellokal: " + wunsch.Spiellokal);
            }
        }

        if (vorgegeben is not null && vorgegeben.UeberschriebenesLokal.Length > 0)
        {
            hinweise.Add("Spiellokal: " + vorgegeben.UeberschriebenesLokal);
        }

        if (wunsch is null)
        {
            hinweise.Add("kein Wunschtermin");
        }

        if (plan.SpieleNichtErlaubt.Exists(s => s.IstGleich(spiel)))
        {
            hinweise.Add("doppeltes Spiel");
        }

        if (wunsch is not null && plan.SpieleErlaubt.Find(s => s.IstGleich(spiel)) is RefSpiel erlaubt && !plan.SpielIstGueltig(erlaubt))
        {
            hinweise.Add("ungültiger Termin");
        }

        return hinweise;
    }

    /// <summary>Original <c>getKoppeledGame(Game)</c> über die Spielliste der Mannschaft.</summary>
    private static bool Gekoppelt(RefMannschaft mannschaft, RefSpiel spiel)
    {
        int index = mannschaft.Spiele.FindIndex(s => s.IstGleich(spiel));
        return index >= 0 && mannschaft.GekoppeltesSpiel(index) is not null;
    }

    /// <summary>Original <c>PaintOneTerminSisterGames</c> für eine Mannschaft (ohne Filter nach Verein, den macht die Anzeige).</summary>
    private static List<Nachbartermin> Nachbartermine(RefPlan plan, RefSpiel spiel, RefMannschaft mannschaft)
    {
        var ergebnis = new List<Nachbartermin>();
        if (!spiel.DatumGueltig)
        {
            return ergebnis;
        }

        int tag = DelphiDatum.Trunc(spiel.Datum);
        if (mannschaft.Nachbarspiele.TryGetValue(tag, out List<RefNachbarspiel>? liste))
        {
            ergebnis.AddRange(liste
                .OrderBy(n => n.Datum)
                .ThenBy(n => n.Mannschaftsname, StringComparer.Ordinal)
                .Select(n => new Nachbartermin(DelphiDatum.ZuDateTime(n.Datum), n.Geschlecht, n.Heim, n.Gast, n.Lokal, n.KeineParallelenSpiele)));
        }

        foreach (RefMannschaft verein in mannschaft.VereinsteamsImPlan)
        {
            ergebnis.AddRange(verein.Spiele
                .Where(s => s.DatumGueltig && DelphiDatum.Trunc(s.Datum) == tag
                    && !ReferenceEquals(s.Heim, mannschaft) && !ReferenceEquals(s.Gast, mannschaft))
                .Select(s => new Nachbartermin(
                    DelphiDatum.ZuDateTime(s.Datum),
                    plan.Geschlecht,
                    s.Heim.TeamName,
                    s.Gast.TeamName,
                    s.Lokal,
                    Math.Abs(mannschaft.TeamNummer - verein.TeamNummer) == 1)));
        }

        return ergebnis;
    }

    /// <summary>Delphi <c>DecToRom</c> für 1 … 30.</summary>
    private static string Roemisch(int zahl)
    {
        string[] zehner = [string.Empty, "X", "XX", "XXX"];
        string[] einer = [string.Empty, "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX"];
        return zehner[zahl / 10] + einer[zahl % 10];
    }
}
