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
/// click-TT, die nicht in der Terminmeldung (XML) stehen. Bisher die Mannschaftsmeldung (CSV) als Vorschlag für die
/// Setzliste aus den TTR-Werten der Stammspieler.
/// </summary>
public sealed class ZusatzdatenSeiteViewModel : DatenSeiteViewModel
{
    private readonly IOberflaeche oberflaeche;
    private Setzlistenvorschlag? vorschlag;
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
    public override bool IstStandard => vorschlag is null;

    /// <inheritdoc/>
    public override void Laden()
    {
        // Der geladene Vorschlag bleibt beim Wechsel der Seite erhalten; aus der Arbeitskopie ist nichts zu lesen.
    }

    /// <inheritdoc/>
    public override void Speichern()
    {
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
    public override void Standard() => Vorschlagen(null, string.Empty);

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

    private async Task AufstellungenLadenAsync()
    {
        string? pfad = await oberflaeche.DateiOeffnenAsync("Mannschaftsmeldung aus click-TT laden", [new Dateifilter("CSV-Dateien", ["*.csv"])]);
        if (pfad is not null)
        {
            AufstellungenLaden(pfad);
        }
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
