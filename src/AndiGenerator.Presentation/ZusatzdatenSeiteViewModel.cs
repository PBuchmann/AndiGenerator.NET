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
    private Setzlistenvorschlag? vorschlag;
    private Terminwunschabgleich? abgleich;
    private string pdfDatei = string.Empty;
    private string datei = string.Empty;
    private string fehler = string.Empty;
    private bool alsSetzliste = true;

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

    /// <summary>Holt den Befehl „Terminwünsche laden…“.</summary>
    public IAsyncRelayCommand TerminwuenscheLadenCommand { get; }

    /// <summary>Holt einen Wert, der angibt, ob ein Bericht „Terminwünsche“ geladen ist.</summary>
    public bool HatTerminwuensche => abgleich is not null;

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

    /// <summary>Holt den Befehl „Mannschaftsmeldung laden…“.</summary>
    public IAsyncRelayCommand AufstellungenLadenCommand { get; }

    /// <summary>Holt die Zeilen des Vorschlags.</summary>
    public ObservableCollection<Staerkezeile> Staerken { get; } = [];

    /// <summary>Holt einen Wert, der angibt, ob eine Mannschaftsmeldung geladen ist.</summary>
    public bool HatAufstellungen => vorschlag is not null;

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
    public override bool IstStandard => vorschlag is null && abgleich is null;

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

        OnPropertyChanged(nameof(HatTerminwuensche));
        OnPropertyChanged(nameof(TerminwunschDatei));
        OnPropertyChanged(nameof(IstStandard));
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

        OnPropertyChanged(nameof(HatAufstellungen));
        OnPropertyChanged(nameof(AufstellungsHinweis));
        OnPropertyChanged(nameof(IstStandard));
    }
}
