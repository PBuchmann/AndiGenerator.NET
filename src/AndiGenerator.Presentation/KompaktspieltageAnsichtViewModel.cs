// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Domain.Turniere;
using AndiGenerator.Engine.Turniere;
using AndiGenerator.Persistence.Geo;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AndiGenerator.Presentation;

/// <summary>
/// Ansicht „Kompaktspieltage“ (nicht im Original): Meldungen und Plan als Raster, Karte der Spielorte mit den Fahrten,
/// Kennzahlen, Spieltage und Hinweise. Ein Klick auf einen Sonntag, eine Mannschaft oder einen Spieltag zeigt dessen
/// Fahrten; ein zweiter Klick wieder alle. Entfernungen in Luftlinie.
/// </summary>
public sealed class KompaktspieltageAnsichtViewModel : Document
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private readonly HauptfensterViewModel hauptfenster;
    private Kompaktauswertung? auswertung;
    private (char Art, string Schluessel) auswahl = ('-', string.Empty);
    private bool eingeschaltet;
    private string kopfzeile = string.Empty;
    private string kartentitel = string.Empty;
    private Kartenbild karte = Kartenbild.Leer;
    private IReadOnlyList<Kachelwert> kennzahlen = [];
    private IReadOnlyList<Kompaktspalte> spalten = [];
    private IReadOnlyList<Kompaktrasterzeile> zeilen = [];
    private IReadOnlyList<Kompaktkmzeile> kmZeilen = [];
    private IReadOnlyList<Kompaktspieltagkarte> spieltage = [];
    private IReadOnlyList<string> hinweise = [];

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="hauptfenster">Hauptfenster.</param>
    public KompaktspieltageAnsichtViewModel(HauptfensterViewModel hauptfenster)
    {
        ArgumentNullException.ThrowIfNull(hauptfenster);
        this.hauptfenster = hauptfenster;
        Id = "Kompaktspieltage";
        Title = "Kompaktspieltage";
        CanClose = true;
        CanFloat = true;
        EinschaltenCommand = new AsyncRelayCommand(() => hauptfenster.KompaktspieltageSchaltenAsync(Kompaktmodus.Bestaetigt));
        AusschaltenCommand = new AsyncRelayCommand(() => hauptfenster.KompaktspieltageSchaltenAsync(Kompaktmodus.Aus));
        TagWaehlenCommand = new RelayCommand<Kompaktrasterzeile>(z => Waehlen('T', z?.Tag.ToString("O", CultureInfo.InvariantCulture)));
        MannschaftWaehlenCommand = new RelayCommand<string>(n => Waehlen('M', n));
        SpieltagWaehlenCommand = new RelayCommand<Kompaktspieltagkarte>(k => Waehlen('S', k?.Nummer.ToString(CultureInfo.InvariantCulture)));
        Laden();
    }

    /// <summary>Holt die Erklärung der Kompaktspieltage (auch für die Einrichtung).</summary>
    public static string Erklaerung =>
        "Bei Kompaktspieltagen treffen sich drei oder vier Mannschaften an einem Sonntag in einer Halle: beim 3er-Spieltag spielt "
        + "jede gegen jede (drei Spiele), beim 4er-Spieltag spielen der Ausrichter und eine zweite Mannschaft je gegen die beiden "
        + "anderen (vier Spiele in zwei Runden, zwei Spiele parallel). Jede Mannschaft soll mindestens einen Heimspieltag "
        + "ausrichten. Die Meldungen stehen in den Terminwünschen aus click-TT: Uhrzeit am Sonntag = Heimspieltag möglich, ohne "
        + "Eintrag = auswärts möglich, „frei“ = kann nicht, max. Heimspiele 2 = 4er-Spieltag möglich; „Nur Heim: TT.MM., …“ in "
        + "den Bemerkungen. Entfernungen werden in Luftlinie zwischen den Hallen berechnet.";

    /// <summary>Holt den Befehl „Kompaktspieltage einschalten“.</summary>
    public IAsyncRelayCommand EinschaltenCommand { get; }

    /// <summary>Holt den Befehl „Kompaktspieltage ausschalten“.</summary>
    public IAsyncRelayCommand AusschaltenCommand { get; }

    /// <summary>Holt den Befehl „Sonntag wählen“.</summary>
    public IRelayCommand<Kompaktrasterzeile> TagWaehlenCommand { get; }

    /// <summary>Holt den Befehl „Mannschaft wählen“.</summary>
    public IRelayCommand<string> MannschaftWaehlenCommand { get; }

    /// <summary>Holt den Befehl „Spieltag wählen“.</summary>
    public IRelayCommand<Kompaktspieltagkarte> SpieltagWaehlenCommand { get; }

    /// <summary>Holt einen Wert, der angibt, ob die Staffel mit Kompaktspieltagen geplant wird.</summary>
    public bool Eingeschaltet
    {
        get => eingeschaltet;
        private set => SetProperty(ref eingeschaltet, value);
    }

    /// <summary>Holt die Kopfzeile, z. B. „Jugend 19 Verbandsoberliga · 15 Sonntage · 9 Mannschaften“.</summary>
    public string Kopfzeile
    {
        get => kopfzeile;
        private set => SetProperty(ref kopfzeile, value);
    }

    /// <summary>Holt die Kennzahlen.</summary>
    public IReadOnlyList<Kachelwert> Kennzahlen
    {
        get => kennzahlen;
        private set => SetProperty(ref kennzahlen, value);
    }

    /// <summary>Holt die Spaltenköpfe des Rasters.</summary>
    public IReadOnlyList<Kompaktspalte> Spalten
    {
        get => spalten;
        private set => SetProperty(ref spalten, value);
    }

    /// <summary>Holt die Zeilen des Rasters (je Sonntag).</summary>
    public IReadOnlyList<Kompaktrasterzeile> Zeilen
    {
        get => zeilen;
        private set => SetProperty(ref zeilen, value);
    }

    /// <summary>Holt die Karte.</summary>
    public Kartenbild Karte
    {
        get => karte;
        private set => SetProperty(ref karte, value);
    }

    /// <summary>Holt die Überschrift der Karte, z. B. „Fahrten von Konz · 681 km“.</summary>
    public string Kartentitel
    {
        get => kartentitel;
        private set => SetProperty(ref kartentitel, value);
    }

    /// <summary>Holt die Zeilen „km je Mannschaft“, die weiteste zuerst.</summary>
    public IReadOnlyList<Kompaktkmzeile> KmZeilen
    {
        get => kmZeilen;
        private set => SetProperty(ref kmZeilen, value);
    }

    /// <summary>Holt die Karten der Spieltage.</summary>
    public IReadOnlyList<Kompaktspieltagkarte> Spieltage
    {
        get => spieltage;
        private set => SetProperty(ref spieltage, value);
    }

    /// <summary>Holt die Hinweise: Verstöße, fehlende Meldungen und Bemerkungen der Vereine.</summary>
    public IReadOnlyList<string> Hinweise
    {
        get => hinweise;
        private set => SetProperty(ref hinweise, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob Hallen ohne bekannte Lage fehlen (dann Hinweis auf die Terminwünsche).</summary>
    public bool FehlenLagen => auswertung?.Mannschaften.Any(m => m.Lage is null) ?? false;

    /// <summary>Liest Staffel und Plan neu (nach Öffnen oder Änderung).</summary>
    internal void Laden()
    {
        Eingeschaltet = hauptfenster.Kompaktmodus is Kompaktmodus.Ein or Kompaktmodus.Bestaetigt;
        auswertung = Eingeschaltet ? hauptfenster.Kompaktauswertung() : null;
        if (auswertung is null)
        {
            Kopfzeile = hauptfenster.Staffelname;
            Kennzahlen = [];
            Spalten = [];
            Zeilen = [];
            KmZeilen = [];
            Spieltage = [];
            Hinweise = [];
            Karte = Kartenbild.Leer;
            OnPropertyChanged(nameof(FehlenLagen));
            return;
        }

        Kompaktauswertung a = auswertung;
        Kopfzeile = string.Create(Deutsch, $"{hauptfenster.Staffelname} · {a.Tage.Count} Sonntage · {a.Mannschaften.Count} Mannschaften");
        string Zahl(double z) => z.ToString("N0", Deutsch);
        Kompaktmannschaft? weiteste = a.Mannschaften.MaxBy(m => m.Km);
        Kennzahlen =
        [
            new Kachelwert(Zahl(a.KmGesamt), "km gesamt (Luftlinie, hin und zurück)"),
            new Kachelwert(weiteste is null ? "–" : Zahl(weiteste.Km), weiteste is null ? "km weiteste Mannschaft" : $"km weiteste Mannschaft ({weiteste.Kurzname})"),
            new Kachelwert(Zahl(a.OhneHeimspieltag), "Mannschaften ohne Heimspieltag"),
            new Kachelwert(Zahl(a.Verstoesse), "Verstöße gegen Meldungen"),
            new Kachelwert(Zahl(a.GroessterAbstand), "größter Abstand im Stand (Spiele)"),
            new Kachelwert(string.Create(Deutsch, $"{a.MeisteAmTag} / {a.Mannschaften.Count}"), "Mannschaften am vollsten Spieltag"),
        ];
        Hinweise = HinweiseBilden(a);
        OnPropertyChanged(nameof(FehlenLagen));
        AuswahlZeigen();
    }

    private static List<string> HinweiseBilden(Kompaktauswertung a)
    {
        var liste = new List<string>(a.Befunde.Select(b => "! " + b));
        liste.AddRange(a.Mannschaften.Where(m => m.Lage is null).Select(m => $"? {m.Name}: Halle ohne bekannte Lage – Terminwünsche (PDF) in der Einrichtung laden."));
        liste.AddRange(a.Mannschaften.Where(m => m.Bemerkung.Length > 0).Select(m => $"„ {m.Kurzname}: {m.Bemerkung}"));
        return liste;
    }

    private static string Meldungsart(Turnierbereitschaft? b) => b switch
    {
        null => "unbekannt",
        Turnierbereitschaft.Heim => "heim",
        Turnierbereitschaft.Auswaerts => "auswaerts",
        Turnierbereitschaft.Heim | Turnierbereitschaft.Auswaerts => "beides",
        _ => "nicht",
    };

    private static string Kurz(Kompaktauswertung a, string name) => a.Mannschaften.FirstOrDefault(m => m.Name == name)?.Kurzname ?? name;

    private static Kompaktrasterzeile Zeile(Kompaktauswertung a, DateOnly tag, bool gewaehlt, string? team)
    {
        var zellen = new List<Kompaktzelle>();
        foreach (Kompaktmannschaft m in a.Mannschaften)
        {
            Turnierbereitschaft? b = m.Meldung?.An(tag);
            Kompaktspieltag? s = a.Spieltage.FirstOrDefault(x => x.Tag == tag && x.Mannschaften.Contains(m.Name));
            bool ausrichter = s is not null && s.Ausrichter == m.Name;
            bool verstoss = s is not null && b is Turnierbereitschaft bb
                && !bb.HasFlag(ausrichter ? Turnierbereitschaft.Heim : Turnierbereitschaft.Auswaerts);
            string zeichen = (s, b) switch
            {
                (not null, _) => ausrichter ? "★" : "●",
                (null, null) => "?",
                (null, Turnierbereitschaft.None) => "✗",
                _ => string.Empty,
            };
            string meldung = b switch
            {
                null => "keine Meldung",
                Turnierbereitschaft.None => "kann nicht",
                Turnierbereitschaft.Heim => "nur Heim",
                Turnierbereitschaft.Auswaerts => "nur auswärts",
                _ => "Heim oder auswärts",
            };
            string rolle = string.Empty;
            if (s is not null)
            {
                rolle = ausrichter ? " · richtet aus" : " · Gast bei " + Kurz(a, s.Ausrichter ?? "?");
            }

            zellen.Add(new Kompaktzelle(Meldungsart(b), zeichen, verstoss, team is not null && team != m.Name, string.Create(Deutsch, $"{m.Name}, {tag:dd.MM.yyyy}: {meldung}{rolle}")));
        }

        return new Kompaktrasterzeile(tag, tag.ToString("dd.MM.", Deutsch), gewaehlt, zellen);
    }

    private static Kompaktspieltagkarte Spieltagkarte(int nummer, Kompaktspieltag s, Kompaktauswertung a, bool gewaehlt)
    {
        string form = s.Form switch
        {
            Turnierform.Dreier => "3er",
            Turnierform.Vierer => "4er",
            Turnierform.Einzel => "Einzel",
            _ => "unbekannt",
        };
        string halle = a.Mannschaften.FirstOrDefault(m => m.Name == s.Ausrichter)?.Halle ?? string.Empty;
        string verlegen = string.Empty;
        if (s.HalleVerlegen.Count > 0)
        {
            string ziel = halle.Length > 0 ? " → " + halle : string.Empty;
            verlegen = "In click-TT Halle verlegen: " + string.Join(", ", s.HalleVerlegen.Select(g => Kurz(a, g.Heim) + " – " + Kurz(a, g.Gast))) + ziel;
        }

        return new Kompaktspieltagkarte(
            nummer,
            s.Tag.ToString("ddd dd.MM.yyyy", Deutsch),
            form,
            (s.Form == Turnierform.Einzel ? "Heim: " : "★ ") + (s.Ausrichter ?? "kein passender Ausrichter"),
            s.Km.ToString("N0", Deutsch) + " km",
            s.Spiele.Select(g => string.Create(Deutsch, $"{g.Zeitpunkt:HH:mm}  {Kurz(a, g.Heim)} – {Kurz(a, g.Gast)}")).ToList(),
            verlegen,
            string.Join(", ", s.Gaeste.Select(g => Kurz(a, g))) + " reisen an",
            s.Hinweise,
            gewaehlt);
    }

    private void Waehlen(char art, string? schluessel)
    {
        if (schluessel is null)
        {
            return;
        }

        auswahl = auswahl == (art, schluessel) ? ('-', string.Empty) : (art, schluessel);
        AuswahlZeigen();
    }

    private void AuswahlZeigen()
    {
        if (auswertung is not Kompaktauswertung a)
        {
            return;
        }

        string? team = auswahl.Art == 'M' ? auswahl.Schluessel : null;
        DateOnly? tag = auswahl.Art == 'T' ? DateOnly.ParseExact(auswahl.Schluessel, "O", CultureInfo.InvariantCulture) : null;
        int? nummer = auswahl.Art == 'S' ? int.Parse(auswahl.Schluessel, CultureInfo.InvariantCulture) : null;
        List<(int Nummer, Kompaktspieltag Spieltag)> alle = a.Spieltage.Select((s, i) => (i, s)).ToList();
        List<Kompaktspieltag> gewaehlt = alle
            .Where(x => (tag is null || x.Spieltag.Tag == tag) && (nummer is null || x.Nummer == nummer) && (team is null || x.Spieltag.Gaeste.Contains(team)))
            .Select(x => x.Spieltag)
            .ToList();

        Spalten = a.Mannschaften.Select(m => new Kompaktspalte(m.Name, m.Kurzname, m.Name == team)).ToList();
        Zeilen = a.Tage.Select(t => Zeile(a, t, tag == t, team)).ToList();
        double weiteste = Math.Max(1, a.Mannschaften.Select(m => m.Km).DefaultIfEmpty(0).Max());
        KmZeilen = a.Mannschaften.OrderByDescending(m => m.Km)
            .Select(m => new Kompaktkmzeile(m.Name, m.Kurzname, string.Create(Deutsch, $"{m.Ausgerichtet}× Ausrichter"), m.Km.ToString("N0", Deutsch), m.Km / weiteste, m.Name == team))
            .ToList();
        Spieltage = alle.Select(x => Spieltagkarte(x.Nummer, x.Spieltag, a, (tag is not null && x.Spieltag.Tag == tag) || nummer == x.Nummer)).ToList();
        Kartentitel = (auswahl.Art, team) switch
        {
            ('M', string t) => string.Create(Deutsch, $"Fahrten von {Kurz(a, t)} · {a.Mannschaften.First(m => m.Name == t).Km:N0} km"),
            ('T', _) => gewaehlt.Count == 0 ? string.Create(Deutsch, $"Am {tag:dd.MM.yyyy} kein Spieltag") : string.Create(Deutsch, $"Fahrten am {tag:dd.MM.yyyy}"),
            ('S', _) => gewaehlt.Count == 0 ? "Spieltag" : string.Create(Deutsch, $"Spieltag {gewaehlt[0].Tag:dd.MM.yyyy} bei {Kurz(a, gewaehlt[0].Ausrichter ?? string.Empty)}"),
            _ => "Alle Fahrten der Halbrunde",
        };
        bool alleZeigen = auswahl.Art == '-';
        List<Kompaktspieltag> sichtbar = alleZeigen ? a.Spieltage.ToList() : gewaehlt;
        var fahrten = sichtbar.Where(s => s.Ausrichter is not null)
            .SelectMany(s => s.Gaeste.Where(g => team is null || g == team).Select(g => (Von: g, Nach: s.Ausrichter!, Kraeftig: !alleZeigen)))
            .ToList();
        var ausrichter = sichtbar.Select(s => s.Ausrichter).OfType<string>().ToHashSet(StringComparer.Ordinal);
        List<Kartenort> orte = a.Mannschaften
            .Where(m => m.Lage is not null)
            .Select(m => new Kartenort(
                m.Name,
                m.Kurzname,
                m.Lage.GetValueOrDefault(),
                !alleZeigen && ausrichter.Contains(m.Name),
                team is not null && m.Name != team && !ausrichter.Contains(m.Name),
                m.Halle.Length > 0 ? $"{m.Name} – {m.Halle}" : m.Name))
            .ToList();
        Karte = Kartenbau.Bauen(orte, fahrten, Kartendaten.Laender, Kartendaten.Staedte);
    }
}
