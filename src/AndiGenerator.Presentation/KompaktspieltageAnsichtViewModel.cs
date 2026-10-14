// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Domain.Turniere;
using AndiGenerator.Engine.Turniere;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AndiGenerator.Presentation;

/// <summary>
/// Ansicht „Kompaktspieltage“ (nicht im Original): Meldungen und Plan als Raster, Karte der Spielorte mit den Fahrten,
/// Pläne (Plan aus click-TT und Varianten des Planers), Spieltage und Hinweise. Ein Klick auf einen Sonntag, eine Mannschaft oder einen Spieltag zeigt dessen
/// Fahrten; ein zweiter Klick wieder alle. Entfernungen in Luftlinie.
/// </summary>
public sealed partial class KompaktspieltageAnsichtViewModel : Document
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private readonly HauptfensterViewModel hauptfenster;
    private Kompaktauswertung? auswertung;
    private (char Art, string Schluessel) auswahl = ('-', string.Empty);
    private bool eingeschaltet;
    private string kopfzeile = string.Empty;
    private string kartentitel = string.Empty;
    private Kartenbild karte = Kartenbild.Leer;
    private IReadOnlyList<Kompaktplanwahl> plaene = [];
    private IReadOnlyList<string> planerhinweise = [];
    private IReadOnlyList<string> mannschaftsnamen = [];
    private bool rechnet;
    private bool angehalten;
    private double fortschritt;
    private string fortschrittText = string.Empty;
    private string erlaeuterungstitel = string.Empty;
    private IReadOnlyList<string> erlaeuterung = [];
    private IReadOnlyList<Kompaktspalte> spalten = [];
    private IReadOnlyList<Kompaktrasterzeile> zeilen = [];
    private IReadOnlyList<Kompaktkmzeile> kmZeilen = [];
    private IReadOnlyList<Kompaktspieltagkarte> spieltage = [];
    private IReadOnlyList<string> hinweise = [];
    private (IReadOnlyList<string> Spitze, IReadOnlyList<string> Keller) gruppen = ([], []);

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
        PlanWaehlenCommand = new RelayCommand<Kompaktplanwahl>(w => hauptfenster.KompaktplanWaehlen(w?.Variante));
        ZelleWaehlenCommand = new RelayCommand<Kompaktzelle>(z => CursorSetzen(z?.Mannschaft, z?.Tag));
        FeldSetzenCommand = new AsyncRelayCommand<string>(FeldSetzenAsync, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        TerminSpeichernCommand = new AsyncRelayCommand(TerminSpeichernAsync);
        TagAnlegenCommand = new AsyncRelayCommand(TagAnlegenAsync);
        LokalFuerAlleCommand = new AsyncRelayCommand(LokalFuerAlleAsync);
        ZeitLoeschenCommand = new AsyncRelayCommand(ZeitLoeschenAsync);
        Laden();
    }

    /// <summary>Holt die Erklärung der Kompaktspieltage (auch für die Einrichtung).</summary>
    public static string Erklaerung =>
        "Bei Kompaktspieltagen treffen sich drei oder vier Mannschaften an einem Sonntag in einer Halle: beim 3er-Spieltag spielt "
        + "jede gegen jede (drei Spiele), beim 4er-Spieltag spielen der Ausrichter und eine zweite Mannschaft je gegen die beiden "
        + "anderen (vier Spiele in zwei Runden, zwei Spiele parallel). Jede Mannschaft soll mindestens einen Heimspieltag "
        + "ausrichten. Die Meldungen stehen in den Terminwünschen aus click-TT: Uhrzeit am Sonntag = Heimspieltag möglich, ohne "
        + "Eintrag = auswärts möglich, „frei“ = kann nicht, max. Heimspiele 2 = 4er-Spieltag möglich; „Nur Heim: TT.MM., …“ in "
        + "den Bemerkungen. Im Raster lassen sich die Meldungen auch direkt erfassen (Schalter „Bearbeiten“). Entfernungen werden in Luftlinie zwischen den Hallen berechnet.";

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

    /// <summary>Holt den Befehl „Plan merken…“ für den gewählten Plan.</summary>
    public IAsyncRelayCommand MerkenCommand => hauptfenster.PlanMerkenCommand;

    /// <summary>Holt den Befehl „Plan zeigen“ (Variante oder Plan aus click-TT).</summary>
    public IRelayCommand<Kompaktplanwahl> PlanWaehlenCommand { get; }

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

    /// <summary>Holt einen Wert, der angibt, ob der Planer gerade rechnet.</summary>
    public bool Rechnet
    {
        get => rechnet;
        private set => SetProperty(ref rechnet, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob die Berechnung angehalten ist (dann stehen die bisher besten Pläne zur Wahl).</summary>
    public bool Angehalten
    {
        get => angehalten;
        private set => SetProperty(ref angehalten, value);
    }

    /// <summary>Holt den Fortschritt der Berechnung in Prozent.</summary>
    public double Fortschritt
    {
        get => fortschritt;
        private set => SetProperty(ref fortschritt, value);
    }

    /// <summary>Holt, was die Berechnung gerade tut.</summary>
    public string FortschrittText
    {
        get => fortschrittText;
        private set => SetProperty(ref fortschrittText, value);
    }

    /// <summary>Holt die Überschrift der Erläuterung, z. B. „Bestes Gesamtpaket“.</summary>
    public string Erlaeuterungstitel
    {
        get => erlaeuterungstitel;
        private set => SetProperty(ref erlaeuterungstitel, value);
    }

    /// <summary>Holt die Erläuterung des gezeigten Plans: wofür er steht und wie er im Vergleich abschneidet.</summary>
    public IReadOnlyList<string> Erlaeuterung
    {
        get => erlaeuterung;
        private set => SetProperty(ref erlaeuterung, value);
    }

    /// <summary>Holt die Pläne zur Wahl: Plan aus click-TT und die Varianten der letzten Berechnung.</summary>
    public IReadOnlyList<Kompaktplanwahl> Plaene
    {
        get => plaene;
        private set => SetProperty(ref plaene, value);
    }

    /// <summary>Holt die Hinweise der letzten Berechnung.</summary>
    public IReadOnlyList<string> Planerhinweise
    {
        get => planerhinweise;
        private set
        {
            if (SetProperty(ref planerhinweise, value))
            {
                OnPropertyChanged(nameof(HatPlanerhinweise));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es Hinweise der Berechnung gibt.</summary>
    public bool HatPlanerhinweise => Planerhinweise.Count > 0;

    /// <summary>Holt die Namen der Mannschaften.</summary>
    public IReadOnlyList<string> Mannschaftsnamen
    {
        get => mannschaftsnamen;
        private set => SetProperty(ref mannschaftsnamen, value);
    }

    /// <summary>Holt die Spaltenköpfe des Rasters.</summary>
    public IReadOnlyList<Kompaktspalte> Spalten
    {
        get => spalten;
        private set
        {
            if (SetProperty(ref spalten, value))
            {
                OnPropertyChanged(nameof(Kopfhoehe));
            }
        }
    }

    /// <summary>Holt die Höhe der Spaltenköpfe: Platz für den längsten vollen Mannschaftsnamen (senkrecht), wie im Ausdruck.</summary>
    public double Kopfhoehe => Kompaktdruck.Kopfhoehe(spalten.Select(s => s.Name));

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
        private set
        {
            if (SetProperty(ref spieltage, value))
            {
                OnPropertyChanged(nameof(Spieltagbreite));
            }
        }
    }

    /// <summary>
    /// Holt die Breite der Spieltagskarten (alle gleich, damit die Reihen ordentlich bleiben): Platz für die längste Zeile aus
    /// Uhrzeit und Begegnung mit vollen Mannschaftsnamen, zwischen 310 und 560; Längeres bricht um (Peter, 14.10.2026).
    /// Geschätzt mit 6,9 je Zeichen bei 12,5 pt, dazu Innenabstand und Rand der Karte.
    /// </summary>
    public double Spieltagbreite =>
        Math.Clamp(Math.Ceiling((spieltage.SelectMany(s => s.Spiele).Select(z => z.Length).DefaultIfEmpty(0).Max() * 6.9) + 34), 310, 560);

    /// <summary>Holt die Hinweise: Verstöße, fehlende Meldungen und Bemerkungen der Vereine.</summary>
    public IReadOnlyList<string> Hinweise
    {
        get => hinweise;
        private set => SetProperty(ref hinweise, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob ein Plan gezeigt wird (aus click-TT oder berechnet); sonst entfallen die km je Mannschaft.</summary>
    public bool HatPlan => auswertung?.Spieltage.Count > 0;

    /// <summary>Holt einen Wert, der angibt, ob Hallen ohne bekannte Lage fehlen (dann Hinweis auf die Terminwünsche).</summary>
    public bool FehlenLagen => auswertung?.Mannschaften.Any(m => m.Lage is null) ?? false;

    /// <summary>Zeigt den Fortschritt der Berechnung.</summary>
    /// <param name="prozent">Erledigt in Prozent.</param>
    /// <param name="text">Was gerade geschieht.</param>
    internal void FortschrittZeigen(double prozent, string text)
    {
        Fortschritt = prozent;
        FortschrittText = string.Create(Deutsch, $"{prozent:N0} % – {text}");
    }

    /// <summary>Zeigt unter dem Balken, dass die Berechnung angehalten ist (der Balken bleibt stehen).</summary>
    /// <param name="text">Der Hinweis.</param>
    internal void PauseZeigen(string text) => FortschrittText = string.Create(Deutsch, $"{Fortschritt:N0} % – {text}");

    /// <summary>Liest Staffel und Plan neu (nach Öffnen oder Änderung).</summary>
    internal void Laden()
    {
        Eingeschaltet = hauptfenster.KompaktEingeschaltet;
        Rechnet = hauptfenster.KompaktRechnet;
        Angehalten = hauptfenster.KompaktPausiert;
        Planerhinweise = hauptfenster.KompaktPlanerhinweise;
        auswertung = Eingeschaltet ? hauptfenster.Kompaktauswertung() : null;
        Plaene = PlaeneBilden();
        Mannschaftsnamen = hauptfenster.KompaktMannschaften();
        if (auswertung is null)
        {
            Bearbeiten = false;
            Kopfzeile = hauptfenster.Staffelname;
            Spalten = [];
            Zeilen = [];
            KmZeilen = [];
            Spieltage = [];
            Hinweise = [];
            Erlaeuterungstitel = string.Empty;
            Erlaeuterung = [];
            Karte = Kartenbild.Leer;
            OnPropertyChanged(nameof(FehlenLagen));
            OnPropertyChanged(nameof(HatPlan));
            return;
        }

        Kompaktauswertung a = auswertung;
        (IReadOnlyList<string> spitze, IReadOnlyList<string> keller) = hauptfenster.KompaktGruppen();
        gruppen = (spitze, keller);
        Kompaktvariante? gezeigt = hauptfenster.GewaehlteKompaktvariante;
        if (gezeigt is null && !hauptfenster.HatClickTtPlan)
        {
            // Ohne Plan aus click-TT und noch ohne Varianten gibt es nichts zu erläutern.
            Erlaeuterungstitel = string.Empty;
            Erlaeuterung = [];
        }
        else
        {
            Erlaeuterungstitel = gezeigt?.Name ?? "Plan aus click-TT";
            Erlaeuterung = Kompakterlaeuterung.Zeilen(gezeigt, a, hauptfenster.Kompaktvarianten, spitze, keller);
        }

        Kopfzeile = string.Create(Deutsch, $"{hauptfenster.Staffelname} · {a.Tage.Count} mögliche Termine · {a.Mannschaften.Count} Mannschaften");
        Hinweise = [.. Planerhinweise.Select(h => "ℹ " + h), .. HinweiseBilden(a)];
        OnPropertyChanged(nameof(FehlenLagen));
        OnPropertyChanged(nameof(HatPlan));
        AuswahlZeigen();
    }

    private static List<string> HinweiseBilden(Kompaktauswertung a)
    {
        var liste = new List<string>(a.Befunde.Select(b => "! " + b));
        liste.AddRange(a.Mannschaften.Where(m => m.Lage is null).Select(m => $"? {m.Name}: Halle ohne bekannte Lage – Terminwünsche (PDF) in der Einrichtung laden."));
        liste.AddRange(a.Mannschaften.Where(m => m.Bemerkung.Length > 0).Select(m => $"„ {m.Name}: {m.Bemerkung}"));
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

    private static string Ausrichtertext(Kompaktmannschaft m) => m.EinzelHeim > 0
        ? string.Create(Deutsch, $"{m.Ausgerichtet}× Ausrichter, {m.EinzelHeim}× Einzel zuhause")
        : string.Create(Deutsch, $"{m.Ausgerichtet}× Ausrichter");

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
            string ziel = halle.Length > 0 ? " – in diese Halle:" : string.Empty;
            verlegen = "In click-TT Halle verlegen: " + string.Join(", ", s.HalleVerlegen.Select(g => g.Heim + " – " + g.Gast)) + ziel;
        }

        return new Kompaktspieltagkarte(
            nummer,
            s.Tag.ToString("ddd dd.MM.yyyy", Deutsch),
            form,
            (s.Form == Turnierform.Einzel ? "Heim: " : "★ ") + (s.Ausrichter ?? "kein passender Ausrichter"),
            s.Km.ToString("N0", Deutsch) + " km",
            s.Spiele.Select(g => string.Create(Deutsch, $"{g.Zeitpunkt:HH:mm}  {g.Heim} – {g.Gast}")).ToList(),
            verlegen,
            string.Join(", ", s.Gaeste) + " reisen an",
            s.Hinweise,
            gewaehlt)
        {
            Adresse = halle,
        };
    }

    private static string Planzeile(Kompaktkennzahlen k, int mannschaften, double? punkte)
    {
        var formen = new List<string>();
        if (k.Vierer > 0)
        {
            formen.Add(string.Create(Deutsch, $"{k.Vierer}× 4er"));
        }

        if (k.Dreier > 0)
        {
            formen.Add(string.Create(Deutsch, $"{k.Dreier}× 3er"));
        }

        if (k.Einzel > 0)
        {
            formen.Add(string.Create(Deutsch, $"{k.Einzel}× Einzel"));
        }

        string zeile = string.Join(" · ", formen)
            + string.Create(Deutsch, $" · {k.Km:N0} km (Unterschied {k.KmSpanne:N0} km) · Stand höchstens {k.Abstand} Spiele auseinander · bis zu {k.Gemeinsam} von {mannschaften} an einem Tag");
        zeile = k.Verstoesse > 0 ? zeile + string.Create(Deutsch, $" · {k.Verstoesse} Verstöße") : zeile;
        return punkte is double p ? string.Create(Deutsch, $"{p:N1} Punkte · ") + zeile : zeile;
    }

    private List<Kompaktplanwahl> PlaeneBilden()
    {
        Kompaktvariante? gezeigt = hauptfenster.GewaehlteKompaktvariante;
        int mannschaften = hauptfenster.KompaktMannschaften().Count;
        var liste = new List<Kompaktplanwahl>();
        if (hauptfenster.HatClickTtPlan)
        {
            string zeile = hauptfenster.KompaktClickTtPunkte is double p ? string.Create(Deutsch, $"wie in click-TT eingetragen · {p:N1} Punkte") : "wie in click-TT eingetragen";
            liste.Add(new Kompaktplanwahl("Plan aus click-TT", zeile, gezeigt is null, null));
        }

        // Gespeicherte Pläne (gesichert, gemerkt): gewählt gelten sie mit genau ihren Spielen für Druck und click-TT-Export.
        liste.AddRange(hauptfenster.KompaktGespeichertePlaene.Select(v =>
            new Kompaktplanwahl(v.Name, Planzeile(v.Kennzahlen, mannschaften, hauptfenster.KompaktPunkte(v)), ReferenceEquals(v, gezeigt), v) { Gespeichert = true }));
        liste.AddRange(hauptfenster.Kompaktvarianten.Select(v => new Kompaktplanwahl(v.Name, Planzeile(v.Kennzahlen, mannschaften, hauptfenster.KompaktPunkte(v)), ReferenceEquals(v, gezeigt), v)));
        return liste;
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

        Spalten = a.Mannschaften.Select(m => new Kompaktspalte(m.Name, m.Name == team)).ToList();
        (string, DateOnly)? rahmen = Bearbeiten ? cursor : null;
        Kompaktspieltag? spitzentag = gruppen.Spitze.Count >= 3 ? Kompaktpunkte.Gruppentag(a, gruppen.Spitze) : null;
        Kompaktspieltag? kellertag = gruppen.Keller.Count >= 3 ? Kompaktpunkte.Gruppentag(a, gruppen.Keller) : null;
        Zeilen = a.Tage.Select(t => Rasterzeile(a, t, tag == t, team, tag is not null && tag != t, rahmen, (spitzentag, kellertag))).ToList();
        double weiteste = Math.Max(1, a.Mannschaften.Select(m => m.Km).DefaultIfEmpty(0).Max());
        KmZeilen = a.Mannschaften.OrderByDescending(m => m.Km)
            .Select(m => new Kompaktkmzeile(m.Name, Ausrichtertext(m), m.Km.ToString("N0", Deutsch), m.Km / weiteste, m.Name == team))
            .ToList();

        // Markiert sind die Spieltage der Auswahl: des Sonntags, der angeklickte, oder alle der Mannschaft (Heim und auswärts).
        Spieltage = alle
            .Select(x => Spieltagkarte(
                x.Nummer,
                x.Spieltag,
                a,
                (tag is not null && x.Spieltag.Tag == tag) || nummer == x.Nummer || (team is not null && x.Spieltag.Mannschaften.Contains(team))))
            .ToList();
        Kartentitel = (auswahl.Art, team) switch
        {
            ('M', string t) => string.Create(Deutsch, $"Fahrten von {t} ·{a.Mannschaften.First(m => m.Name == t).Km:N0} km"),
            ('T', _) => gewaehlt.Count == 0 ? string.Create(Deutsch, $"Am {tag:dd.MM.yyyy} kein Spieltag") : string.Create(Deutsch, $"Fahrten am {tag:dd.MM.yyyy}"),
            ('S', _) => gewaehlt.Count == 0 ? "Spieltag" : string.Create(Deutsch, $"Spieltag {gewaehlt[0].Tag:dd.MM.yyyy} bei {gewaehlt[0].Ausrichter ?? string.Empty}"),
            _ => "Alle Fahrten der Halbrunde",
        };
        bool alleZeigen = auswahl.Art == '-';
        Karte = Kompaktfahrtenkarte.Bauen(a, alleZeigen ? a.Spieltage : gewaehlt, team, alleZeigen);
    }
}
