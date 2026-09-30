// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Persistence.Gemeinsam;

namespace AndiGenerator.Persistence.Optionen;

/// <summary>
/// Einstellungsdatei einer Staffel (<c>AndiGenerator.options</c>, Original <c>TCalculateOptions.load</c>/<c>Save</c>).
/// Geschrieben wird wie im Original: iso-8859-15, XML-Deklaration + CRLF, der Baum in einer Zeile, CRLF am Ende.
/// Gelesen wird mit den Eigenheiten des Originals: fehlende oder unbekannte Werte werden zu <see cref="Gewichtung.Normal"/>,
/// jeder Fehler (z. B. ein ungültiges Datum) führt zu <see cref="Berechnungsoptionen.Standard"/>.
/// </summary>
public static class OptionenDatei
{
    /// <summary>Dateiname im Staffelordner (<c>%LOCALAPPDATA%\Andi-Generator\&lt;Staffel&gt;</c>).</summary>
    public const string Dateiname = "AndiGenerator.options";

    private const string Deklaration = "<?xml version=\"1.0\" encoding=\"iso-8859-15\"?>";

    /// <summary>Delphi-Namen von <c>TCalculateOptionsValues</c>, Index = <see cref="Gewichtung"/>.</summary>
    private static readonly string[] GewichtungsNamen =
        ["coIgnore", "coSehrWenig", "coWenig", "coNormal", "coHoch", "coSehrHoch", "coExtremHoch"];

    /// <summary>Delphi-Namen von <c>TMannschaftsKostenType</c>, Index = <see cref="MannschaftsKostenart"/>.</summary>
    private static readonly string[] KostenartNamen =
    [
        "mktHalleBelegt", "mktSisterGames", "mktForceSameHomeGames", "mktSperrTermine", "mktAusweichTermine",
        "mkt60Kilometer", "mktEngeTermine", "mkt2SpieleProWoche", "mktSpielverteilung", "mktKoppelTermine",
        "mktAuswaertsKoppelTermine", "mktZahlHeimSpielTermine", "mktWechselHeimAuswaerts", "mktAbstandHeimAuswaerts",
        "mktRanking", "mktMandatoryGames",
    ];

    /// <summary>Delphi-Namen von <c>TRoundPlaning</c>, Index = <see cref="Rundenplanung"/>.</summary>
    private static readonly string[] RundenplanungsNamen = ["rpBoth", "rpHalfRound", "rpFirstOnly", "rpSecondOnly", "rpCorona"];

    private static Encoding Iso885915
    {
        get
        {
            DelphiKompatibel.CodepagesRegistrieren();
            return Encoding.GetEncoding("iso-8859-15");
        }
    }

    /// <summary>Lädt die Einstellungen; fehlt die Datei, gelten die Standardeinstellungen.</summary>
    /// <param name="pfad">Pfad der Datei <c>AndiGenerator.options</c>.</param>
    /// <returns>Die gelesenen Einstellungen oder die Standardeinstellungen.</returns>
    public static Berechnungsoptionen Laden(string pfad)
    {
        if (!File.Exists(pfad))
        {
            return Berechnungsoptionen.Standard;
        }

        try
        {
            using FileStream strom = File.OpenRead(pfad);
            return Laden(strom);
        }
        catch (IOException)
        {
            return Berechnungsoptionen.Standard;
        }
        catch (UnauthorizedAccessException)
        {
            return Berechnungsoptionen.Standard;
        }
    }

    /// <summary>Lädt die Einstellungen; bei jedem Fehler gelten wie im Original die Standardeinstellungen.</summary>
    /// <param name="strom">Dateiinhalt.</param>
    /// <returns>Die gelesenen Einstellungen oder die Standardeinstellungen.</returns>
    public static Berechnungsoptionen Laden(Stream strom)
    {
        ArgumentNullException.ThrowIfNull(strom);
        DelphiKompatibel.CodepagesRegistrieren();
        try
        {
            using XmlReader leser = XmlReader.Create(strom, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            XDocument dokument = XDocument.Load(leser, LoadOptions.None);
            return Auswerten(dokument.Root ?? throw new FormatException("Kein Wurzelelement."));
        }
        catch (Exception ex) when (ex is XmlException or FormatException or OverflowException or ArgumentException)
        {
            return Berechnungsoptionen.Standard;
        }
    }

    /// <summary>Erzeugt den Dateiinhalt (iso-8859-15).</summary>
    /// <param name="optionen">Zu speichernde Einstellungen.</param>
    /// <returns>Der Dateiinhalt in iso-8859-15.</returns>
    /// <exception cref="ArgumentException">Eine Liste der Gewichtungen je Kostenart hat die falsche Länge.</exception>
    public static byte[] ErzeugenBytes(Berechnungsoptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(optionen);
        if (optionen.JeKostenart.Count != Berechnungsoptionen.AnzahlKostenarten)
        {
            throw new ArgumentException("JeKostenart muss für jede Kostenart einen Wert enthalten.", nameof(optionen));
        }

        var text = new StringBuilder();
        text.Append(Deklaration).Append("\r\n<andigenerator-options><weighting");
        Attribut(text, "friday-is-part-of-weekend", optionen.FreitagZaehltZumWochenende ? "true" : "false");
        Attribut(text, "gameday", Name(optionen.Spieltag));
        Attribut(text, "gameday-overlapp", Name(optionen.SpieltagUeberlappung));
        Attribut(text, "last-gameday", Name(optionen.LetzterSpieltag));
        Attribut(text, "last-gameday-overlapp", Name(optionen.LetzterSpieltagUeberlappung));
        Attribut(text, "sister-game-at-start", Name(optionen.VereinsinterneSpieleAmAnfang));
        for (int i = 0; i < KostenartNamen.Length; i++)
        {
            Attribut(text, KostenartNamen[i], Name(optionen.JeKostenart[i]));
        }

        if (optionen.Mannschaften.Count == 0)
        {
            text.Append("/>");
        }
        else
        {
            text.Append('>');
            foreach (MannschaftsGewichtung m in optionen.Mannschaften)
            {
                if (m.JeKostenart.Count != Berechnungsoptionen.AnzahlKostenarten)
                {
                    throw new ArgumentException($"Mannschaft {m.MannschaftsId}: JeKostenart muss für jede Kostenart einen Wert enthalten.", nameof(optionen));
                }

                text.Append("<team");
                Attribut(text, "teamid", m.MannschaftsId);
                Attribut(text, "main", Name(m.Gesamt));
                for (int i = 0; i < KostenartNamen.Length; i++)
                {
                    Attribut(text, KostenartNamen[i], Name(m.JeKostenart[i]));
                }

                text.Append("/>");
            }

            text.Append("</weighting>");
        }

        text.Append("<round-planing");
        Attribut(text, "planing", RundenplanungsNamen[(int)optionen.Rundenplanung]);
        text.Append("/><double-round");
        Attribut(text, "active", optionen.Doppelrunde ? "true" : "false");
        if (optionen.AutomatischeMitte)
        {
            Attribut(text, "automatic-mid-date", "true");
        }
        else
        {
            Attribut(text, "automatic-mid-date", "false");
            if (optionen.Mitte1 is DateOnly m1)
            {
                Attribut(text, "mid-date-1", m1.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
            }

            if (optionen.Mitte2 is DateOnly m2)
            {
                Attribut(text, "mid-date-2", m2.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
            }
        }

        text.Append("/></andigenerator-options>\r\n");
        return Iso885915.GetBytes(text.ToString());
    }

    /// <summary>Schreibt die Datei (erst in eine temporäre Datei, dann ersetzen).</summary>
    /// <param name="pfad">Pfad der Datei.</param>
    /// <param name="optionen">Zu speichernde Einstellungen.</param>
    public static void Speichern(string pfad, Berechnungsoptionen optionen)
    {
        byte[] inhalt = ErzeugenBytes(optionen);
        string temp = pfad + ".tmp";
        File.WriteAllBytes(temp, inhalt);
        File.Move(temp, pfad, overwrite: true);
    }

    private static Berechnungsoptionen Auswerten(XElement wurzel)
    {
        Berechnungsoptionen o = Berechnungsoptionen.Standard;

        XElement? gewichtung = wurzel.Elements("weighting").FirstOrDefault();
        if (gewichtung is not null)
        {
            var jeKostenart = new Gewichtung[Berechnungsoptionen.AnzahlKostenarten];
            for (int i = 0; i < jeKostenart.Length; i++)
            {
                jeKostenart[i] = GewichtungLesen(gewichtung, KostenartNamen[i]);
            }

            // Reihenfolge wie gelesen; eine doppelte teamid überschreibt die Werte, behält aber ihren Platz (TDictionary im Original).
            var mannschaften = new List<MannschaftsGewichtung>();
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (XElement team in gewichtung.Elements("team"))
            {
                string id = AttributLesen(team, "teamid");
                if (id.Length == 0)
                {
                    continue;
                }

                var details = new Gewichtung[Berechnungsoptionen.AnzahlKostenarten];
                for (int i = 0; i < details.Length; i++)
                {
                    details[i] = GewichtungLesen(team, KostenartNamen[i]);
                }

                var eintrag = new MannschaftsGewichtung(id, GewichtungLesen(team, "main"), details);
                if (index.TryGetValue(id, out int vorhanden))
                {
                    mannschaften[vorhanden] = eintrag;
                }
                else
                {
                    index.Add(id, mannschaften.Count);
                    mannschaften.Add(eintrag);
                }
            }

            o = o with
            {
                // Original: fehlt das Attribut, ist der Wert false (nicht der Standard true).
                FreitagZaehltZumWochenende = AttributLesen(gewichtung, "friday-is-part-of-weekend") == "true",
                Spieltag = GewichtungLesen(gewichtung, "gameday"),
                SpieltagUeberlappung = GewichtungLesen(gewichtung, "gameday-overlapp"),
                LetzterSpieltag = GewichtungLesen(gewichtung, "last-gameday"),
                LetzterSpieltagUeberlappung = GewichtungLesen(gewichtung, "last-gameday-overlapp"),
                VereinsinterneSpieleAmAnfang = GewichtungLesen(gewichtung, "sister-game-at-start"),
                JeKostenart = jeKostenart,
                Mannschaften = mannschaften,
            };
        }

        XElement? runde = wurzel.Elements("round-planing").FirstOrDefault();
        if (runde is not null)
        {
            int wert = EnumWert(RundenplanungsNamen, AttributLesen(runde, "planing"));
            o = o with { Rundenplanung = wert >= 0 ? (Rundenplanung)wert : Rundenplanung.Beide };
        }

        XElement? doppel = wurzel.Elements("double-round").FirstOrDefault();
        if (doppel is not null)
        {
            string mitte1 = AttributLesen(doppel, "mid-date-1");
            string mitte2 = AttributLesen(doppel, "mid-date-2");
            o = o with
            {
                Doppelrunde = AttributLesen(doppel, "active") == "true",
                AutomatischeMitte = AttributLesen(doppel, "automatic-mid-date") == "true",
                Mitte1 = mitte1.Length > 0 ? DelphiKompatibel.DateFromString(mitte1) : null,
                Mitte2 = mitte2.Length > 0 ? DelphiKompatibel.DateFromString(mitte2) : null,
            };
        }

        return o;
    }

    private static string AttributLesen(XElement element, string name) => element.Attribute(name)?.Value ?? string.Empty;

    /// <summary>Original <c>getOptionsValueFromAttribute</c>: Name ohne Beachtung der Groß-/Kleinschreibung, sonst coNormal.</summary>
    private static Gewichtung GewichtungLesen(XElement element, string name)
    {
        int wert = EnumWert(GewichtungsNamen, AttributLesen(element, name));
        return wert >= 0 ? (Gewichtung)wert : Gewichtung.Normal;
    }

    /// <summary>Delphi <c>GetEnumValue</c>: Namensvergleich ohne Groß-/Kleinschreibung, -1 wenn unbekannt.</summary>
    private static int EnumWert(string[] namen, string wert)
    {
        for (int i = 0; i < namen.Length; i++)
        {
            if (string.Equals(namen[i], wert, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string Name(Gewichtung g) => GewichtungsNamen[(int)g];

    private static void Attribut(StringBuilder text, string name, string wert)
    {
        text.Append(' ').Append(name).Append("=\"");
        foreach (char z in wert)
        {
            switch (z)
            {
                case '&': text.Append("&amp;"); break;
                case '<': text.Append("&lt;"); break;
                case '>': text.Append("&gt;"); break;
                case '"': text.Append("&quot;"); break;
                default: text.Append(z); break;
            }
        }

        text.Append('"');
    }
}
