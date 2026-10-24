// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Presentation;

namespace AndiGenerator.Mcp;

/// <summary>Gewichtungen, Spielplandaten und Drucken.</summary>
internal sealed partial class Fernsteuerung
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");
    private static readonly string[] Datumsformate = ["d.M.yyyy", "dd.MM.yyyy", "yyyy-MM-dd"];
    private static readonly string[] Zeitformate = ["H:mm", "HH:mm", "H.mm", "H"];

    /// <summary>Holt die Gewichtungen.</summary>
    /// <param name="nurGeaenderte">Nur die, die nicht „normal“ sind.</param>
    /// <returns>Die Gewichtungen (leer ohne Staffel).</returns>
    public Task<IReadOnlyList<Gewichtungsstand>> GewichtungenAsync(bool nurGeaenderte) => faden.Ausfuehren<IReadOnlyList<Gewichtungsstand>>(() =>
        hf.Gewichtungen()
            .Where(g => !nurGeaenderte || g.Wert != Gewichtung.Normal)
            .Select(g => new Gewichtungsstand(g.Ebene, g.Kostenart, g.Mannschaft, g.Stufe))
            .ToList());

    /// <summary>Ändert eine Gewichtung (gilt sofort, auch für die laufende Generierung).</summary>
    /// <param name="kostenart">Name der Kostenart oder leer.</param>
    /// <param name="mannschaft">Name der Mannschaft oder leer.</param>
    /// <param name="stufe">Die Stufe, z. B. „hoch“.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> GewichtungSetzenAsync(string kostenart, string mannschaft, string stufe) => faden.Ausfuehren(() =>
    {
        if (!BereitFuerAnsichten(out string grund))
        {
            return Ergebnis(false, grund);
        }

        Gewichtung? wert = Gewichtungsanzeige.AlleStufen.Select(g => (Gewichtung?)g)
            .FirstOrDefault(g => Gleich(Gewichtungsanzeige.Name(g!.Value), stufe) || (g == Gewichtung.NichtBeruecksichtigen && Gleich(stufe, "aus")));
        if (wert is not Gewichtung neu)
        {
            return Ergebnis(false, $"Keine Stufe „{stufe}“. Möglich: {string.Join(", ", Gewichtungsanzeige.AlleStufen.Select(Gewichtungsanzeige.Name))}.");
        }

        if (hf.GewichtungszielSuchen(kostenart, mannschaft) is not { } ziel)
        {
            return Ergebnis(false, $"Keine Gewichtung für Kostenart „{kostenart}“ und Mannschaft „{mannschaft}“ (Namen siehe „gewichtungen“).");
        }

        string vorher = hf.Gewichtungen().FirstOrDefault(g => g.Ziel == ziel)?.Stufe ?? "normal";
        hf.GewichtungAendern(ziel, neu);
        string wo = string.Join(" · ", new[] { kostenart.Trim(), mannschaft.Trim() }.Where(t => t.Length > 0));
        string hinweis = hf.Automodus ? " Im Automodus lenkt die Suche die Gewichte selbst; die Gewichtung gilt dort als Ausgangspunkt." : string.Empty;
        return Ergebnis(true, $"{wo}: {Gewichtungsanzeige.Name(neu)} (vorher {vorher}).{hinweis}");
    });

    /// <summary>Holt die Spielplandaten im Überblick.</summary>
    /// <returns>Die Daten oder <c>null</c> ohne Staffel.</returns>
    public Task<Spielplandaten?> SpielplandatenAsync() => faden.Ausfuehren(() => hf.SpielplandatenLesen(d =>
    {
        Setzlistendaten setzliste = d.Setzliste();
        return new Spielplandaten(
            d.Mannschaftsnamen().Select(n =>
            {
                IReadOnlyList<Heimtag> tage = d.Heimtage(n);
                return string.Create(Deutsch, $"{n}: {tage.Count(t => t.IstHeimtag)} Heimtermine, {tage.Count(t => t.Sperrtermin)} Sperrtermine");
            }).ToList(),
            d.EingetrageneSpielfreieTage().Select(Tag).ToList(),
            d.Pflichtspielzeiten().Select((p, i) => string.Create(Deutsch, $"{i + 1}. {Tag(p.Von)} – {Tag(p.Bis)}: mindestens {p.Anzahl} Spiele")).ToList(),
            d.VorgegebeneSpiele().Select((v, i) => string.Create(Deutsch, $"{i + 1}. {v.Termin:ddd dd.MM.yyyy HH:mm}: {v.Heim} – {v.Gast}{(v.Spiellokal.Length > 0 ? " (Lokal " + v.Spiellokal + ")" : string.Empty)}")).ToList(),
            (setzliste.Aktiv ? "aktiv: " : "aus: ") + string.Join(", ", setzliste.Reihenfolge));
    }));

    /// <summary>Holt die Spielplandaten einer Mannschaft.</summary>
    /// <param name="mannschaft">Name der Mannschaft.</param>
    /// <returns>Die Daten oder <c>null</c>, wenn es die Mannschaft nicht gibt.</returns>
    public Task<Mannschaftstermine?> MannschaftAsync(string mannschaft) => faden.Ausfuehren(() => hf.SpielplandatenLesen(d =>
    {
        if (d.Mannschaftsnamen().FirstOrDefault(n => Gleich(n, mannschaft)) is not string name)
        {
            return null;
        }

        Mannschaftsdaten m = d.Mannschaft(name);
        return new Mannschaftstermine(
            name,
            string.Create(CultureInfo.InvariantCulture, $"Nummer {m.Nummer}, Mannschafts-ID {m.Id}, Vereins-ID {m.VereinsId}"),
            m.Spiellokal,
            d.Heimtage(name).OrderBy(t => t.Datum).Select(Termintext).ToList(),
            d.Heimkoppeln(name).Where(k => k.Option1 != Koppelwunsch.Keiner || k.Option2 != Koppelwunsch.Keiner).Select(Koppeltext).ToList(),
            d.Auswaertskoppeln(name).Select(k => k.Bezeichnung).ToList(),
            d.Nachbarmannschaften(name).Select(n => Nachbartext(d, name, n)).ToList(),
            Heimrechttext(d.HeimrechtLesen(name)),
            d.NurWochenendeGegen(name),
            d.Bemerkungen().GetValueOrDefault(name, string.Empty));
    }));

    /// <summary>Setzt einen Termin einer Mannschaft: Heimtermin, Ausweichtermin, Sperrtermin oder entfernen.</summary>
    /// <param name="mannschaft">Name der Mannschaft.</param>
    /// <param name="datum">Tag, z. B. 05.09.2026.</param>
    /// <param name="art">„heim“, „ausweich“, „sperre“ oder „entfernen“.</param>
    /// <param name="uhrzeit">Beginn bei Heim- und Ausweichterminen, z. B. 19:30.</param>
    /// <param name="spiellokal">Nummer des Spiellokals oder leer.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> TerminSetzenAsync(string mannschaft, string datum, string art, string uhrzeit, string spiellokal) => DatenAendernAsync(d =>
    {
        if (Name(d, mannschaft) is not string name)
        {
            return $"Keine Mannschaft „{mannschaft}“.";
        }

        if (Datum(datum) is not DateOnly tag)
        {
            return $"„{datum}“ ist kein Datum (TT.MM.JJJJ).";
        }

        string a = art.Trim().ToLowerInvariant();
        TimeOnly? zeit = a is "heim" or "ausweich" ? Zeit(uhrzeit) : TimeOnly.MinValue;
        if (zeit is null)
        {
            return $"Bitte für einen Heimtermin die Uhrzeit angeben (z. B. 19:30), nicht „{uhrzeit}“.";
        }

        List<Heimtag> tage = d.Heimtage(name).Where(t => DateOnly.FromDateTime(t.Datum) != tag).ToList();
        switch (a)
        {
            case "heim" or "ausweich":
                tage.Add(new Heimtag(tag.ToDateTime(zeit.Value), false, false, false, 0, false, TimeOnly.MinValue, 0, a == "ausweich", spiellokal.Trim()));
                break;
            case "sperre":
                tage.Add(new Heimtag(tag.ToDateTime(TimeOnly.MinValue), true, false, false, 0, false, TimeOnly.MinValue, 0, false, string.Empty));
                break;
            case "entfernen":
                break;
            default:
                return $"Keine Art „{art}“ (heim, ausweich, sperre, entfernen).";
        }

        d.HeimtageSpeichern(name, tage.OrderBy(t => t.Datum));
        return null;
    });

    /// <summary>Trägt spielfreie Tage ein oder aus.</summary>
    /// <param name="tage">Die Tage.</param>
    /// <param name="eintragen"><c>true</c> = eintragen, <c>false</c> = austragen.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> SpielfreieTageAsync(IReadOnlyList<string> tage, bool eintragen) => DatenAendernAsync(d =>
    {
        List<DateOnly?> gelesen = tage.Select(Datum).ToList();
        if (gelesen.IndexOf(null) is int falsch and >= 0)
        {
            return $"„{tage[falsch]}“ ist kein Datum (TT.MM.JJJJ).";
        }

        // Wirksam sind spielfreie Tage nur an möglichen Spieltagen (mindestens ein Heimtermin); die übrigen bleiben unberührt.
        HashSet<DateOnly> moeglich = [.. d.MoeglicheSpieltage()];
        if (gelesen.OfType<DateOnly>().FirstOrDefault(t => !moeglich.Contains(t)) is DateOnly ohne && ohne != default)
        {
            return $"Am {Tag(ohne)} hat keine Mannschaft einen Heimtermin – dort wird ohnehin nicht gespielt.";
        }

        var neu = new SortedSet<DateOnly>(d.SpielfreieTage());
        foreach (DateOnly tag in gelesen.OfType<DateOnly>())
        {
            if (eintragen)
            {
                neu.Add(tag);
            }
            else
            {
                neu.Remove(tag);
            }
        }

        d.SpielfreieTageSpeichern(neu);
        return null;
    });

    /// <summary>Setzt die Setzliste.</summary>
    /// <param name="reihenfolge">Alle Mannschaften, die stärkste zuerst (leer = Reihenfolge behalten).</param>
    /// <param name="aktiv">Setzliste berücksichtigen.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> SetzlisteAsync(IReadOnlyList<string> reihenfolge, bool aktiv) => DatenAendernAsync(d =>
    {
        IReadOnlyList<string> alle = d.Setzliste().Reihenfolge;
        if (reihenfolge.Count == 0)
        {
            d.SetzlisteSpeichern(new Setzlistendaten(aktiv, alle));
            return null;
        }

        List<string?> namen = reihenfolge.Select(r => alle.FirstOrDefault(n => Gleich(n, r))).ToList();
        if (namen.IndexOf(null) is int falsch and >= 0)
        {
            return $"Keine Mannschaft „{reihenfolge[falsch]}“.";
        }

        if (namen.Distinct().Count() != alle.Count)
        {
            return $"Die Reihenfolge muss jede der {alle.Count} Mannschaften genau einmal nennen.";
        }

        d.SetzlisteSpeichern(new Setzlistendaten(aktiv, namen.OfType<string>().ToList()));
        return null;
    });

    /// <summary>Legt fest, gegen wen eine Mannschaft auswärts nur am Wochenende spielt (60-km-Regel).</summary>
    /// <param name="mannschaft">Die Gastmannschaft.</param>
    /// <param name="gegner">Die Gastgeber (leer = keine).</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> NurWochenendeAsync(string mannschaft, IReadOnlyList<string> gegner) => DatenAendernAsync(d =>
    {
        if (Name(d, mannschaft) is not string name)
        {
            return $"Keine Mannschaft „{mannschaft}“.";
        }

        List<string?> namen = gegner.Select(g => Name(d, g)).ToList();
        if (namen.IndexOf(null) is int falsch and >= 0)
        {
            return $"Keine Mannschaft „{gegner[falsch]}“.";
        }

        d.NurWochenendeSpeichern(name, namen.OfType<string>().Where(n => n != name).Distinct());
        return null;
    });

    /// <summary>Legt eine Begegnung fest (Termin von Hand).</summary>
    /// <param name="heim">Heimmannschaft.</param>
    /// <param name="gast">Gastmannschaft.</param>
    /// <param name="termin">Tag und Uhrzeit, z. B. 05.09.2026 18:00.</param>
    /// <param name="spiellokal">Nummer des Spiellokals oder leer.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> BegegnungFestlegenAsync(string heim, string gast, string termin, string spiellokal) => DatenAendernAsync(d =>
    {
        string[] teile = termin.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (teile.Length != 2 || Datum(teile[0]) is not DateOnly tag || Zeit(teile[1]) is not TimeOnly zeit)
        {
            return $"„{termin}“ ist kein Termin (TT.MM.JJJJ HH:MM).";
        }

        if (Name(d, heim) is not string h || Name(d, gast) is not string g)
        {
            return $"Keine Mannschaft „{(Name(d, heim) is null ? heim : gast)}“.";
        }

        var spiel = new Vorgabespiel(-1, tag.ToDateTime(zeit), h, g, spiellokal.Trim());
        if (d.VorgegebenesSpielPruefen(null, spiel) is string fehler)
        {
            return fehler;
        }

        d.VorgegebenesSpielSpeichern(null, spiel);
        return null;
    });

    /// <summary>Löscht eine festgelegte Begegnung.</summary>
    /// <param name="nummer">Nummer aus „spielplandaten“ (ab 1).</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> BegegnungLoeschenAsync(int nummer) => DatenAendernAsync(d =>
    {
        if (nummer < 1 || nummer > d.VorgegebeneSpiele().Count)
        {
            return $"Keine festgelegte Begegnung Nummer {nummer}.";
        }

        d.VorgegebenesSpielLoeschen(nummer - 1);
        return null;
    });

    /// <summary>Trägt eine Pflichtspielzeit ein oder löscht eine.</summary>
    /// <param name="von">Erster Tag.</param>
    /// <param name="bis">Letzter Tag.</param>
    /// <param name="anzahl">Mindestens so viele Spiele je Mannschaft (1–10); 0 löscht den Zeitraum.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> PflichtspielzeitAsync(string von, string bis, int anzahl) => DatenAendernAsync(d =>
    {
        if (Datum(von) is not DateOnly a || Datum(bis) is not DateOnly b || b < a)
        {
            return $"„{von}“ bis „{bis}“ ist kein Zeitraum (TT.MM.JJJJ).";
        }

        List<Pflichtspielzeit> zeiten = d.Pflichtspielzeiten().Where(p => p.Von != a || p.Bis != b).ToList();
        if (anzahl > 0)
        {
            if (!Pflichtspielzeit.Anzahlen.Contains(anzahl))
            {
                return "Die Anzahl muss zwischen 1 und 10 liegen.";
            }

            zeiten.Add(new Pflichtspielzeit(a, b, anzahl));
        }

        d.PflichtspielzeitenSpeichern(zeiten.OrderBy(p => p.Von));
        return null;
    });

    /// <summary>Setzt eine Meldung im Raster der Kompaktspieltage.</summary>
    /// <param name="mannschaft">Name der Mannschaft.</param>
    /// <param name="datum">Der Tag.</param>
    /// <param name="meldung">„A“ auswärts, „H“ nur Heim, „B“ beides, „-“ kann nicht.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> KompaktMeldungAsync(string mannschaft, string datum, string meldung) => DatenAendernAsync(d =>
    {
        Kompaktfeld? feld = meldung.Trim().ToUpperInvariant() switch
        {
            "A" => Kompaktfeld.Auswaerts,
            "H" => Kompaktfeld.Heim,
            "B" => Kompaktfeld.Beides,
            "-" => Kompaktfeld.Leer,
            _ => null,
        };
        if (feld is not Kompaktfeld f)
        {
            return $"Keine Meldung „{meldung}“ (A auswärts, H nur Heim, B beides, - kann nicht).";
        }

        if (Name(d, mannschaft) is not string name || Datum(datum) is not DateOnly tag)
        {
            return Name(d, mannschaft) is null ? $"Keine Mannschaft „{mannschaft}“." : $"„{datum}“ ist kein Datum (TT.MM.JJJJ).";
        }

        d.KompaktfeldSetzen(name, tag, f);
        return null;
    });

    /// <summary>Druckt (PDF) wie in der Oberfläche.</summary>
    /// <param name="pfad">Zieldatei (.pdf).</param>
    /// <param name="plan">Name des Plans (Quelle) oder leer für den vorgewählten.</param>
    /// <param name="abschnitte">Abschnitte oder leer für die Vorgabe.</param>
    /// <param name="querformat">Querformat.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> DruckenAsync(string pfad, string plan, IReadOnlyList<string> abschnitte, bool? querformat) => faden.Ausfuehren(async () =>
    {
        if (!BereitFuerAnsichten(out string grund))
        {
            return Ergebnis(false, grund);
        }

        if (!string.Equals(Path.GetExtension(pfad), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Ergebnis(false, "Die Zieldatei muss auf .pdf enden.");
        }

        string? fehler = null;
        oberflaeche.Druckauswahl = a => DruckauswahlEinstellen(a, plan, abschnitte, querformat, out fehler);
        string ziel = Path.GetFullPath(pfad);
        DateTime vorher = File.Exists(ziel) ? File.GetLastWriteTimeUtc(ziel) : DateTime.MinValue;
        oberflaeche.DateiZumSpeichern = ziel;
        try
        {
            await hf.DruckenCommand.ExecuteAsync(null);
        }
        finally
        {
            oberflaeche.Druckauswahl = null;
            oberflaeche.DateiZumSpeichern = null;
        }

        if (fehler is not null)
        {
            return Ergebnis(false, fehler);
        }

        bool geschrieben = File.Exists(ziel) && File.GetLastWriteTimeUtc(ziel) > vorher;
        return Ergebnis(geschrieben, geschrieben ? $"Gedruckt nach „{ziel}“." : "Nicht gedruckt – siehe Meldungen.");
    });

    private static bool DruckauswahlEinstellen(DruckauswahlViewModel a, string plan, IReadOnlyList<string> abschnitte, bool? querformat, out string? fehler)
    {
        fehler = null;
        if (plan.Trim().Length > 0)
        {
            if (a.Quellen.FirstOrDefault(q => Gleich(q.Name, plan)) is not { } quelle)
            {
                fehler = $"Keinen Plan „{plan}“ zum Drucken. Möglich: {string.Join(", ", a.Quellen.Select(q => q.Name))}.";
                return false;
            }

            a.Quelle = quelle;
        }

        if (abschnitte.Count > 0)
        {
            var namen = abschnitte.Select(n => n.Trim().ToLowerInvariant()).ToHashSet();
            string[] bekannt = ["spielplan", "mannschaftsplaene", "mannschaftsplaene_mit_nachbarn", "terminwuensche", "nachbartermine", "kosten", "diagramme", "kompaktspieltage"];
            if (namen.FirstOrDefault(n => !bekannt.Contains(n)) is string unbekannt)
            {
                fehler = $"Keinen Abschnitt „{unbekannt}“. Möglich: {string.Join(", ", bekannt)}.";
                return false;
            }

            a.Spielplan = namen.Contains("spielplan");
            a.Mannschaftsplaene = namen.Contains("mannschaftsplaene") || namen.Contains("mannschaftsplaene_mit_nachbarn");
            a.MannschaftsplaeneMitNachbarn = namen.Contains("mannschaftsplaene_mit_nachbarn");
            a.Terminwuensche = namen.Contains("terminwuensche");
            a.Nachbartermine = namen.Contains("nachbartermine");
            a.Kosten = namen.Contains("kosten");
            a.Diagramme = namen.Contains("diagramme");
            a.Kompaktspieltage = namen.Contains("kompaktspieltage");
        }

        if (querformat is bool quer)
        {
            a.Querformat = quer;
        }

        if (!a.IstGueltig)
        {
            fehler = "Diese Auswahl lässt sich nicht drucken (kein Plan oder kein Abschnitt gewählt).";
            return false;
        }

        return true;
    }

    private static string Tag(DateOnly tag) => tag.ToString("ddd dd.MM.yyyy", Deutsch);

    private static string Termintext(Heimtag t)
    {
        if (t.Sperrtermin)
        {
            return t.Datum.ToString("ddd dd.MM.yyyy", Deutsch) + " Sperrtermin";
        }

        string art = (t.Ausweichtermin, t.Koppeltermin, t.Doppeltermin) switch
        {
            (true, _, _) => "Ausweichtermin",
            (_, true, _) => "Heimtermin, Koppeltermin",
            (_, _, true) => "Heimtermin, Doppeltermin",
            _ => "Heimtermin",
        };
        string lokal = t.Spiellokal.Length > 0 ? " (Lokal " + t.Spiellokal + ")" : string.Empty;
        return t.Datum.ToString("ddd dd.MM.yyyy HH:mm", Deutsch) + " " + art + lokal;
    }

    private static DateOnly? Datum(string text) =>
        DateOnly.TryParseExact(text.Trim(), Datumsformate, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly tag) ? tag : null;

    private static TimeOnly? Zeit(string text) =>
        TimeOnly.TryParseExact(text.Trim(), Zeitformate, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly zeit) ? zeit : null;

    private static string? Name(Datenbearbeitung d, string mannschaft) => d.Mannschaftsnamen().FirstOrDefault(n => Gleich(n, mannschaft));

    /// <summary>Ändert die Spielplandaten wie die Oberfläche und liefert das Ergebnis mit dem Stand danach.</summary>
    private Task<Aktionsergebnis> DatenAendernAsync(Func<Datenbearbeitung, string?> aenderung) => faden.Ausfuehren(async () =>
    {
        if (!BereitFuerAnsichten(out string grund))
        {
            return Ergebnis(false, grund);
        }

        string? fehler = await hf.SpielplandatenAendernAsync(aenderung);
        return fehler is null ? Ergebnis(true, "Gespeichert; eine laufende Generierung rechnet mit den neuen Daten weiter.") : Ergebnis(false, fehler);
    });
}
