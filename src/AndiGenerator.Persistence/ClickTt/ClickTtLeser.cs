// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Xml;
using System.Xml.Linq;
using AndiGenerator.Persistence.Gemeinsam;

namespace AndiGenerator.Persistence.ClickTt;

/// <summary>
/// Liest den click-TT-Export (Nachbildung von <c>TPlanData.LoadFromClickTTFile</c>, siehe Doku PlanDataObjects).
/// Elemente werden über ihren lokalen Namen erkannt, der Namespace <c>http://www.tt-liga.de/TTGenerator</c> ist optional.
/// </summary>
public static class ClickTtLeser
{
    /// <summary>Prüft, ob die Datei ein click-TT-Export ist (Wurzel <c>TT</c>), ohne sie vollständig zu laden.</summary>
    /// <param name="pfad">Pfad der Datei.</param>
    /// <returns><c>true</c>, wenn das Wurzelelement <c>TT</c> heißt.</returns>
    public static bool IstClickTtDatei(string pfad)
    {
        DelphiKompatibel.CodepagesRegistrieren();
        using XmlReader leser = XmlReader.Create(pfad, Einstellungen());
        return leser.MoveToContent() == XmlNodeType.Element && leser.LocalName == "TT";
    }

    /// <summary>Liest eine click-TT-Exportdatei.</summary>
    /// <param name="pfad">Pfad der Datei.</param>
    /// <returns>Die gelesene Staffel.</returns>
    public static ClickTtStaffel Lesen(string pfad)
    {
        using FileStream strom = File.OpenRead(pfad);
        return Lesen(strom);
    }

    /// <summary>Liest einen click-TT-Export aus einem Datenstrom (Kodierung laut XML-Deklaration, z.B. iso-8859-15).</summary>
    /// <param name="strom">Dateiinhalt.</param>
    /// <returns>Die gelesene Staffel.</returns>
    public static ClickTtStaffel Lesen(Stream strom)
    {
        DelphiKompatibel.CodepagesRegistrieren();
        XDocument dokument;
        try
        {
            using XmlReader leser = XmlReader.Create(strom, Einstellungen());
            dokument = XDocument.Load(leser);
        }
        catch (XmlException ex)
        {
            throw new ClickTtFormatException("Die Datei ist kein gültiges XML: " + ex.Message, ex);
        }

        XElement wurzel = dokument.Root ?? throw new ClickTtFormatException("Die Datei ist leer.");
        if (wurzel.Name.LocalName != "TT")
        {
            throw new ClickTtFormatException($"Keine click-TT-Exportdatei (Wurzelelement '{wurzel.Name.LocalName}' statt 'TT').");
        }

        return new ClickTtStaffel(
            Name: Text(wurzel, "name"),
            Id: Text(wurzel, "id"),
            Geschlecht: Text(wurzel, "gender"),
            Von: PflichtDatum(wurzel, "from"),
            Bis: PflichtDatum(wurzel, "until"),
            Rueckrundenbeginn: DelphiKompatibel.DatumLesen(Attribut(wurzel, "mid")),
            SaisonTyp: Attribut(wurzel, "seasonType") is { } typ ? DelphiKompatibel.Trim(typ) : null,
            Mannschaften: Kinder(Kind(wurzel, "teams"), "team").Select(LeseMannschaft).ToList(),
            VorgegebeneSpiele: Spiele(Kind(wurzel, "predefinedGames")),
            SpielfreieZeitraeume: Kinder(Kind(wurzel, "noGameDays"), "noGame").Select(z => LeseZeitraum(z, pflicht: true)!).ToList(),
            Pflichtspieltage: Kinder(Kind(wurzel, "mandatoryGameDays"), "mandatoryDay")
                .Select(t => DelphiKompatibel.DatumLesen(Attribut(t, "day")))
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToList(),
            BestehenderSpielplan: Spiele(Kind(wurzel, "existingSchedule")));
    }

    private static ClickTtMannschaft LeseMannschaft(XElement team)
    {
        XElement? spieltage = Kind(team, "gameDays");
        return new ClickTtMannschaft(
            Id: Text(team, "id"),
            VereinsId: Text(team, "clubId"),
            Nummer: DelphiKompatibel.ZahlLesen(Attribut(team, "number"), 1),
            Geschlecht: Text(team, "gender"),
            Name: DelphiKompatibel.Trim(Kind(team, "name")?.Value),
            KeineDoppelspieltage: DelphiKompatibel.BoolLesen(Attribut(team, "noDoubleDays"), standard: true),
            Heimspieltermine: Kinder(spieltage, "homeGame").Select(LeseHeimspieltermin).OfType<ClickTtHeimspieltermin>().ToList(),
            Sperrzeitraeume: Kinder(spieltage, "noGame").Select(z => LeseZeitraum(z, pflicht: false)).OfType<ClickTtZeitraum>().ToList(),
            Auswaertskoppeln: Kinder(spieltage, "roadCouple")
                .Select(k => new ClickTtAuswaertskoppel(Text(k, "teamA"), Text(k, "teamB"), DelphiKompatibel.BoolLesen(Attribut(k, "onSameDay"), standard: false)))
                .ToList(),
            KeinWochenspielGegen: Kinder(spieltage, "noWeekGame").Select(g => Text(g, "team")).ToList(),
            Nachbarmannschaften: Kinder(Kind(team, "sisterTeams"), "team").Select(LeseNachbarmannschaft).ToList());
    }

    private static ClickTtHeimspieltermin? LeseHeimspieltermin(XElement termin)
    {
        // Wie im Original nur mit Tag und Uhrzeit.
        DateOnly? tag = DelphiKompatibel.DatumLesen(Attribut(termin, "day"));
        TimeOnly? zeit = DelphiKompatibel.ZeitLesen(Attribut(termin, "time"));
        if (tag is null || zeit is null)
        {
            return null;
        }

        string? koppel = Attribut(termin, "coupleGame");
        return new ClickTtHeimspieltermin(
            Zeitpunkt: tag.Value.ToDateTime(zeit.Value),
            Ausweichtermin: DelphiKompatibel.BoolLesen(Attribut(termin, "secondary"), standard: false),
            MaxParalleleSpiele: DelphiKompatibel.ZahlLesen(Attribut(termin, "maxParallelGames"), 0),
            Koppelwunsch: koppel is null ? null : DelphiKompatibel.Trim(koppel),
            Halle: Text(termin, "courtHall"));
    }

    private static ClickTtNachbarmannschaft LeseNachbarmannschaft(XElement team)
    {
        return new ClickTtNachbarmannschaft(
            Name: DelphiKompatibel.Trim(Kind(team, "name")?.Value),
            Geschlecht: Text(team, "gender"),
            Nummer: DelphiKompatibel.ZahlLesen(Attribut(team, "number"), 0), // Original: Standard 0 (Staffelmannschaft: 1)
            Spiele: Kinder(team, "game").Select(LeseSpiel).OfType<ClickTtSpiel>().ToList());
    }

    private static ClickTtZeitraum? LeseZeitraum(XElement zeitraum, bool pflicht)
    {
        DateOnly? von = DelphiKompatibel.DatumLesen(Attribut(zeitraum, "from"));
        DateOnly? bis = DelphiKompatibel.DatumLesen(Attribut(zeitraum, "until"));
        if (von is null || bis is null)
        {
            if (pflicht)
            {
                throw new ClickTtFormatException($"'{zeitraum.Name.LocalName}' ohne gültiges 'from'/'until'.");
            }

            return null;
        }

        return new ClickTtZeitraum(von.Value, bis.Value);
    }

    private static List<ClickTtSpiel> Spiele(XElement? liste)
    {
        return Kinder(liste, "game").Select(LeseSpiel).OfType<ClickTtSpiel>().ToList();
    }

    private static ClickTtSpiel? LeseSpiel(XElement spiel)
    {
        DateOnly? tag = DelphiKompatibel.DatumLesen(Attribut(spiel, "day"));
        if (tag is null)
        {
            return null;
        }

        TimeOnly zeit = DelphiKompatibel.ZeitLesen(Attribut(spiel, "time")) ?? TimeOnly.MinValue;
        return new ClickTtSpiel(tag.Value.ToDateTime(zeit), Text(spiel, "homeTeam"), Text(spiel, "guestTeam"), Text(spiel, "courtHall"));
    }

    private static DateOnly PflichtDatum(XElement element, string name)
    {
        return DelphiKompatibel.DatumLesen(Attribut(element, name))
            ?? throw new ClickTtFormatException($"Pflichtattribut '{name}' fehlt oder ist kein Datum (dd.mm.yyyy).");
    }

    private static string? Attribut(XElement element, string name) => element.Attribute(name)?.Value;

    private static string Text(XElement element, string name) => DelphiKompatibel.Trim(Attribut(element, name));

    private static XElement? Kind(XElement? element, string name) => element?.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    private static IEnumerable<XElement> Kinder(XElement? element, string name) =>
        element?.Elements().Where(e => e.Name.LocalName == name) ?? Enumerable.Empty<XElement>();

    private static XmlReaderSettings Einstellungen() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
    };
}
