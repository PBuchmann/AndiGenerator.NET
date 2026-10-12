// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>Kompaktspieltage (nicht im Original): Ansicht, Schalter und Punkt der Einrichtung.</summary>
public sealed partial class HauptfensterViewModel
{
    private KompaktspieltageAnsichtViewModel? kompaktAnsicht;
    private IRelayCommand? kompaktCommand;

    /// <summary>Holt den Befehl „Kompaktspieltage“ (Ansicht als andockbares Dokument).</summary>
    public IRelayCommand KompaktspieltageCommand => kompaktCommand ??= new RelayCommand(KompaktspieltageOeffnen, () => ZeigtArbeitsbereich);

    /// <summary>
    /// Holt einen Wert, der angibt, ob die Ansicht „Kompaktspieltage“ angeboten wird: bei einer Halbrunde mit überwiegend
    /// Spieltagen am Wochenende oder wenn die Kompaktspieltage schon eingeschaltet sind.
    /// </summary>
    public bool ZeigtKompaktspieltage =>
        sitzung is Plansitzung s
        && (Kompaktmodus is Kompaktmodus.Ein or Kompaktmodus.Bestaetigt || Kompaktplanung.InFrage(s.Staffel, s.Optionen.Rundenplanung, false));

    /// <summary>Holt, ob die geöffnete Staffel mit Kompaktspieltagen geplant wird.</summary>
    internal Kompaktmodus Kompaktmodus => sitzung?.DatenBearbeiten().Kompaktspieltage ?? Kompaktmodus.Offen;

    /// <summary>Wertet den Plan aus click-TT als Kompaktspieltage aus.</summary>
    /// <returns>Die Auswertung oder <c>null</c> ohne geöffnete Staffel.</returns>
    internal Kompaktauswertung? Kompaktauswertung() =>
        sitzung is Plansitzung s ? Kompaktplanung.Auswerten(s.Staffel, s.DatenBearbeiten(), s.Optionen.Rundenplanung, DateOnly.FromDateTime(DateTime.Today)) : null;

    /// <summary>Schaltet die Kompaktspieltage ein oder aus und speichert das mit der Staffel.</summary>
    /// <param name="modus">Der neue Zustand.</param>
    /// <returns>Eine Aufgabe.</returns>
    internal async Task KompaktspieltageSchaltenAsync(Kompaktmodus modus)
    {
        if (sitzung is not Plansitzung s)
        {
            return;
        }

        Datenbearbeitung daten = s.DatenBearbeiten();
        daten.KompaktspieltageSetzen(modus);
        if (await UebernehmenAsync(s, daten))
        {
            KompaktAktualisieren();
        }
    }

    /// <summary>
    /// Punkt „Klasse mit Kompaktspieltagen“ der Einrichtung: nur bei einer Halbrunde („(HR)“ bzw. „(FR)“ im Namen, eingestellt
    /// oder vorgeschlagen) mit überwiegend Spieltagen am Wochenende; noch nicht gefragt → einschalten oder normal planen,
    /// eingeschaltet (noch nicht bestätigt) → beibehalten oder zurücknehmen.
    /// </summary>
    private static Einrichtungsschritt? KompaktschrittErstellen(Plansitzung neu, int nummer, Rundenvorschlag vorschlag)
    {
        Kompaktmodus modus = neu.DatenBearbeiten().Kompaktspieltage;
        bool eingeschaltet = modus == Kompaktmodus.Ein;
        if (modus is Kompaktmodus.Bestaetigt or Kompaktmodus.Aus
            || (!eingeschaltet && !Kompaktplanung.InFrage(neu.Staffel, neu.Optionen.Rundenplanung, vorschlag == Rundenvorschlag.Halbrunde)))
        {
            return null;
        }

        IReadOnlyList<string> details =
        [
            "Optimiert wird nach Luftlinie zwischen den Hallen (Adressen aus den Terminwünschen).",
            "Gilt nur für eine Halbrunde. Lässt sich jederzeit in der Ansicht „Kompaktspieltage“ wieder ausschalten.",
        ];
        string text = eingeschaltet
            ? "Diese Staffel wird mit Kompaktspieltagen geplant. Falls das beim letzten Mal versehentlich eingeschaltet wurde, lässt es sich hier zurücknehmen. "
                + KompaktspieltageAnsichtViewModel.Erklaerung
            : KompaktspieltageAnsichtViewModel.Erklaerung;
        IReadOnlyList<Einrichtungsoption> optionen = eingeschaltet
            ? [new Einrichtungsoption("Kompaktspieltage beibehalten", "wird danach nicht mehr gefragt"), new Einrichtungsoption("Kompaktspieltage ausschalten", "normale Planung")]
            : [new Einrichtungsoption("Mit Kompaktspieltagen planen", "3er- und 4er-Spieltage, Karte und Prüfung"), new Einrichtungsoption("Normal planen", "jedes Spiel einzeln")];
        return new Einrichtungsschritt(nummer, "Klasse mit Kompaktspieltagen", text, details, optionen);
    }

    /// <summary>Nach Öffnen, Schließen oder Änderungen: Ansicht und Navigation aktualisieren.</summary>
    private void KompaktAktualisieren()
    {
        kompaktAnsicht?.Laden();
        OnPropertyChanged(nameof(ZeigtKompaktspieltage));
        KompaktspieltageCommand.NotifyCanExecuteChanged();
    }

    private void KompaktspieltageOeffnen()
    {
        if (kompaktAnsicht is null)
        {
            kompaktAnsicht = new KompaktspieltageAnsichtViewModel(this);
            fabrik.AnsichtHinzufuegen(kompaktAnsicht);
        }
        else
        {
            fabrik.SetActiveDockable(kompaktAnsicht);
        }
    }

    /// <summary>Übernimmt die Wahl im Punkt „Klasse mit Kompaktspieltagen“.</summary>
    private async Task<bool> KompaktschrittUebernehmenAsync(Plansitzung s, Einrichtungsschritt schritt)
    {
        if (!ReferenceEquals(s, sitzung))
        {
            return false;
        }

        Kompaktmodus bisher = s.DatenBearbeiten().Kompaktspieltage;
        Kompaktmodus neu = (bisher, schritt.Auswahl) switch
        {
            (Kompaktmodus.Ein, 0) => Kompaktmodus.Bestaetigt,
            (_, 0) => Kompaktmodus.Ein,
            _ => Kompaktmodus.Aus,
        };
        await KompaktspieltageSchaltenAsync(neu);
        if (neu != Kompaktmodus.Aus)
        {
            KompaktspieltageOeffnen();
        }

        return true;
    }
}
