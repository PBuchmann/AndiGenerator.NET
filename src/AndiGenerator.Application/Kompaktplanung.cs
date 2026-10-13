// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text.RegularExpressions;
using AndiGenerator.Domain.Geo;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Domain.Turniere;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Application;

/// <summary>
/// Kompaktspieltage (nicht im Original): Meldungen aus den Terminwünschen der XML-Datei und Auswertung des Plans aus
/// click-TT. Sonntag mit Heimtermin = Heimspieltag möglich, ohne Eintrag = auswärts möglich, Sperrtermin = kann nicht;
/// „Nur Heim“ steht als Zeile „Nur Heim: TT.MM., …“ in der Bemerkung des Vereins. Entfernungen in Luftlinie.
/// </summary>
public static partial class Kompaktplanung
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Wertet den Plan aus click-TT einer Staffel mit Kompaktspieltagen aus.</summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="daten">Arbeitskopie der Spielplandaten (Bemerkungen und Hallenadressen).</param>
    /// <param name="planung">Eingestellte Rundenplanung (wählt mit dem Klassennamen die Halbrunde).</param>
    /// <param name="heute">Heutiges Datum (wählt sonst ohne Plan die Halbrunde).</param>
    /// <returns>Die Auswertung.</returns>
    public static Kompaktauswertung Auswerten(Staffel staffel, Datenbearbeitung daten, Rundenplanung planung, DateOnly heute)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        return AuswertenFuer(staffel, daten, planung, heute, Turniererkennung.Erkennen(staffel.BestehenderSpielplan));
    }

    /// <summary>Wertet einen Plan des Kompaktplaners aus (Ausrichter wie geplant).</summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="daten">Arbeitskopie der Spielplandaten (Bemerkungen und Hallenadressen).</param>
    /// <param name="planung">Eingestellte Rundenplanung.</param>
    /// <param name="heute">Heutiges Datum.</param>
    /// <param name="plan">Die geplanten Spieltage.</param>
    /// <returns>Die Auswertung.</returns>
    public static Kompaktauswertung Auswerten(Staffel staffel, Datenbearbeitung daten, Rundenplanung planung, DateOnly heute, IReadOnlyList<Planturnier> plan)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(plan);
        List<Turnier> turniere = plan
            .Select(p => new Turnier(p.Tag, p.Form, p.Mannschaften, Spiele(p, staffel), [p.Ausrichter]))
            .OrderBy(t => t.Tag)
            .ToList();
        return AuswertenFuer(staffel, daten, planung, heute, turniere);
    }

    /// <summary>
    /// Wertet einen Plan aus seinen Spielen aus, z. B. den gesicherten Plan: Die Spieltage werden erkannt; kommen beim
    /// 4er-Spieltag zwei Mannschaften als Ausrichter in Frage, ist es die, deren Spiele ein Spiellokal haben.
    /// </summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="daten">Arbeitskopie der Spielplandaten (Bemerkungen und Hallenadressen).</param>
    /// <param name="planung">Eingestellte Rundenplanung.</param>
    /// <param name="heute">Heutiges Datum.</param>
    /// <param name="spiele">Die Spiele des Plans.</param>
    /// <returns>Die Auswertung.</returns>
    public static Kompaktauswertung SpieleAuswerten(Staffel staffel, Datenbearbeitung daten, Rundenplanung planung, DateOnly heute, IEnumerable<Spiel> spiele)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(spiele);
        List<Turnier> turniere = Turniererkennung.Erkennen(spiele).Select(AusrichterMitLokal).ToList();
        return AuswertenFuer(staffel, daten, planung, heute, turniere);
    }

    /// <summary>
    /// Ob die Frage nach Kompaktspieltagen kommt: Die Klasse spielt nur eine Halbrunde („(HR)“ bzw. „(FR)“ im Namen oder
    /// Rundenplanung „Halbrunde“) und hat überwiegend Spieltage am Wochenende.
    /// </summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="planung">Eingestellte Rundenplanung.</param>
    /// <param name="halbrundeVorgeschlagen">Ob beim Öffnen eine Halbrunde vorgeschlagen wird (Runde kürzer als 6 Monate).</param>
    /// <returns><c>true</c>, wenn nach Kompaktspieltagen gefragt wird.</returns>
    public static bool InFrage(Staffel staffel, Rundenplanung planung, bool halbrundeVorgeschlagen)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        bool halbrunde = halbrundeVorgeschlagen
            || planung == Rundenplanung.Halbrunde
            || Halbrundenname.Kennzeichen(staffel.Name) != Halbrundenkennzeichen.Keins;
        return halbrunde && Vermutet(staffel);
    }

    /// <summary>
    /// Vermutet Kompaktspieltage aus den Daten: Im Plan aus click-TT spielen Mannschaften zweimal am selben Tag (3er- oder
    /// 4er-Spieltage), oder die Termine (Terminwünsche und Plan) liegen überwiegend am Wochenende – insgesamt oder bei den
    /// meisten Mannschaften. Zwei oder drei Termine unter der Woche für einzelne Spiele ändern daran nichts; ohne Termine
    /// wird nicht widersprochen.
    /// </summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <returns><c>true</c>, wenn die Daten nach Kompaktspieltagen aussehen.</returns>
    public static bool Vermutet(Staffel staffel)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        if (Turniererkennung.Erkennen(staffel.BestehenderSpielplan).Any(t => t.Form is Turnierform.Dreier or Turnierform.Vierer))
        {
            return true;
        }

        HashSet<DateOnly> tage = staffel.Mannschaften
            .SelectMany(m => m.Heimspieltermine)
            .Select(t => DateOnly.FromDateTime(t.Zeitpunkt))
            .Concat(staffel.BestehenderSpielplan.Where(s => s.Zeitpunkt.HasValue).Select(s => DateOnly.FromDateTime(s.Zeitpunkt!.Value)))
            .ToHashSet();
        int wochenende = tage.Count(t => t.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        if (WochenendeUeberwiegt(wochenende, tage.Count - wochenende))
        {
            return true;
        }

        // Je Mannschaft gezählt (Peter, 13.10.2026): Eine Mannschaft spielt am Wochenende, wenn höchstens zwei ihrer
        // Heimtermine unter der Woche liegen (für Einzelbegegnungen) und der Rest am Wochenende – oder das Wochenende
        // ohnehin überwiegt. Die Klasse zählt, wenn das bei den meisten Mannschaften so ist.
        List<bool> mannschaftAmWochenende = staffel.Mannschaften
            .Select(m => m.Heimspieltermine.Select(t => DateOnly.FromDateTime(t.Zeitpunkt)).Distinct().ToList())
            .Where(d => d.Count > 0)
            .Select(d => (Wochenende: d.Count(t => t.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday), Werktage: d.Count(t => t.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))))
            .Select(z => (z.Werktage <= 2 && z.Wochenende > 0) || WochenendeUeberwiegt(z.Wochenende, z.Werktage))
            .ToList();
        int amWochenende = mannschaftAmWochenende.Count(w => w);
        return mannschaftAmWochenende.Count > 0 && WochenendeUeberwiegt(amWochenende, mannschaftAmWochenende.Count - amWochenende);
    }

    /// <summary>Überwiegend Wochenende: mindestens doppelt so viele Tage wie unter der Woche – bis zu drei Werktage stören nicht.</summary>
    /// <param name="wochenende">Anzahl der Tage am Wochenende.</param>
    /// <param name="werktage">Anzahl der Tage unter der Woche.</param>
    /// <returns><c>true</c>, wenn das Wochenende überwiegt oder es keine Termine gibt.</returns>
    public static bool WochenendeUeberwiegt(int wochenende, int werktage) =>
        wochenende + werktage == 0 || wochenende >= 2 * werktage || (werktage <= 3 && wochenende > werktage);

    /// <summary>Die zu planende Halbrunde: aus der Rundenplanung oder dem Klassennamen; <c>null</c>, wenn offen.</summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="planung">Eingestellte Rundenplanung.</param>
    /// <returns><c>true</c> für die Rückrunde, <c>false</c> für die Hinrunde.</returns>
    public static bool? Rueckrunde(Staffel staffel, Rundenplanung planung)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        if (planung is Rundenplanung.NurVorrunde or Rundenplanung.NurRueckrunde or Rundenplanung.Corona)
        {
            return planung != Rundenplanung.NurVorrunde;
        }

        return Halbrundenname.Kennzeichen(staffel.Name) switch
        {
            Halbrundenkennzeichen.Hinrunde => false,
            Halbrundenkennzeichen.Rueckrunde => true,
            _ => null,
        };
    }

    /// <summary>Kurzer Name ohne Vereinskürzel und Zusatz nach „/“: „ESV Engers“ → „Engers“, „SG Sinzig/Ehlingen“ → „Sinzig“.</summary>
    /// <param name="name">Mannschaftsname.</param>
    /// <returns>Der kurze Name (oder der ganze, wenn nichts übrig bliebe).</returns>
    public static string Kurzname(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string ohneZusatz = name.Split('/')[0];
        List<string> woerter = ohneZusatz.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !(w.Length <= 4 && w.Count(char.IsUpper) >= 2) && !RoemischeZahl().IsMatch(w))
            .ToList();
        return woerter.Count == 0 ? name : string.Join(' ', woerter);
    }

    /// <summary>Sonntage aus der Zeile „Nur Heim: TT.MM., …“ der Bemerkung.</summary>
    /// <param name="bemerkung">Bemerkung des Vereins.</param>
    /// <param name="tage">Sonntage der Halbrunde (für Daten ohne Jahr).</param>
    /// <returns>Die Sonntage.</returns>
    public static IReadOnlyList<DateOnly> NurHeim(string bemerkung, IReadOnlyList<DateOnly> tage)
    {
        ArgumentNullException.ThrowIfNull(bemerkung);
        ArgumentNullException.ThrowIfNull(tage);
        var ergebnis = new List<DateOnly>();
        foreach (Match zeile in NurHeimZeile().Matches(bemerkung))
        {
            foreach (GroupCollection d in Datum().Matches(zeile.Groups[1].Value).Select(m => m.Groups))
            {
                int tag = int.Parse(d[1].Value, CultureInfo.InvariantCulture);
                int monat = int.Parse(d[2].Value, CultureInfo.InvariantCulture);
                int? jahr = d[3].Success ? Jahr(d[3].Value) : null;
                ergebnis.AddRange(tage.Where(t => t.Day == tag && t.Month == monat && (jahr is null || t.Year == jahr)));
            }
        }

        return ergebnis.Distinct().Order().ToList();
    }

    /// <summary>
    /// Nimmt einen Tag in die Zeile „Nur Heim: TT.MM., …“ der Bemerkung auf oder entfernt ihn (Erfassen im Raster). Die übrige
    /// Bemerkung bleibt, wie sie ist; ohne Tage entfällt die Zeile.
    /// </summary>
    /// <param name="bemerkung">Bisherige Bemerkung.</param>
    /// <param name="tag">Der Tag.</param>
    /// <param name="nurHeim"><c>true</c> = aufnehmen, <c>false</c> = entfernen.</param>
    /// <returns>Die neue Bemerkung.</returns>
    public static string NurHeimAendern(string bemerkung, DateOnly tag, bool nurHeim)
    {
        ArgumentNullException.ThrowIfNull(bemerkung);
        Match zeile = NurHeimZeile().Match(bemerkung);
        var daten = new SortedSet<(int Monat, int Tag)>();
        if (zeile.Success)
        {
            foreach (GroupCollection d in Datum().Matches(zeile.Groups[1].Value).Select(m => m.Groups))
            {
                daten.Add((int.Parse(d[2].Value, CultureInfo.InvariantCulture), int.Parse(d[1].Value, CultureInfo.InvariantCulture)));
            }
        }

        if (nurHeim)
        {
            daten.Add((tag.Month, tag.Day));
        }
        else
        {
            daten.Remove((tag.Month, tag.Day));
        }

        string neu = daten.Count == 0
            ? string.Empty
            : "Nur Heim: " + string.Join(", ", daten.Select(d => string.Create(CultureInfo.InvariantCulture, $"{d.Tag:00}.{d.Monat:00}.")));
        if (!zeile.Success)
        {
            return neu.Length == 0 ? bemerkung : string.Join('\n', new[] { bemerkung.TrimEnd(), neu }.Where(t => t.Length > 0));
        }

        // Zeilenumbruch hinter der Zeile erhalten (der Ausdruck schließt Leerraum hinter dem letzten Datum ein).
        string schluss = zeile.Value[zeile.Value.TrimEnd().Length..];
        string ergebnis = bemerkung[..zeile.Index] + neu + (neu.Length > 0 ? schluss : string.Empty) + bemerkung[(zeile.Index + zeile.Length)..];
        return neu.Length > 0 ? ergebnis : ergebnis.Replace("\n\n", "\n", StringComparison.Ordinal).Trim();
    }

    private static Kompaktauswertung AuswertenFuer(Staffel staffel, Datenbearbeitung daten, Rundenplanung planung, DateOnly heute, IReadOnlyList<Turnier> turniere)
    {
        ArgumentNullException.ThrowIfNull(daten);
        List<DateOnly> tage = Spieltermine(staffel, turniere, Rueckrunde(staffel, planung), heute, daten.Kompakttage());
        IReadOnlyDictionary<string, string> bemerkungen = daten.Bemerkungen();
        IReadOnlyDictionary<string, Koordinate> lagen = daten.Hallenkoordinaten();
        IReadOnlyDictionary<string, string> adressen = daten.Hallenadressen();
        var befunde = new List<string>();
        var meldungen = new List<Turniermeldung>();
        foreach (Mannschaft m in staffel.Mannschaften)
        {
            string bemerkung = bemerkungen.GetValueOrDefault(m.Name, string.Empty);
            if (Meldung(m, tage, bemerkung, befunde) is Turniermeldung meldung)
            {
                meldungen.Add(meldung);
            }
        }

        List<string> namen = staffel.Mannschaften.Select(m => m.Name).ToList();
        Turnierpruefungsergebnis pruefung = Turnierpruefung.Pruefen(turniere, namen, meldungen);

        // Ohne Plan (weder aus click-TT noch berechnet) fehlt jede Paarung – das muss nicht als Hinweis dastehen.
        if (turniere.Count > 0)
        {
            befunde.AddRange(pruefung.Befunde);
        }

        double Entfernung(string a, string b) =>
            lagen.TryGetValue(a, out Koordinate x) && lagen.TryGetValue(b, out Koordinate y) ? 2 * x.Luftlinie(y) : 0;

        List<Kompaktspieltag> spieltage = pruefung.Turniere.Select(b => Spieltag(b, Entfernung)).ToList();
        List<Kompaktmannschaft> mannschaften = staffel.Mannschaften.Select(m => new Kompaktmannschaft(
            m.Name,
            Kurzname(m.Name),
            lagen.TryGetValue(m.Name, out Koordinate lage) ? lage : null,
            adressen.GetValueOrDefault(m.Name) ?? m.Heimspieltermine.Select(t => t.Halle).FirstOrDefault(h => h.Length > 0) ?? string.Empty,
            meldungen.Find(x => x.Mannschaft == m.Name),
            bemerkungen.GetValueOrDefault(m.Name, string.Empty),
            spieltage.Where(s => s.Ausrichter is not null && s.Gaeste.Contains(m.Name)).Sum(s => Entfernung(m.Name, s.Ausrichter!)),
            pruefung.Ausgerichtet.GetValueOrDefault(m.Name))
        {
            EinzelHeim = spieltage.Count(s => s.Form == Turnierform.Einzel && s.Ausrichter == m.Name),
            Termine = m.Heimspieltermine
                .Where(h => tage.Contains(DateOnly.FromDateTime(h.Zeitpunkt)))
                .GroupBy(h => DateOnly.FromDateTime(h.Zeitpunkt))
                .ToDictionary(g => g.Key, g => Termintext(g.First(), daten, m.Name)),
        }).ToList();
        return new Kompaktauswertung(tage, mannschaften, spieltage, befunde, GroessterAbstand(spieltage, namen), MeisteAmTag(spieltage));
    }

    /// <summary>Halle und Beginn eines Heimtermins, z. B. „Halle: Sporthalle Nord, Hauptstr. 1, 56000 Ort · Beginn: Standard (3er 10:00, 4er 11:00)“.</summary>
    private static string Termintext(Heimspieltermin h, Datenbearbeitung daten, string mannschaft)
    {
        string halle = h.Halle.Length > 0 ? h.Halle : daten.Hallenname(mannschaft, h.Spiellokal);
        if (halle.Length == 0)
        {
            halle = h.Spiellokal.Length > 0 ? "Spiellokal " + h.Spiellokal : "Spiellokal der Mannschaft";
        }

        string beginn = h.Zeitpunkt.TimeOfDay > TimeSpan.Zero ? h.Zeitpunkt.ToString("HH:mm", Deutsch) + " Uhr" : "Standard (3er 10:00, 4er 11:00)";
        string tische = h.MaxParalleleSpiele != 1 ? string.Empty : " · nur ein Tisch (kein 4er-Spieltag)";
        return $"Halle: {halle} · Beginn: {beginn}{tische}";
    }

    /// <summary>
    /// Die möglichen Spieltermine der Halbrunde (die gewählte, sonst die mit Spielen aus click-TT bzw. – ohne Spiele – die
    /// laufende oder nächste): alle Sonntage, dazu Samstage und Wochentage mit einem Heimtermin und die im Raster angelegten Tage;
    /// ohne spielfreie Tage.
    /// </summary>
    private static List<DateOnly> Spieltermine(Staffel staffel, IReadOnlyList<Turnier> turniere, bool? rueckrunde, DateOnly heute, IReadOnlyList<DateOnly> angelegt)
    {
        DateOnly beginn = staffel.Beginn ?? turniere.Select(t => t.Tag).DefaultIfEmpty(heute).Min();
        DateOnly ende = staffel.Ende ?? turniere.Select(t => t.Tag).DefaultIfEmpty(heute).Max();
        if (staffel.Rueckrundenbeginn is DateOnly mitte)
        {
            DateOnly bezug = turniere.Count > 0 ? turniere[0].Tag : heute;
            bool zweite = rueckrunde ?? bezug >= mitte;
            (beginn, ende) = zweite ? (mitte, ende) : (beginn, mitte.AddDays(-1));
        }

        var tage = new SortedSet<DateOnly>();
        for (DateOnly t = beginn.AddDays(((int)DayOfWeek.Sunday - (int)beginn.DayOfWeek + 7) % 7); t <= ende; t = t.AddDays(7))
        {
            tage.Add(t);
        }

        // Dazu Samstage (Kompaktspieltage) und Wochentage (nur Einzelbegegnungen) mit einem Heimtermin einer Mannschaft
        // sowie die Tage des Plans aus click-TT (Peter, 13.10.2026).
        tage.UnionWith(staffel.Mannschaften
            .SelectMany(m => m.Heimspieltermine)
            .Select(h => DateOnly.FromDateTime(h.Zeitpunkt))
            .Concat(turniere.Select(t => t.Tag))
            .Where(t => t >= beginn && t <= ende));

        // Und die im Raster angelegten Zeilen (Peter, 13.10.2026).
        tage.UnionWith(angelegt.Where(t => t >= beginn && t <= ende));
        tage.ExceptWith(staffel.SpielfreieTage);
        return [.. tage];
    }

    private static Turniermeldung? Meldung(Mannschaft m, List<DateOnly> tage, string bemerkung, List<string> befunde)
    {
        var heim = m.Heimspieltermine.Select(t => DateOnly.FromDateTime(t.Zeitpunkt)).Where(tage.Contains).ToHashSet();
        var gesperrt = m.Sperrtermine.Where(tage.Contains).ToHashSet();
        if (heim.Count == 0 && gesperrt.Count == 0)
        {
            return null;
        }

        IReadOnlyList<DateOnly> nurHeim = NurHeim(bemerkung, tage);
        befunde.AddRange(nurHeim.Where(t => !heim.Contains(t))
            .Select(t => $"{m.Name}: „Nur Heim“ am {t.ToString("dd.MM.", Deutsch)}, aber an diesem Sonntag ist kein Heimtermin eingetragen."));
        var bereitschaft = new Dictionary<DateOnly, Turnierbereitschaft>();
        foreach (DateOnly tag in tage)
        {
            Turnierbereitschaft b = Turnierbereitschaft.None;
            if (!gesperrt.Contains(tag))
            {
                b = heim.Contains(tag) ? Turnierbereitschaft.Heim : Turnierbereitschaft.None;
                b |= Auswaerts(tag, nurHeim) ? Turnierbereitschaft.Auswaerts : Turnierbereitschaft.None;
            }

            bereitschaft[tag] = b;
        }

        string halle = m.Heimspieltermine.Select(t => t.Halle).FirstOrDefault(h => h.Length > 0) ?? string.Empty;
        return new Turniermeldung(m.Name, bereitschaft, halle, m.Heimspieltermine.Any(t => t.MaxParalleleSpiele >= 2), bemerkung);
    }

    /// <summary>
    /// Ob die Mannschaft an dem (nicht gesperrten) Tag auswärts spielen kann: wenn er nicht „Nur Heim“ ist. Das gilt an jedem
    /// Tag, auch samstags (Peter, 13.10.2026: im Raster frei erfassbar; bisher galt samstags der Sonntag desselben Wochenendes).
    /// </summary>
    private static bool Auswaerts(DateOnly tag, IReadOnlyList<DateOnly> nurHeim) => !nurHeim.Contains(tag);

    private static Turnier AusrichterMitLokal(Turnier t)
    {
        if (t.MoeglicheAusrichter.Count < 2)
        {
            return t;
        }

        List<string> mitLokal = t.MoeglicheAusrichter.Where(m => t.Spiele.Any(s => s.Heim == m && s.Spiellokal.Length > 0)).ToList();
        return mitLokal.Count == 1 ? t with { MoeglicheAusrichter = mitLokal } : t;
    }

    private static Kompaktspieltag Spieltag(Turnierbewertung b, Func<string, string, double> entfernung)
    {
        Turnier t = b.Turnier;
        IReadOnlyList<Spiel> verlegen = b.Ausrichter is string a && t.Form != Turnierform.Einzel
            ? t.Spiele.Where(s => s.Heim != a).ToList()
            : [];
        double km = b.Ausrichter is string ausrichter ? t.Mannschaften.Where(m => m != ausrichter).Sum(m => entfernung(m, ausrichter)) : 0;
        return new Kompaktspieltag(t.Tag, t.Form, b.Ausrichter, t.Mannschaften, t.Spiele, verlegen, b.Hinweise, b.Ausweichtage, km);
    }

    /// <summary>Nach jedem Spieltag: Unterschied der Zahl gespielter Spiele; der größte Wert der Halbrunde.</summary>
    private static int GroessterAbstand(List<Kompaktspieltag> spieltage, List<string> namen)
    {
        var gespielt = namen.ToDictionary(n => n, _ => 0, StringComparer.Ordinal);
        int groesster = 0;
        foreach (IGrouping<DateOnly, Kompaktspieltag> tag in spieltage.GroupBy(s => s.Tag).OrderBy(g => g.Key))
        {
            foreach (Spiel spiel in tag.SelectMany(s => s.Spiele))
            {
                gespielt[spiel.Heim] = gespielt.GetValueOrDefault(spiel.Heim) + 1;
                gespielt[spiel.Gast] = gespielt.GetValueOrDefault(spiel.Gast) + 1;
            }

            groesster = Math.Max(groesster, gespielt.Values.Max() - gespielt.Values.Min());
        }

        return groesster;
    }

    private static int MeisteAmTag(List<Kompaktspieltag> spieltage) =>
        spieltage.GroupBy(s => s.Tag).Select(g => g.SelectMany(s => s.Mannschaften).Distinct(StringComparer.Ordinal).Count()).DefaultIfEmpty(0).Max();

    private static int Jahr(string text)
    {
        int jahr = int.Parse(text, CultureInfo.InvariantCulture);
        return jahr < 100 ? 2000 + jahr : jahr;
    }

    /// <summary>„Nur Heim:“ und danach nur Daten (der Bericht fügt Zeilen der Bemerkung zusammen; was folgt, gehört nicht dazu).</summary>
    [GeneratedRegex(@"nur\s+heim(?:spiel|spiele)?\s*:\s*((?:\d{1,2}\.\d{1,2}\.(?:\d{4}|\d{2}(?!\d))?(?:\s*[,;]\s*|\s+und\s+|\s+)?)+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NurHeimZeile();

    [GeneratedRegex(@"(\d{1,2})\.(\d{1,2})\.(\d{4}|\d{2}(?!\d))?", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Datum();

    [GeneratedRegex(@"^[IVX]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RoemischeZahl();
}
