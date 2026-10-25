// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Persistence.Plandaten;
using AndiGenerator.Persistence.Terminwuensche;

namespace AndiGenerator.Application;

/// <summary>
/// Meldungen der Kompaktspieltage direkt im Raster erfassen (nicht im Original; Peter, 13.10.2026). Gespeichert wird wie
/// bisher: Heimtermin = Heim möglich, Sperrtermin = kann nicht, ohne Eintrag = auswärts; „nur Heim“ steht als Zeile
/// „Nur Heim: TT.MM., …“ in der Bemerkung der Mannschaft.
/// </summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>Attribut am Knoten <c>plan</c>: die im Raster angelegten Tage, z. B. <c>12.09.2026;03.10.2026</c>.</summary>
    private const string KompakttageAttribut = "kompakttage";

    /// <summary>Liest die im Raster angelegten Tage (sie erscheinen als Zeilen, auch wenn dort niemand einen Heimtermin hat).</summary>
    /// <returns>Die Tage, aufsteigend.</returns>
    public IReadOnlyList<DateOnly> Kompakttage() =>
        Arbeitsstand.Lesen(KompakttageAttribut)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => DateOnly.TryParseExact(t, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly d) ? d : (DateOnly?)null)
            .OfType<DateOnly>()
            .Distinct()
            .Order()
            .ToList();

    /// <summary>Setzt die Meldung einer Mannschaft an einem Tag.</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="tag">Der Tag.</param>
    /// <param name="feld">Die Meldung.</param>
    public void KompaktfeldSetzen(string mannschaft, DateOnly tag, Kompaktfeld feld)
    {
        List<Heimtag> tage = Heimtage(mannschaft).ToList();
        bool heim = feld is Kompaktfeld.Heim or Kompaktfeld.Beides;
        bool hatHeimtermin = tage.Exists(h => h.IstHeimtag && Am(h, tag));
        tage.RemoveAll(h => Am(h, tag) && (h.Sperrtermin || !heim));
        if (feld == Kompaktfeld.Leer)
        {
            tage.Add(new Heimtag(tag.ToDateTime(TimeOnly.MinValue), true, false, false, 0, false, TimeOnly.MinValue, 0, false, string.Empty));
        }
        else if (heim && !hatHeimtermin)
        {
            tage.Add(NeuerHeimtermin(mannschaft, tag, tage));
        }

        HeimtageSpeichern(mannschaft, tage.OrderBy(h => h.Datum));
        DatenKnoten team = Team(mannschaft);
        string bemerkung = team.Lesen(BemerkungAttribut);
        string neu = Kompaktplanung.NurHeimAendern(bemerkung, tag, feld == Kompaktfeld.Heim);
        if (neu != bemerkung)
        {
            team.Setzen(BemerkungAttribut, neu);
        }
    }

    /// <summary>Liest den Heimtermin einer Mannschaft an einem Tag.</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="tag">Der Tag.</param>
    /// <returns>Der Termin oder <c>null</c>, wenn sie an dem Tag keinen Heimtermin hat.</returns>
    public Kompakttermin? KompaktterminLesen(string mannschaft, DateOnly tag)
    {
        if (Heimtage(mannschaft).FirstOrDefault(h => h.IstHeimtag && Am(h, tag)) is not Heimtag h)
        {
            return null;
        }

        TimeOnly zeit = TimeOnly.FromDateTime(h.Datum);
        return new Kompakttermin(zeit == TimeOnly.MinValue ? null : zeit, h.Spiellokal, h.MaxParallel >= 2);
    }

    /// <summary>Ändert Beginn, Spiellokal und Tische des Heimtermins einer Mannschaft an einem Tag.</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="tag">Der Tag.</param>
    /// <param name="termin">Die neuen Angaben.</param>
    public void KompaktterminSetzen(string mannschaft, DateOnly tag, Kompakttermin termin)
    {
        ArgumentNullException.ThrowIfNull(termin);
        List<Heimtag> tage = Heimtage(mannschaft).ToList();
        int i = tage.FindIndex(h => h.IstHeimtag && Am(h, tag));
        if (i < 0)
        {
            return;
        }

        tage[i] = tage[i] with
        {
            Datum = tag.ToDateTime(termin.Zeit ?? TimeOnly.MinValue),
            Spiellokal = Spiellokale.Korrigieren(termin.Spiellokal),
            MaxParallel = termin.ZweiTische ? 2 : 1,
        };
        HeimtageSpeichern(mannschaft, tage.OrderBy(h => h.Datum));
    }

    /// <summary>
    /// Legt einen weiteren Tag im Raster an (gespeichert mit der Staffel): Alle Mannschaften ohne Eintrag an diesem Tag bekommen
    /// einen Sperrtermin (leeres Feld); Felder füllt man danach einzeln.
    /// </summary>
    /// <param name="tag">Der Tag.</param>
    public void KompakttagAnlegen(DateOnly tag)
    {
        Arbeitsstand.Setzen(
            KompakttageAttribut,
            string.Join(';', Kompakttage().Append(tag).Distinct().Order().Select(t => t.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture))));
        List<string> ohneEintrag = Teams(Arbeitsstand)
            .Select(t => t.Lesen("teamname"))
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Where(n => !Heimtage(n).Any(h => Am(h, tag)))
            .ToList();
        foreach (string name in ohneEintrag)
        {
            KompaktfeldSetzen(name, tag, Kompaktfeld.Leer);
        }
    }

    /// <summary>
    /// Die Spiellokale, die für eine Mannschaft in Frage kommen: ihr Standardlokal, alle ihrer Heimtermine und alle mit
    /// Adresse aus den Terminwünschen, jeweils mit dem Namen der Halle.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Die Spiellokale nach Nummer; mindestens „1“.</returns>
    public IReadOnlyList<Kompaktlokal> KompaktLokale(string mannschaft)
    {
        DatenKnoten team = Team(mannschaft);
        var lokale = new SortedSet<string>(StringComparer.Ordinal) { "1" };
        lokale.UnionWith(Enumerable.Range(1, 5).Select(n => n.ToString(CultureInfo.InvariantCulture)).Where(n => team.Lesen(AdressAttribut + n).Length > 0));
        string standardlokal = Spiellokale.Korrigieren(Team(mannschaft).Lesen("location"));
        if (standardlokal.Length > 0)
        {
            lokale.Add(standardlokal);
        }

        lokale.UnionWith(Heimtage(mannschaft).Where(h => h.IstHeimtag && h.Spiellokal.Length > 0).Select(h => h.Spiellokal));
        return lokale.Select(n => new Kompaktlokal(n, Hallenname(mannschaft, n))).ToList();
    }

    /// <summary>Name und Adresse einer Halle aus den Terminwünschen.</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="lokal">Nummer des Spiellokals; leer = Standardlokal der Mannschaft.</param>
    /// <returns>Der Text oder leer, wenn unbekannt.</returns>
    public string Hallenname(string mannschaft, string lokal)
    {
        DatenKnoten? team = Teams(Arbeitsstand).FirstOrDefault(t => t.Lesen("teamname") == mannschaft);
        if (team is null)
        {
            return string.Empty;
        }

        string nummer = Spiellokale.Korrigieren(lokal.Length > 0 ? lokal : team.Lesen("location"));
        string text = team.Lesen(AdressAttribut + (nummer.Length > 0 ? nummer : "1"));
        return text.Length == 0 ? string.Empty : Spiellokaladresse.Aus(text).Text;
    }

    /// <summary>
    /// Die Standardhalle einer Mannschaft: ihr Standardlokal, sonst das ihrer meisten Heimtermine, sonst „1“ – so bekommen
    /// auch neue Heimtermine ihre Halle.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Die Nummer des Spiellokals.</returns>
    public string KompaktStandardlokal(string mannschaft)
    {
        string lokal = NeuerHeimtermin(mannschaft, DateOnly.MinValue, Heimtage(mannschaft).ToList()).Spiellokal;
        return lokal.Length > 0 ? lokal : "1";
    }

    /// <summary>
    /// Setzt ein Spiellokal für die ganze Mannschaft: als ihr Standardlokal und für alle ihre Heimtermine.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="lokal">Nummer des Spiellokals.</param>
    public void KompaktLokalFuerAlle(string mannschaft, string lokal)
    {
        string nummer = Spiellokale.Korrigieren(lokal);
        Team(mannschaft).Setzen("location", nummer);
        HeimtageSpeichern(mannschaft, Heimtage(mannschaft).Select(h => h.IstHeimtag ? h with { Spiellokal = nummer } : h).ToList());
    }

    private static bool Am(Heimtag h, DateOnly tag) => DateOnly.FromDateTime(h.Datum) == tag;

    /// <summary>
    /// Ein neuer Heimtermin ohne Uhrzeit (00:00 = Standardbeginn: 3er-Spieltag 10:00, 4er-Spieltag 11:00; Peter, 13.10.2026),
    /// im Spiellokal der Mannschaft (sonst dem ihrer meisten Heimtermine), zwei Tische (4er-Spieltag möglich).
    /// </summary>
    private Heimtag NeuerHeimtermin(string mannschaft, DateOnly tag, List<Heimtag> tage)
    {
        string lokal = Spiellokale.Korrigieren(Team(mannschaft).Lesen("location"));
        if (lokal.Length == 0)
        {
            lokal = tage.Where(h => h.IstHeimtag).GroupBy(h => h.Spiellokal).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? string.Empty;
        }

        return new Heimtag(tag.ToDateTime(TimeOnly.MinValue), false, false, false, 0, false, TimeOnly.MinValue, 2, false, lokal);
    }
}
