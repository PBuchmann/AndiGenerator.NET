// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Concurrent;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation.Tests;

/// <summary>
/// Attrappe der Oberfläche: Dialoge werden nicht angezeigt, sondern an die jeweils gesetzte Reaktion übergeben (sie
/// füllt das ViewModel wie ein Anwender und liefert „OK“ oder „Abbrechen“). Ohne Reaktion gilt „Abbrechen“. Alle
/// Aufrufe werden mitgeschrieben.
/// </summary>
internal sealed class TestOberflaeche : IOberflaeche
{
    private readonly ConcurrentQueue<Action> warteschlange = new();

    /// <summary>
    /// Holt oder setzt, ob <see cref="AufOberflaeche"/> Aufrufe sammelt statt sie sofort (auf dem Hintergrund-Thread)
    /// auszuführen. Wie auf dem echten UI-Thread laufen sie dann erst mit <see cref="Abarbeiten"/>, und zwar auf dem
    /// Thread des Tests – nie gleichzeitig mit ihm.
    /// </summary>
    public bool Sammeln { get; set; }

    /// <summary>Holt die Titel aller angezeigten Dialoge in der Reihenfolge der Aufrufe.</summary>
    public List<string> Aufrufe { get; } = [];

    /// <summary>Holt oder setzt die Antwort auf Ja/Nein-Fragen.</summary>
    public bool Antwort { get; set; }

    /// <summary>Holt oder setzt die Datei, die „Öffnen“ liefert.</summary>
    public string? DateiZumOeffnen { get; set; }

    /// <summary>Holt oder setzt die Datei, die „Speichern unter“ liefert.</summary>
    public string? DateiZumSpeichern { get; set; }

    /// <summary>Holt oder setzt die Texteingabe (z. B. Name eines gemerkten Plans).</summary>
    public string? Texteingabe { get; set; }

    /// <summary>Holt oder setzt die gewählte Gewichtung.</summary>
    public Gewichtung? GewaehlteGewichtung { get; set; }

    /// <summary>Holt die Meldungen (Titel und Text).</summary>
    public List<string> Meldungen { get; } = [];

    public Func<DatenDialogViewModel, Task<bool>>? Datendialog { get; set; }

    public Func<MannschaftsdialogViewModel, bool>? Mannschaft { get; set; }

    public Func<AuswaertskoppeldialogViewModel, bool>? Auswaertskoppel { get; set; }

    public Func<WunschterminDialogViewModel, bool>? Wunschtermin { get; set; }

    public Func<NachbarmannschaftsdialogViewModel, Task<bool>>? Nachbarmannschaft { get; set; }

    public Func<NachbarspieldialogViewModel, bool>? Nachbarspiel { get; set; }

    public Func<PflichtspielzeitdialogViewModel, bool>? Pflichtspielzeit { get; set; }

    public Func<VorgabespieldialogViewModel, bool>? Vorgabespiel { get; set; }

    /// <summary>Holt oder setzt die Antwort auf die Druckauswahl.</summary>
    public Func<DruckauswahlViewModel, bool>? Druckauswahl { get; set; }

    /// <summary>Holt oder setzt den Fehler, den <see cref="PdfErzeugenAsync"/> auslöst.</summary>
    public Exception? PdfFehler { get; set; }

    /// <summary>Holt den zuletzt erzeugten Ausdruck.</summary>
    public Druckdokument? Ausdruck { get; private set; }

    /// <summary>Holt das Seitenformat des zuletzt erzeugten Ausdrucks.</summary>
    public Seitenformat? Format { get; private set; }

    /// <summary>Holt die angezeigten Dateien.</summary>
    public List<string> Angezeigt { get; } = [];

    /// <summary>Holt die geöffneten Internetadressen.</summary>
    public List<Uri> Adressen { get; } = [];

    /// <summary>Holt den zuletzt angezeigten Dialog „Über“.</summary>
    public UeberViewModel? Ueber { get; private set; }

    public void AufOberflaeche(Action aktion)
    {
        if (Sammeln)
        {
            warteschlange.Enqueue(aktion);
        }
        else
        {
            aktion();
        }
    }

    /// <summary>Führt die gesammelten Aufrufe aus (siehe <see cref="Sammeln"/>).</summary>
    public void Abarbeiten()
    {
        while (warteschlange.TryDequeue(out Action? aktion))
        {
            aktion();
        }
    }

    public Task<string?> DateiOeffnenAsync(string titel, IReadOnlyList<Dateifilter> filter) => Merken(titel, DateiZumOeffnen);

    public Task<string?> DateiSpeichernAsync(string titel, string vorschlag, Dateifilter filter) => Merken(titel, DateiZumSpeichern);

    public Task<bool> FragenAsync(string titel, string frage) => Merken(titel, Antwort);

    public Task<bool> EntscheidenAsync(string titel, string text, IReadOnlyList<string> details, string ja, string nein) => Merken(titel, Antwort);

    public Task<string?> TextEingebenAsync(string titel, string frage, string vorgabe) => Merken(titel, Texteingabe);

    public Task<Gewichtung?> GewichtungWaehlenAsync(string titel, string beschriftung, Gewichtung aktuell, IReadOnlyList<string> meldungen) =>
        Merken(titel, GewaehlteGewichtung);

    public Task<bool> DatenBearbeitenAsync(DatenDialogViewModel dialog)
    {
        Aufrufe.Add("Daten");
        return Datendialog?.Invoke(dialog) ?? Task.FromResult(false);
    }

    public Task<bool> MannschaftBearbeitenAsync(MannschaftsdialogViewModel dialog) => Merken("Mannschaft", Mannschaft?.Invoke(dialog) ?? false);

    public Task<bool> AuswaertskoppelBearbeitenAsync(AuswaertskoppeldialogViewModel dialog) =>
        Merken("Auswärtskoppel", Auswaertskoppel?.Invoke(dialog) ?? false);

    public Task<bool> WunschterminBearbeitenAsync(WunschterminDialogViewModel dialog) => Merken("Wunschtermin", Wunschtermin?.Invoke(dialog) ?? false);

    public Task<bool> NachbarmannschaftBearbeitenAsync(NachbarmannschaftsdialogViewModel dialog)
    {
        Aufrufe.Add("Nachbarmannschaft");
        return Nachbarmannschaft?.Invoke(dialog) ?? Task.FromResult(false);
    }

    public Task<bool> NachbarspielBearbeitenAsync(NachbarspieldialogViewModel dialog) => Merken("Nachbarspiel", Nachbarspiel?.Invoke(dialog) ?? false);

    public Task<bool> PflichtspielzeitBearbeitenAsync(PflichtspielzeitdialogViewModel dialog) =>
        Merken("Pflichtspielzeit", Pflichtspielzeit?.Invoke(dialog) ?? false);

    public Task<bool> VorgabespielBearbeitenAsync(VorgabespieldialogViewModel dialog) => Merken("Vorgabespiel", Vorgabespiel?.Invoke(dialog) ?? false);

    public Task<bool> DruckauswahlAsync(DruckauswahlViewModel auswahl) => Merken("Druckauswahl", Druckauswahl?.Invoke(auswahl) ?? false);

    public Task PdfErzeugenAsync(string pfad, Druckdokument dokument, Seitenformat format)
    {
        Aufrufe.Add("PDF");
        if (PdfFehler is not null)
        {
            return Task.FromException(PdfFehler);
        }

        Ausdruck = dokument;
        Format = format;
        return Task.CompletedTask;
    }

    public Task DateiAnzeigenAsync(string pfad)
    {
        Angezeigt.Add(pfad);
        return Task.CompletedTask;
    }

    public Task AdresseOeffnenAsync(Uri adresse)
    {
        Adressen.Add(adresse);
        return Task.CompletedTask;
    }

    public Task UeberAnzeigenAsync(UeberViewModel ueber)
    {
        Aufrufe.Add("Über");
        Ueber = ueber;
        return Task.CompletedTask;
    }

    public Task MeldenAsync(string titel, string text)
    {
        Aufrufe.Add(titel);
        Meldungen.Add(titel + ": " + text);
        return Task.CompletedTask;
    }

    private Task<T> Merken<T>(string titel, T ergebnis)
    {
        Aufrufe.Add(titel);
        return Task.FromResult(ergebnis);
    }
}
