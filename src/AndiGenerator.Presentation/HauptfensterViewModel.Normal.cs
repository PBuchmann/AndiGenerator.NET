// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Persistence.Gemeinsam;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Normaler Modus und Expertenmodus (Peter, 29.10.2026): Der normale Modus führt in drei Seiten zum Plan (Dateien, Planung,
/// Ergebnis); der Expertenmodus ist die bisherige Oberfläche. Einmal gewählt, startet das Programm wieder in diesem Modus
/// (<see cref="Programmeinstellungen.Expertenmodus"/>); man kann jederzeit zurück.
/// </summary>
public sealed partial class HauptfensterViewModel
{
    private NormalmodusViewModel? normal;
    private bool istNormal;
    private IRelayCommand? normalCommand;
    private IRelayCommand? experteCommand;

    /// <summary>Holt den normalen Modus (seine drei Seiten).</summary>
    public NormalmodusViewModel Normal => normal ??= new NormalmodusViewModel(this);

    /// <summary>Holt einen Wert, der angibt, ob der normale Modus gewählt ist.</summary>
    public bool IstNormal
    {
        get => istNormal;
        private set
        {
            if (SetProperty(ref istNormal, value))
            {
                OnPropertyChanged(nameof(IstExperte));
                OnPropertyChanged(nameof(ZeigtNormalAnsicht));
                OnPropertyChanged(nameof(AktiverZoom));
                NormalCommand.NotifyCanExecuteChanged();
                ExperteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob der Expertenmodus gewählt ist.</summary>
    public bool IstExperte => !IstNormal;

    /// <summary>
    /// Holt einen Wert, der angibt, ob die Seiten des normalen Modus das Fenster füllen – nicht, solange der Klassenleiter
    /// die Einrichtung prüft (sie liegt im Expertenteil).
    /// </summary>
    public bool ZeigtNormalAnsicht => IstNormal && !(ZeigtEinrichtung && Normal.EinrichtungAnzeigen);

    /// <summary>Holt den Befehl „Normal“ (in den normalen Modus wechseln).</summary>
    public IRelayCommand NormalCommand => normalCommand ??= new RelayCommand(() => ModusWechseln(normal: true), () => !IstNormal && !Laeuft && !KompaktRechnet);

    /// <summary>Holt den Befehl „Experte“ (in den Expertenmodus wechseln).</summary>
    public IRelayCommand ExperteCommand => experteCommand ??= new RelayCommand(() => ModusWechseln(normal: false), () => IstNormal && !(normal?.Laeuft ?? false));

    /// <summary>Holt die geöffnete Sitzung (für den normalen Modus).</summary>
    internal Plansitzung? Sitzung => sitzung;

    /// <summary>Öffnet eine Staffel aus abgelegten Dateien (eine XML-Datei, dazu höchstens eine CSV- und eine PDF-Datei).</summary>
    /// <param name="pfade">Die Dateien.</param>
    /// <returns>Erledigt, wenn die Staffel geöffnet ist (oder ein Hinweis kam).</returns>
    internal Task DateienOeffnenAsync(IReadOnlyList<string> pfade) => DateienAuswertenAsync(pfade);

    /// <summary>Exportiert einen Plan als CSV für click-TT (normaler Modus: den besten Plan).</summary>
    /// <param name="quelle">Der Plan.</param>
    /// <returns>Erledigt nach dem Export.</returns>
    internal Task PlanExportierenAsync(PlanQuelle quelle) => CsvExportierenAsync(quelle);

    /// <summary>Druckt als PDF; der Plan steht in der Auswahl vorn.</summary>
    /// <param name="quelle">Der Plan.</param>
    /// <returns>Erledigt nach dem Druck.</returns>
    internal Task PlanDruckenAsync(PlanQuelle? quelle) => DruckenAsync(quelle);

    /// <summary>Holt die nächste Kennung einer Ansicht (für die Ansichten des normalen Modus).</summary>
    /// <returns>Die Kennung.</returns>
    internal string AnsichtsId() => NaechsteId();

    /// <summary>Die Einrichtung mit ihren Vorgaben übernehmen (normaler Modus: „Plan erstellen“ ohne Rückfragen).</summary>
    /// <returns>Erledigt.</returns>
    internal Task EinrichtungUebernehmenAsync() => Einrichtung is null ? Task.CompletedTask : EinrichtungAbschliessen();

    /// <summary>Nach dem Speichern eines gemerkten Plans im normalen Modus: Planquellen neu, der Plan wird neu gelesen.</summary>
    /// <param name="eintrag">Der gemerkte Plan.</param>
    /// <returns>Seine Planquelle.</returns>
    internal PlanQuelle GemerktenPlanNeuLesen(GemerkterPlanEintrag eintrag)
    {
        gemerkteStaende.Remove(eintrag.Pfad);
        QuellenNeuAufbauen();
        return Planquellen.FirstOrDefault(q => q.Eintrag?.Pfad == eintrag.Pfad) ?? PlanQuelle.Gemerkt(eintrag);
    }

    /// <summary>Meldet, dass sich die Sichtbarkeit der Seiten geändert hat (z. B. Einrichtung prüfen).</summary>
    internal void NormalAnsichtGeaendert()
    {
        OnPropertyChanged(nameof(ZeigtNormalAnsicht));
        OnPropertyChanged(nameof(AktiverZoom));
        ExperteCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Liest den gespeicherten Modus.</summary>
    /// <param name="basis">Der eigene Ordner.</param>
    /// <returns><c>true</c> für den Expertenmodus.</returns>
    private static bool ModusLesen(string basis)
    {
        try
        {
            return Programmeinstellungen.Laden(basis).Expertenmodus;
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void ModusWechseln(bool normal)
    {
        IstNormal = normal;
        try
        {
            (Programmeinstellungen.Laden(eigeneBasis) with { Expertenmodus = !normal }).Speichern(eigeneBasis);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            Statuszeile = "Der Modus ließ sich nicht speichern: " + fehler.Message;
        }

        if (normal)
        {
            Normal.Aktualisieren();
        }
    }
}
