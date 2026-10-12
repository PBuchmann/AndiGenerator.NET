// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Globalization;
using AndiGenerator.Application;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Seite „Daten aus click-TT“ (erster Punkt der Einrichtung beim ersten Öffnen, nicht im Original): weitere Exporte aus
/// click-TT, die nicht in der Terminmeldung (XML) stehen: die Mannschaftsmeldung (CSV) als Vorschlag für die Setzliste
/// aus den TTR-Werten der Stammspieler und der Bericht „Terminwünsche“ (PDF) für Spiellokal-Nummern, Adressen und
/// Bemerkungen der Vereine.
/// </summary>
public sealed class ZusatzdatenSeiteViewModel : DatenSeiteViewModel
{
    private readonly IOberflaeche oberflaeche;
    private readonly List<Wochenspielbefund> gesetzt = [];
    private Setzlistenvorschlag? vorschlag;
    private Terminwunschabgleich? abgleich;
    private string pdfDatei = string.Empty;
    private string datei = string.Empty;
    private string fehler = string.Empty;
    private bool alsSetzliste = true;
    private decimal? grenze = (decimal)Datenbearbeitung.Wochenspielgrenze;
    private string wochenspielText = string.Empty;
    private string wochenspielStatus = string.Empty;
    private bool hatWochentagsgastgeber;
    private bool hatWochenspielpruefung;
    private Wochenspielpruefung? pruefung;
    private string protokollKopf = string.Empty;

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche (Dateiauswahl).</param>
    public ZusatzdatenSeiteViewModel(Datenbearbeitung bearbeitung, IOberflaeche oberflaeche)
        : base(bearbeitung, "Daten aus click-TT")
    {
        ArgumentNullException.ThrowIfNull(oberflaeche);
        this.oberflaeche = oberflaeche;
        AufstellungenLadenCommand = new AsyncRelayCommand(AufstellungenLadenAsync);
        TerminwuenscheLadenCommand = new AsyncRelayCommand(TerminwuenscheLadenAsync);
        SperrenSetzenCommand = new AsyncRelayCommand(SperrenSetzenAsync, () => HatFehlendeSperren);
        WochenspielePruefen();
    }

    /// <summary>Holt den Text des Einrichtungspunkts.</summary>
    public static string Einrichtungstext =>
        "Die Terminmeldung aus click-TT (XML) enthält nicht alles, was für die Planung nützlich ist. Weitere Exporte "
        + "derselben Staffel können Sie hier ergänzen – jeder ist freiwillig.";

    /// <summary>Holt die Erklärung zur Mannschaftsmeldung.</summary>
    public static string AufstellungsErklaerung =>
        "Mannschaftsmeldung (CSV): In click-TT unter den Downloads der Staffel „Mannschaftsmeldung Vorrunde (csv)“. Aus den "
        + "TTR-Werten der Stammspieler jeder Mannschaft entsteht ein Vorschlag für die Setzliste – die stärkste Mannschaft "
        + "zuerst. Gelesen werden nur Mannschaft, Rang, Status und TTR-Wert, keine Namen.";

    /// <summary>Holt die Erklärung zum Bericht „Terminwünsche“.</summary>
    public static string TerminwunschErklaerung =>
        "Terminwünsche (PDF): In click-TT unter den Downloads der Staffel „Terminmeldungen (pdf)“. Der Bericht nennt die "
        + "Spiellokale 1 bis 3 jedes Vereins mit Adresse. Daraus setzt das Programm bei jedem Heimtermin die Nummer des "
        + "Spiellokals (für den Export nach click-TT), merkt sich die Adressen und zeigt die Bemerkungen der Vereine sowie "
        + "Unterschiede zu den Spielplandaten. Termine und Sperren werden nicht geändert; Kontaktdaten werden nicht gelesen.";

    /// <summary>Holt die Erklärung zur Prüfung der Wochenspiele.</summary>
    public static string WochenspielErklaerung =>
        "Ein Verein kann Auswärtsspiele unter der Woche bei weit entfernten Gegnern sperren (Spielplandaten, Seite "
        + "„Wochenende (60-km-Regel)“); die Generierung legt diese Spiele dann aufs Wochenende. Die Wettspielordnung legt "
        + "dafür kein Maß fest. Diese Prüfung vergleicht die Sperren mit der Luftlinie zwischen den Hallen – sofort hier, "
        + "sobald die Hallenadressen bekannt sind, und erneut bei jeder Änderung der Grenze. Sie gibt nur Hinweise und "
        + "ändert von selbst nichts. Eine Sperre entscheidet nur dort, wo der Gastgeber Heimtermine unter der Woche und am "
        + "Wochenende hat (unter der Woche: Montag bis Donnerstag, Freitag nur, wenn er in den Einstellungen nicht zum "
        + "Wochenende zählt) – nur diese Paare werden geprüft. Fehlende Sperren können Sie mit einer Rückfrage für alle "
        + "Treffer setzen; das Protokoll zeigt danach, welche.";

    /// <summary>Holt den Befehl „Terminwünsche laden…“.</summary>
    public IAsyncRelayCommand TerminwuenscheLadenCommand { get; }

    /// <summary>Holt einen Wert, der angibt, ob ein Bericht „Terminwünsche“ geladen ist.</summary>
    public bool HatTerminwuensche => abgleich is not null;

    /// <summary>Holt die Erfolgsmeldung nach dem Laden der Terminwünsche (hervorgehoben unter dem Knopf) oder einen leeren Text.</summary>
    public string TerminwunschErgebnis
    {
        get
        {
            if (abgleich is null)
            {
                return string.Empty;
            }

            List<Mannschaftsabgleich> m = abgleich.Mannschaften.ToList();
            return string.Create(
                CultureInfo.InvariantCulture,
                $"„{pdfDatei}“ geladen: {m.Count(x => x.Gefunden)} von {m.Count} Mannschaften gefunden, {m.Sum(x => x.Abweichungen.Count)} Unterschiede, {m.Count(x => x.Bemerkung.Length > 0)} Bemerkungen. Einzelheiten und die Prüfung der Wochenspiele stehen darunter.");
        }
    }

    /// <summary>Holt die Kennzahlen des Abgleichs (Mannschaften, gesetzte Nummern, Bemerkungen, Unterschiede).</summary>
    public ObservableCollection<Kachelwert> Kennzahlen { get; } = [];

    /// <summary>Holt je Mannschaft Spiellokale, Bemerkung und Unterschiede.</summary>
    public ObservableCollection<Vereinskarte> Vereine { get; } = [];

    /// <summary>Holt den Namen der geladenen PDF-Datei.</summary>
    public string TerminwunschDatei => pdfDatei;

    /// <summary>Holt den Vermerk, welche Mannschaftsmeldung schon übernommen wurde, oder einen leeren Text.</summary>
    public string AufstellungsVermerk => Vermerk(Bearbeitung.MannschaftsmeldungVermerk);

    /// <summary>Holt den Vermerk, welcher Bericht „Terminwünsche“ schon übernommen wurde, oder einen leeren Text.</summary>
    public string TerminwunschVermerk => Vermerk(Bearbeitung.TerminwunschVermerk);

    /// <summary>Holt oder setzt die Grenze in Kilometern Luftlinie (Vorgabe 60).</summary>
    public decimal? Grenze
    {
        get => grenze;
        set
        {
            if (SetProperty(ref grenze, value))
            {
                WochenspielePruefen();
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob Hallenadressen bekannt sind und die Prüfung angezeigt wird.</summary>
    public bool HatWochenspielpruefung
    {
        get => hatWochenspielpruefung;
        private set => SetProperty(ref hatWochenspielpruefung, value);
    }

    /// <summary>Holt das Ergebnis in einem Satz, z. B. „Keine Mannschaft hat Heimtermine unter der Woche …“.</summary>
    public string WochenspielStatus
    {
        get => wochenspielStatus;
        private set => SetProperty(ref wochenspielStatus, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob es Gastgeber mit Heimterminen unter der Woche gibt (Grenze und Listen).</summary>
    public bool HatWochentagsgastgeber
    {
        get => hatWochentagsgastgeber;
        private set => SetProperty(ref hatWochentagsgastgeber, value);
    }

    /// <summary>Holt die Kennzahlen der Prüfung als Kacheln.</summary>
    public ObservableCollection<Kachelwert> WochenspielKennzahlen { get; } = [];

    /// <summary>Holt die Zusatzhinweise der Prüfung (pauschale Sperren, fehlende Adressen) oder einen leeren Text.</summary>
    public string WochenspielText
    {
        get => wochenspielText;
        private set => SetProperty(ref wochenspielText, value);
    }

    /// <summary>Holt die Paare über der Grenze ohne Sperre („Sperre fehlt vermutlich“).</summary>
    public ObservableCollection<Wochenspielzeile> FehlendeSperren { get; } = [];

    /// <summary>Holt die Paare bis zur Grenze mit Sperre („Sperre vermutlich unnötig“).</summary>
    public ObservableCollection<Wochenspielzeile> UnnoetigeSperren { get; } = [];

    /// <summary>Holt den Befehl „Fehlende Sperren setzen…“ (eine Rückfrage für alle Treffer).</summary>
    public IAsyncRelayCommand SperrenSetzenCommand { get; }

    /// <summary>Holt das Protokoll der gesetzten Sperren, je Sperre eine Zeile.</summary>
    public ObservableCollection<string> SperrenProtokoll { get; } = [];

    /// <summary>Holt die Kopfzeile des Protokolls (wann, bei welcher Grenze, wie viele) oder einen leeren Text.</summary>
    public string SperrenProtokollKopf
    {
        get => protokollKopf;
        private set
        {
            if (SetProperty(ref protokollKopf, value))
            {
                OnPropertyChanged(nameof(HatSperrenProtokoll));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob Sperren gesetzt wurden.</summary>
    public bool HatSperrenProtokoll => SperrenProtokollKopf.Length > 0;

    /// <summary>Holt einen Wert, der angibt, ob Sperren vermutlich fehlen.</summary>
    public bool HatFehlendeSperren => FehlendeSperren.Count > 0;

    /// <summary>Holt einen Wert, der angibt, ob Sperren vermutlich unnötig sind.</summary>
    public bool HatUnnoetigeSperren => UnnoetigeSperren.Count > 0;

    /// <summary>Holt den Befehl „Mannschaftsmeldung laden…“.</summary>
    public IAsyncRelayCommand AufstellungenLadenCommand { get; }

    /// <summary>Holt die Zeilen des Vorschlags.</summary>
    public ObservableCollection<Staerkezeile> Staerken { get; } = [];

    /// <summary>Holt einen Wert, der angibt, ob eine Mannschaftsmeldung geladen ist.</summary>
    public bool HatAufstellungen => vorschlag is not null;

    /// <summary>Holt die Erfolgsmeldung nach dem Laden der Mannschaftsmeldung (hervorgehoben unter dem Knopf) oder einen leeren Text.</summary>
    public string AufstellungsErgebnis => vorschlag is null
        ? string.Empty
        : string.Create(CultureInfo.InvariantCulture, $"„{datei}“ geladen: Vorschlag für die Setzliste mit {vorschlag.Reihenfolge.Count} Mannschaften, die stärkste zuerst – siehe darunter.");

    /// <summary>Holt die Beschreibung des Vorschlags, z. B. welche Datei und wie gewertet wurde.</summary>
    public string AufstellungsHinweis
    {
        get
        {
            if (vorschlag is null)
            {
                return string.Empty;
            }

            string text = string.Create(CultureInfo.InvariantCulture, $"{datei}: Schnitt der besten {vorschlag.GewerteteSpieler} Stammspieler ohne Reservespieler.");
            return vorschlag.OhneAufstellung.Count == 0 ? text : text + " Ohne Aufstellung: " + string.Join(", ", vorschlag.OhneAufstellung) + ".";
        }
    }

    /// <summary>Holt die Fehlermeldung beim Laden oder einen leeren Text.</summary>
    public string Fehler
    {
        get => fehler;
        private set
        {
            if (SetProperty(ref fehler, value))
            {
                OnPropertyChanged(nameof(HatFehler));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob eine Fehlermeldung angezeigt wird.</summary>
    public bool HatFehler => Fehler.Length > 0;

    /// <summary>Holt oder setzt einen Wert, der angibt, ob der Vorschlag als Setzliste übernommen (und diese aktiviert) wird.</summary>
    public bool AlsSetzliste
    {
        get => alsSetzliste;
        set
        {
            if (SetProperty(ref alsSetzliste, value))
            {
                AenderungMelden();
            }
        }
    }

    /// <inheritdoc/>
    public override bool IstStandard => vorschlag is null && abgleich is null && !HatSperrenProtokoll;

    /// <inheritdoc/>
    public override void Laden()
    {
        // Der geladene Vorschlag bleibt beim Wechsel der Seite erhalten; aus der Arbeitskopie ist nichts zu lesen.
    }

    /// <inheritdoc/>
    public override void Speichern()
    {
        if (abgleich is not null)
        {
            Bearbeitung.TerminwuenscheUebernehmen(abgleich, pdfDatei);
        }

        if (vorschlag is null)
        {
            return;
        }

        Bearbeitung.MannschaftsmeldungVermerken(datei);
        if (AlsSetzliste)
        {
            Bearbeitung.SetzlisteSpeichern(new Setzlistendaten(true, vorschlag.Reihenfolge.Select(m => m.Mannschaft).ToList()));
        }
    }

    /// <inheritdoc/>
    public override void Standard()
    {
        if (gesetzt.Count > 0)
        {
            Bearbeitung.WochenspielSperrenEntfernen(gesetzt);
            gesetzt.Clear();
            SperrenProtokoll.Clear();
            SperrenProtokollKopf = string.Empty;
        }

        Vorschlagen(null, string.Empty);
        Abgleichen(null, string.Empty);
    }

    /// <summary>Liest einen Bericht „Terminwünsche“ und zeigt den Abgleich; Fehler erscheinen in <see cref="Fehler"/>.</summary>
    /// <param name="pfad">Pfad der PDF-Datei.</param>
    public void TerminwuenscheLaden(string pfad)
    {
        try
        {
            Abgleichen(Bearbeitung.TerminwuenscheAbgleichen(pfad), Path.GetFileName(pfad));
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            Fehler = ex.Message;
            return;
        }

        Fehler = string.Empty;
        AenderungMelden();
    }

    /// <summary>Liest eine Mannschaftsmeldung und zeigt den Vorschlag; Fehler erscheinen in <see cref="Fehler"/>.</summary>
    /// <param name="pfad">Pfad der CSV-Datei.</param>
    public void AufstellungenLaden(string pfad)
    {
        Setzlistenvorschlag neu;
        try
        {
            neu = Bearbeitung.SetzlisteAusMannschaftsmeldung(pfad);
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            Fehler = ex.Message;
            return;
        }

        if (neu.OhneAufstellung.Count == neu.Reihenfolge.Count)
        {
            Fehler = "In der Datei steht keine Mannschaft dieser Staffel.";
            return;
        }

        Fehler = string.Empty;
        Vorschlagen(neu, Path.GetFileName(pfad));
        AenderungMelden();
    }

    private static string Vermerk(string vermerk) => vermerk.Length == 0 ? string.Empty : "Bereits übernommen: " + vermerk + ".";

    private static string Protokollzeile(Wochenspielbefund b) =>
        string.Create(CultureInfo.InvariantCulture, $"{b.Gast} spielt bei {b.Heim} nur am Wochenende ({b.Luftlinie:0} km)");

    private async Task AufstellungenLadenAsync()
    {
        string? pfad = await oberflaeche.DateiOeffnenAsync("Mannschaftsmeldung aus click-TT laden", [new Dateifilter("CSV-Dateien", ["*.csv"])]);
        if (pfad is not null)
        {
            AufstellungenLaden(pfad);
        }
    }

    private async Task TerminwuenscheLadenAsync()
    {
        string? pfad = await oberflaeche.DateiOeffnenAsync("Terminwünsche aus click-TT laden", [new Dateifilter("PDF-Dateien", ["*.pdf"])]);
        if (pfad is not null)
        {
            TerminwuenscheLaden(pfad);
        }
    }

    private void Abgleichen(Terminwunschabgleich? neu, string dateiname)
    {
        abgleich = neu;
        pdfDatei = dateiname;
        Kennzahlen.Clear();
        Vereine.Clear();
        if (neu is not null)
        {
            string Zahl(int n) => n.ToString(CultureInfo.InvariantCulture);
            List<Mannschaftsabgleich> m = neu.Mannschaften.ToList();
            Kennzahlen.Add(new Kachelwert(Zahl(m.Count(x => x.Gefunden)) + " / " + Zahl(m.Count), "Mannschaften im Bericht gefunden"));
            Kennzahlen.Add(new Kachelwert(Zahl(neu.NummernGesetzt), "Heimtermine erhalten die Spiellokal-Nummer"));
            Kennzahlen.Add(new Kachelwert(Zahl(m.Count(x => x.Bemerkung.Length > 0)), "Bemerkungen der Vereine"));
            Kennzahlen.Add(new Kachelwert(Zahl(m.Sum(x => x.Abweichungen.Count)), "Unterschiede zu den Spielplandaten"));

            // Mannschaften mit Bemerkung oder Unterschieden zuerst, sonst in der Reihenfolge der Staffel.
            foreach (Mannschaftsabgleich a in m.OrderBy(x => x.Bemerkung.Length > 0 || x.Abweichungen.Count > 0 || !x.Gefunden ? 0 : 1))
            {
                Vereine.Add(new Vereinskarte(a.Mannschaft, a.Gefunden, a.Spiellokale, a.Bemerkung, a.Abweichungen));
            }
        }

        OnPropertyChanged(nameof(TerminwunschErgebnis));
        OnPropertyChanged(nameof(HatTerminwuensche));
        OnPropertyChanged(nameof(TerminwunschDatei));
        OnPropertyChanged(nameof(IstStandard));
        WochenspielePruefen();
    }

    private async Task SperrenSetzenAsync()
    {
        if (pruefung is not Wochenspielpruefung p)
        {
            return;
        }

        List<Wochenspielbefund> fehlend = p.Befunde.Where(b => b.Art == Wochenspielhinweis.SperreFehlt).ToList();
        if (fehlend.Count == 0)
        {
            return;
        }

        string grenzeText = p.Grenze.ToString("0", CultureInfo.InvariantCulture);
        string text = string.Create(
            CultureInfo.InvariantCulture,
            $"Bei {fehlend.Count} Paaren liegen die Hallen weiter als {grenzeText} km auseinander, der Gastgeber hat Heimtermine unter der Woche und am Wochenende, und der Gast hat ihn noch nicht gesperrt. Sollen diese Spiele nur am Wochenende stattfinden?");
        if (!await oberflaeche.EntscheidenAsync("Fehlende Sperren setzen", text, fehlend.Select(Protokollzeile).ToList(), "Sperren setzen", "Abbrechen"))
        {
            return;
        }

        IReadOnlyList<Wochenspielbefund> neu = Bearbeitung.WochenspielSperrenSetzen(fehlend);
        gesetzt.AddRange(neu);
        foreach (Wochenspielbefund b in neu)
        {
            SperrenProtokoll.Add(Protokollzeile(b));
        }

        SperrenProtokollKopf = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.Now:dd.MM.yyyy HH:mm}: {gesetzt.Count} Sperren gesetzt (Grenze {grenzeText} km). Sie werden mit der Staffel gespeichert, sobald Sie die Seite übernehmen.");
        WochenspielePruefen();
        AenderungMelden();
    }

    private void WochenspielePruefen()
    {
        double km = (double)(Grenze ?? (decimal)Datenbearbeitung.Wochenspielgrenze);
        Wochenspielpruefung p = Bearbeitung.WochenspielePruefen(km, abgleich?.Meldungen ?? []);
        pruefung = p;
        FehlendeSperren.Clear();
        UnnoetigeSperren.Clear();
        foreach (Wochenspielbefund b in p.Befunde)
        {
            var zeile = new Wochenspielzeile(b.Gast, b.Heim, b.Luftlinie.ToString("0", CultureInfo.InvariantCulture) + " km");
            (b.Art == Wochenspielhinweis.SperreFehlt ? FehlendeSperren : UnnoetigeSperren).Add(zeile);
        }

        string Zahl(int n) => n.ToString(CultureInfo.InvariantCulture);
        WochenspielKennzahlen.Clear();
        WochenspielKennzahlen.Add(new Kachelwert(Zahl(p.Wochentagsgastgeber.Count), "Gastgeber mit Heimterminen unter der Woche und am Wochenende"));
        WochenspielKennzahlen.Add(new Kachelwert(Zahl(p.GeprueftePaare), "Paare geprüft (Gast → Gastgeber)"));
        WochenspielKennzahlen.Add(new Kachelwert(Zahl(FehlendeSperren.Count), "Sperre fehlt vermutlich"));
        WochenspielKennzahlen.Add(new Kachelwert(Zahl(UnnoetigeSperren.Count), "Sperre vermutlich unnötig"));
        HatWochentagsgastgeber = p.Wochentagsgastgeber.Count > 0;
        WochenspielStatus = (p.Wochentagsgastgeber.Count, p.Befunde.Count) switch
        {
            (0, _) => "In dieser Staffel hat keine Mannschaft Heimtermine sowohl unter der Woche als auch am Wochenende "
                + "gemeldet – wann gespielt wird, steht damit schon fest. Die Sperren wirken sich nicht aus; es gibt nichts zu prüfen.",
            (_, 0) => "Keine Auffälligkeiten: Alle Sperren passen zur Grenze.",
            _ => "Gastgeber mit Heimterminen unter der Woche und am Wochenende: " + string.Join(", ", p.Wochentagsgastgeber) + ".",
        };

        var text = new List<string>();
        if (p.PauschalGesperrt.Count > 0)
        {
            text.Add("Alle Gegner gesperrt (Vereinswunsch, nicht bewertet): " + string.Join(", ", p.PauschalGesperrt) + ".");
        }

        if (p.OhneLage.Count > 0)
        {
            text.Add("Ohne Hallenadresse: " + string.Join(", ", p.OhneLage) + ".");
        }

        OnPropertyChanged(nameof(HatFehlendeSperren));
        OnPropertyChanged(nameof(HatUnnoetigeSperren));
        SperrenSetzenCommand.NotifyCanExecuteChanged();
        WochenspielText = string.Join(" ", text);
        HatWochenspielpruefung = p.OhneLage.Count < Bearbeitung.Mannschaftsnamen().Count;
    }

    private void Vorschlagen(Setzlistenvorschlag? neu, string dateiname)
    {
        vorschlag = neu;
        datei = dateiname;
        Staerken.Clear();
        if (neu is not null)
        {
            int platz = 0;
            foreach (Mannschaftsstaerke m in neu.Reihenfolge)
            {
                platz++;
                Staerken.Add(new Staerkezeile(
                    platz.ToString(CultureInfo.InvariantCulture) + ".",
                    m.Mannschaft,
                    m.HatAufstellung ? "Ø " + m.Schnitt.ToString("0", CultureInfo.InvariantCulture) : "keine Aufstellung",
                    m.HatAufstellung ? m.Stammspieler.ToString(CultureInfo.InvariantCulture) + " Stammspieler" : string.Empty));
            }
        }

        OnPropertyChanged(nameof(AufstellungsErgebnis));
        OnPropertyChanged(nameof(HatAufstellungen));
        OnPropertyChanged(nameof(AufstellungsHinweis));
        OnPropertyChanged(nameof(IstStandard));
    }
}
