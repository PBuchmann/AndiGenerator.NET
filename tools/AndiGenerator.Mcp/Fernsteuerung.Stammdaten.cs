// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;

namespace AndiGenerator.Mcp;

/// <summary>Mannschaften, Spiellokale, Heim- und Auswärtskoppeln, Nachbarmannschaften und Heimrecht.</summary>
internal sealed partial class Fernsteuerung
{
    private static readonly (string Name, Koppelwunsch Wunsch)[] Koppelwuensche =
    [
        ("keiner", Koppelwunsch.Keiner), ("moeglich", Koppelwunsch.Moeglich), ("gewuenscht", Koppelwunsch.Gewuenscht),
        ("hoch", Koppelwunsch.Hoch), ("doppel_moeglich", Koppelwunsch.DoppelMoeglich), ("doppel_gewuenscht", Koppelwunsch.DoppelGewuenscht),
        ("doppel_hoch", Koppelwunsch.DoppelHoch),
    ];

    /// <summary>Legt eine Mannschaft an oder ändert sie.</summary>
    /// <param name="name">Name (bestehend oder neu).</param>
    /// <param name="neuerName">Neuer Name oder leer.</param>
    /// <param name="nummer">Mannschaftsnummer oder 0 = unverändert (neu: 1).</param>
    /// <param name="id">Mannschafts-ID aus click-TT oder leer = unverändert.</param>
    /// <param name="vereinsId">Vereins-ID oder leer = unverändert.</param>
    /// <param name="spiellokal">Standard-Spiellokal (leer, 1–5) oder <c>null</c> = unverändert.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> MannschaftSpeichernAsync(string name, string neuerName, int nummer, string id, string vereinsId, string? spiellokal) => DatenAendernAsync(d =>
    {
        string? bisher = Name(d, name);
        Mannschaftsdaten alt = bisher is null ? Mannschaftsdaten.Neu with { Name = name.Trim() } : d.Mannschaft(bisher);
        var neu = new Mannschaftsdaten(
            neuerName.Trim().Length > 0 ? neuerName.Trim() : alt.Name,
            id.Trim().Length > 0 ? id.Trim() : alt.Id,
            vereinsId.Trim().Length > 0 ? vereinsId.Trim() : alt.VereinsId,
            nummer > 0 ? nummer : alt.Nummer,
            spiellokal?.Trim() ?? alt.Spiellokal);
        if (d.MannschaftPruefen(bisher, neu) is string fehler)
        {
            return fehler;
        }

        d.MannschaftSpeichern(bisher, neu);
        return null;
    });

    /// <summary>Löscht eine Mannschaft.</summary>
    /// <param name="name">Name.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> MannschaftLoeschenAsync(string name) => DatenAendernAsync(d =>
    {
        if (Name(d, name) is not string n)
        {
            return $"Keine Mannschaft „{name}“.";
        }

        d.MannschaftLoeschen(n);
        return null;
    });

    /// <summary>Setzt ein Spiellokal: Standard der Mannschaft, eines Heimtermins oder einer Nachbarmannschaft.</summary>
    /// <param name="mannschaft">Name der Mannschaft.</param>
    /// <param name="lokal">Nummer (1–5) oder leer.</param>
    /// <param name="datum">Tag eines Heimtermins oder leer.</param>
    /// <param name="nachbar">Bezeichnung einer Nachbarmannschaft oder leer.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> SpiellokalAsync(string mannschaft, string lokal, string datum, string nachbar) => DatenAendernAsync(d =>
    {
        if (Name(d, mannschaft) is not string name)
        {
            return $"Keine Mannschaft „{mannschaft}“.";
        }

        string l = lokal.Trim();
        if (!Mannschaftsdaten.Spiellokale.Contains(l))
        {
            return $"Kein Spiellokal „{lokal}“ (leer oder 1 bis 5).";
        }

        Spiellokaldaten daten = d.SpiellokaleLesen(name);
        if (datum.Trim().Length > 0)
        {
            if (Datum(datum) is not DateOnly tag || !daten.Heimtage.Any(h => DateOnly.FromDateTime(h.Termin) == tag))
            {
                return $"{name} hat am „{datum}“ keinen Heimtermin.";
            }

            daten = daten with { Heimtage = daten.Heimtage.Select(h => DateOnly.FromDateTime(h.Termin) == tag ? (h.Termin, l) : h).ToList() };
        }
        else if (nachbar.Trim().Length > 0)
        {
            if (!daten.Nachbarn.Any(n => Gleich(n.Bezeichnung, nachbar)))
            {
                return $"{name} hat keine Nachbarmannschaft „{nachbar}“. Vorhanden: {string.Join(", ", daten.Nachbarn.Select(n => n.Bezeichnung))}.";
            }

            daten = daten with { Nachbarn = daten.Nachbarn.Select(n => Gleich(n.Bezeichnung, nachbar) ? (n.Bezeichnung, l) : n).ToList() };
        }
        else
        {
            daten = daten with { Standardlokal = l };
        }

        d.SpiellokaleSpeichern(name, daten);
        return null;
    });

    /// <summary>Setzt den Koppelwunsch an einem Heimtermin (zwei Heimspiele an einem Tag bzw. Doppelspieltag).</summary>
    /// <param name="mannschaft">Name der Mannschaft.</param>
    /// <param name="datum">Tag des Heimtermins.</param>
    /// <param name="wunsch">keiner, moeglich, gewuenscht, hoch, doppel_moeglich, doppel_gewuenscht, doppel_hoch.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> HeimkoppelAsync(string mannschaft, string datum, string wunsch) => DatenAendernAsync(d =>
    {
        if (Name(d, mannschaft) is not string name)
        {
            return $"Keine Mannschaft „{mannschaft}“.";
        }

        if (Koppelwuensche.FirstOrDefault(k => Gleich(k.Name, wunsch)) is not { Name: not null } gewaehlt)
        {
            return $"Kein Wunsch „{wunsch}“. Möglich: {string.Join(", ", Koppelwuensche.Select(k => k.Name))}.";
        }

        if (Datum(datum) is not DateOnly tag || !d.Heimtage(name).Any(h => h.IstHeimtag && DateOnly.FromDateTime(h.Datum) == tag))
        {
            return $"{name} hat am „{datum}“ keinen Heimtermin.";
        }

        // Je Tag ein Wunsch; die übrigen Tage behalten ihren.
        var jeTag = new Dictionary<DateOnly, Koppelwunsch>();
        foreach (Heimkoppel k in d.Heimkoppeln(name))
        {
            jeTag[k.Tag1] = k.Option1;
            if (k.Tag2 is DateOnly zweiter)
            {
                jeTag[zweiter] = k.Option2;
            }
        }

        jeTag[tag] = gewaehlt.Wunsch;
        d.HeimkoppelnSpeichern(name, jeTag.Where(e => e.Value != Koppelwunsch.Keiner).Select(e => new Heimkoppel(e.Key, null, e.Value, Koppelwunsch.Keiner)).ToList());
        return null;
    });

    /// <summary>Legt eine Auswärtskoppel an oder ändert ihre Art.</summary>
    /// <param name="mannschaft">Die reisende Mannschaft.</param>
    /// <param name="gegnerA">Erster Gastgeber.</param>
    /// <param name="gegnerB">Zweiter Gastgeber.</param>
    /// <param name="art">am gleichen Tag, mit Übernachtung, am gleichen Tag oder mit Übernachtung.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> AuswaertskoppelAsync(string mannschaft, string gegnerA, string gegnerB, string art) => DatenAendernAsync(d =>
    {
        if (Name(d, mannschaft) is not string name)
        {
            return $"Keine Mannschaft „{mannschaft}“.";
        }

        int index = Auswaertskoppeldaten.Arten.ToList().FindIndex(a => Gleich(a, art));
        if (index < 0)
        {
            return $"Keine Art „{art}“. Möglich: {string.Join(", ", Auswaertskoppeldaten.Arten)}.";
        }

        if (Name(d, gegnerA) is not string a || Name(d, gegnerB) is not string b)
        {
            return $"Keine Mannschaft „{(Name(d, gegnerA) is null ? gegnerA : gegnerB)}“.";
        }

        Auswaertskoppeldaten? bisher = d.Auswaertskoppeln(name).FirstOrDefault(k => (k.MannschaftA == a && k.MannschaftB == b) || (k.MannschaftA == b && k.MannschaftB == a));
        var neu = new Auswaertskoppeldaten(a, b, index);
        if (d.AuswaertskoppelPruefen(name, bisher, neu) is string fehler)
        {
            return fehler;
        }

        d.AuswaertskoppelSpeichern(name, bisher, neu);
        return null;
    });

    /// <summary>Löscht eine Auswärtskoppel.</summary>
    /// <param name="mannschaft">Die reisende Mannschaft.</param>
    /// <param name="gegnerA">Erster Gastgeber.</param>
    /// <param name="gegnerB">Zweiter Gastgeber.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> AuswaertskoppelLoeschenAsync(string mannschaft, string gegnerA, string gegnerB) => DatenAendernAsync(d =>
    {
        if (Name(d, mannschaft) is not string name)
        {
            return $"Keine Mannschaft „{mannschaft}“.";
        }

        if (d.Auswaertskoppeln(name).FirstOrDefault(k => (Gleich(k.MannschaftA, gegnerA) && Gleich(k.MannschaftB, gegnerB)) || (Gleich(k.MannschaftA, gegnerB) && Gleich(k.MannschaftB, gegnerA))) is not { } koppel)
        {
            return $"{name} hat keine Auswärtskoppel mit „{gegnerA}“ und „{gegnerB}“.";
        }

        d.AuswaertskoppelLoeschen(name, koppel);
        return null;
    });

    /// <summary>Legt eine Nachbarmannschaft an oder ändert sie.</summary>
    /// <param name="mannschaft">Die Mannschaft der Staffel.</param>
    /// <param name="bisher">Bezeichnung der bestehenden Nachbarmannschaft oder leer für eine neue.</param>
    /// <param name="name">Name.</param>
    /// <param name="art">Art (z. B. Herren, Damen, Jugend).</param>
    /// <param name="nummer">Mannschaftsnummer.</param>
    /// <param name="spiellokal">Spiellokal (leer, 1–5).</param>
    /// <param name="keineParallelenSpiele">Gleichzeitige Spiele mit ihr vermeiden.</param>
    /// <param name="paralleleHeimspiele">Gleichzeitige Heimspiele mit ihr erwünscht.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> NachbarmannschaftAsync(string mannschaft, string bisher, string name, string art, int nummer, string spiellokal, bool keineParallelenSpiele, bool paralleleHeimspiele) => DatenAendernAsync(d =>
    {
        if (Name(d, mannschaft) is not string m)
        {
            return $"Keine Mannschaft „{mannschaft}“.";
        }

        int? index = null;
        if (bisher.Trim().Length > 0)
        {
            index = Nachbarindex(d, m, bisher);
            if (index is null)
            {
                return $"{m} hat keine Nachbarmannschaft „{bisher}“.";
            }
        }

        if (!Mannschaftsdaten.Spiellokale.Contains(spiellokal.Trim()))
        {
            return $"Kein Spiellokal „{spiellokal}“ (leer oder 1 bis 5).";
        }

        Nachbarmannschaftsentwurf entwurf = d.NachbarmannschaftBearbeiten(m, index);
        entwurf.Name = name.Trim();
        entwurf.Art = art.Trim();
        entwurf.Nummer = Math.Max(1, nummer);
        entwurf.Spiellokal = spiellokal.Trim();
        entwurf.KeineParallelenSpiele = keineParallelenSpiele;
        entwurf.ParalleleHeimspiele = paralleleHeimspiele;
        if (d.NachbarmannschaftPruefen(m, index, entwurf) is string fehler)
        {
            return fehler;
        }

        d.NachbarmannschaftUebernehmen(m, index, entwurf);
        return null;
    });

    /// <summary>Löscht eine Nachbarmannschaft.</summary>
    /// <param name="mannschaft">Die Mannschaft der Staffel.</param>
    /// <param name="nachbar">Bezeichnung oder Name der Nachbarmannschaft.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> NachbarmannschaftLoeschenAsync(string mannschaft, string nachbar) => DatenAendernAsync(d =>
    {
        if (Name(d, mannschaft) is not string m || Nachbarindex(d, m, nachbar) is not int index)
        {
            return $"Keine Nachbarmannschaft „{nachbar}“ bei „{mannschaft}“.";
        }

        d.NachbarmannschaftLoeschen(m, index);
        return null;
    });

    /// <summary>Trägt eine Begegnung einer Nachbarmannschaft ein (ersetzt eine zum selben Termin) oder löscht sie.</summary>
    /// <param name="mannschaft">Die Mannschaft der Staffel.</param>
    /// <param name="nachbar">Bezeichnung oder Name der Nachbarmannschaft.</param>
    /// <param name="termin">Termin, TT.MM.JJJJ HH:MM.</param>
    /// <param name="gegner">Gegner der Nachbarmannschaft; leer = Begegnung zu diesem Termin löschen.</param>
    /// <param name="heimspiel">Die Nachbarmannschaft spielt zu Hause.</param>
    /// <param name="spiellokal">Spiellokal (leer, 1–5).</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> NachbarspielAsync(string mannschaft, string nachbar, string termin, string gegner, bool heimspiel, string spiellokal) => DatenAendernAsync(d =>
    {
        if (Name(d, mannschaft) is not string m || Nachbarindex(d, m, nachbar) is not int index)
        {
            return $"Keine Nachbarmannschaft „{nachbar}“ bei „{mannschaft}“.";
        }

        string[] teile = termin.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (teile.Length != 2 || Datum(teile[0]) is not DateOnly tag || Zeit(teile[1]) is not TimeOnly zeit)
        {
            return $"„{termin}“ ist kein Termin (TT.MM.JJJJ HH:MM).";
        }

        DateTime zeitpunkt = tag.ToDateTime(zeit);
        Nachbarmannschaftsentwurf entwurf = d.NachbarmannschaftBearbeiten(m, index);
        int? vorhanden = entwurf.Spiele().FirstOrDefault(z => z.Spiel.Termin == zeitpunkt)?.Index;
        if (gegner.Trim().Length == 0)
        {
            if (vorhanden is not int weg)
            {
                return $"Zu „{termin}“ gibt es keine Begegnung.";
            }

            entwurf.SpielLoeschen(weg);
        }
        else
        {
            string eigener = entwurf.Name.Trim();
            var spiel = new Nachbarspieldaten(zeitpunkt, heimspiel ? eigener : gegner.Trim(), heimspiel ? gegner.Trim() : eigener, spiellokal.Trim());
            if (entwurf.SpielPruefen(vorhanden, spiel, gegner) is string fehler)
            {
                return fehler;
            }

            entwurf.SpielSpeichern(vorhanden, spiel, heimspiel);
        }

        d.NachbarmannschaftUebernehmen(m, index, entwurf);
        return null;
    });

    /// <summary>Setzt das Heimrecht: Zahl der Heimspiele in der Vorrunde und/oder die Runde des Heimspiels gegen einen Gegner.</summary>
    /// <param name="mannschaft">Name der Mannschaft.</param>
    /// <param name="heimspieleVorrunde">Heimspiele in der Vorrunde; -1 = automatisch; <c>null</c> = unverändert.</param>
    /// <param name="gegner">Gegner oder leer.</param>
    /// <param name="runde">Heimspiel gegen ihn: vorrunde, rueckrunde oder egal.</param>
    /// <returns>Das Ergebnis.</returns>
    public Task<Aktionsergebnis> HeimrechtAsync(string mannschaft, int? heimspieleVorrunde, string gegner, string runde) => DatenAendernAsync(d =>
    {
        if (Name(d, mannschaft) is not string name)
        {
            return $"Keine Mannschaft „{mannschaft}“.";
        }

        Heimrecht heimrecht = d.HeimrechtLesen(name);
        if (heimspieleVorrunde is int anzahl)
        {
            if (anzahl < Heimrecht.Automatisch)
            {
                return "Die Zahl der Heimspiele muss -1 (automatisch) oder größer sein.";
            }

            heimrecht = heimrecht with { HeimspieleVorrunde = anzahl };
        }

        if (gegner.Trim().Length > 0)
        {
            if (heimrecht.Gegner.Keys.FirstOrDefault(g => Gleich(g, gegner)) is not string g)
            {
                return $"Kein Gegner „{gegner}“.";
            }

            int? wert = runde.Trim().ToLowerInvariant() switch
            {
                "vorrunde" => Heimrecht.Vorrunde,
                "rueckrunde" or "rückrunde" => Heimrecht.Rueckrunde,
                "egal" or "" => Heimrecht.Egal,
                _ => null,
            };
            if (wert is not int w)
            {
                return $"Keine Runde „{runde}“ (vorrunde, rueckrunde, egal).";
            }

            heimrecht = heimrecht with { Gegner = new Dictionary<string, int>(heimrecht.Gegner) { [g] = w } };
        }

        d.HeimrechtSpeichern(name, heimrecht);
        return null;
    });

    private static int? Nachbarindex(Datenbearbeitung d, string mannschaft, string nachbar) =>
        d.Nachbarmannschaften(mannschaft).LastOrDefault(n => Gleich(n.Bezeichnung, nachbar) || Gleich(d.NachbarmannschaftBearbeiten(mannschaft, n.Index).Name, nachbar))?.Index;

    private static string Koppeltext(Heimkoppel k) =>
        k.Tag2 is DateOnly zweiter && k.Option2 != Koppelwunsch.Keiner
            ? $"{Tag(k.Tag1)}: {Heimkoppel.Name(k.Option1)}; {Tag(zweiter)}: {Heimkoppel.Name(k.Option2)}"
            : $"{Tag(k.Tag1)}: {Heimkoppel.Name(k.Option1)}";

    private static string Nachbartext(Datenbearbeitung d, string mannschaft, Nachbarmannschaftseintrag n)
    {
        IReadOnlyList<Nachbarspielzeile> spiele = d.NachbarmannschaftBearbeiten(mannschaft, n.Index).Spiele();
        return n.Bezeichnung + (spiele.Count == 0
            ? string.Empty
            : ": " + string.Join("; ", spiele.Select(z => z.Spiel.Termin.ToString("dd.MM.yyyy HH:mm", Deutsch) + " " + z.Spiel.Heim + " – " + z.Spiel.Gast)));
    }

    private static string Heimrechttext(Heimrecht h)
    {
        string anzahl = h.HeimspieleVorrunde == Heimrecht.Automatisch
            ? "Heimspiele in der Vorrunde: automatisch"
            : string.Create(CultureInfo.InvariantCulture, $"Heimspiele in der Vorrunde: {h.HeimspieleVorrunde}");
        IEnumerable<string> fest = h.Gegner.Where(g => g.Value != Heimrecht.Egal).Select(g => $"{g.Key}: Heimspiel in der {(g.Value == Heimrecht.Vorrunde ? "Vorrunde" : "Rückrunde")}");
        return string.Join("; ", fest.Prepend(anzahl));
    }
}
