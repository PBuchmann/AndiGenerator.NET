// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Persistence.Plandaten;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>Beispielstaffeln auf der Startseite (die Testpläne des Originals).</summary>
public sealed partial class HauptfensterViewModel
{
    private AsyncRelayCommand<string>? beispielOeffnen;

    /// <summary>Holt die beiliegenden Beispiele.</summary>
    public IReadOnlyList<Beispielzeile> Beispiele { get; } = Beispielsammlung.Lesen(Beispielsammlung.Programmordner);

    /// <summary>Holt einen Wert, der angibt, ob es Beispiele gibt.</summary>
    public bool HatBeispiele => Beispiele.Count > 0;

    /// <summary>Holt den Befehl, ein Beispiel zu öffnen (als Kopie im Datenordner; Parameter: Pfad der beiliegenden Datei).</summary>
    public IAsyncRelayCommand<string> BeispielOeffnenCommand => beispielOeffnen ??= new AsyncRelayCommand<string>(BeispielOeffnenAsync, _ => !ZeigtEinrichtung);

    private async Task BeispielOeffnenAsync(string? quelle)
    {
        if (quelle is null)
        {
            return;
        }

        string kopie;
        bool erstesMal;
        try
        {
            erstesMal = !File.Exists(Path.Combine(eigeneBasis, "Beispiele", Path.GetFileName(quelle)));
            kopie = Beispielsammlung.Kopieren(quelle, eigeneBasis);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            await oberflaeche.MeldenAsync("Beispiel öffnen", "Das Beispiel konnte nicht in den Datenordner kopiert werden:" + Environment.NewLine + fehler.Message);
            return;
        }

        await StaffelOeffnenAsync(kopie);

        if (erstesMal)
        {
            BeispielplanMerken(Path.ChangeExtension(kopie, ".plan"));
        }

        // Die Doppelrunde ist eine Einstellung, keine Angabe der click-TT-Datei: beim ersten Öffnen einschalten, damit das
        // Beispiel zeigt, wofür es gedacht ist (später bleibt, was in den Einstellungen gewählt wurde).
        if (erstesMal && Beispielsammlung.IstDoppelrunde(quelle) && Optionen is { Doppelrunde: false } bisher)
        {
            OptionenAnwenden(bisher with { Doppelrunde = true });
        }
    }

    /// <summary>
    /// Merkt den mitgelieferten Plan eines Beispiels (beim ersten Öffnen): Er steht dann unter „Start mit“ bzw. bei
    /// Kompaktspieltagen neben den Varianten zur Wahl, ohne dass man selbst lange rechnen muss.
    /// </summary>
    /// <param name="datei">Die Datei <c>.plan</c> neben der Kopie des Beispiels.</param>
    private void BeispielplanMerken(string datei)
    {
        if (sitzung is not Plansitzung s || !File.Exists(datei))
        {
            return;
        }

        try
        {
            IReadOnlyList<GespeichertesSpiel> spiele = GespeicherterPlanDatei.Laden(datei);
            s.PlanMerken(Beispielsammlung.Beispielplan, spiele.Select(g => new Spiel(g.Zeitpunkt, g.Heim, g.Gast, g.Spiellokal)));
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException or PlanDatenFormatException)
        {
            return;
        }

        QuellenNeuAufbauen();
    }
}
