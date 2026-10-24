// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

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
        try
        {
            kopie = Beispielsammlung.Kopieren(quelle, eigeneBasis);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            await oberflaeche.MeldenAsync("Beispiel öffnen", "Das Beispiel konnte nicht in den Datenordner kopiert werden:" + Environment.NewLine + fehler.Message);
            return;
        }

        await StaffelOeffnenAsync(kopie);
    }
}
