// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Presentation;
using AndiGenerator.Rendering;
using AndiGenerator.UI;

namespace AndiGenerator.Mcp;

/// <summary>
/// Die „Oberfläche“ des Servers: keine Fenster und Dialoge. Was die Oberfläche fragen würde, beantworten vorher gesetzte Werte
/// (Datei zum Öffnen, Name eines Plans, Zustimmung); Meldungen und Fragen werden gesammelt und gehen mit der nächsten Antwort
/// an die KI. Dateien werden nie von selbst angezeigt oder geöffnet.
/// </summary>
internal sealed class Fernoberflaeche : IOberflaeche
{
    private readonly Bedienfaden faden;
    private readonly List<string> meldungen = [];

    /// <summary>Initialisiert die Oberfläche.</summary>
    /// <param name="faden">Der Faden, auf dem alles läuft.</param>
    public Fernoberflaeche(Bedienfaden faden) => this.faden = faden;

    /// <summary>Holt oder setzt die Datei, die „Öffnen“ liefert (einmalig).</summary>
    public string? DateiZumOeffnen { get; set; }

    /// <summary>Holt oder setzt die Datei, die „Speichern unter“ liefert (einmalig).</summary>
    public string? DateiZumSpeichern { get; set; }

    /// <summary>Holt oder setzt die Texteingabe (z. B. Name eines gemerkten Plans; einmalig).</summary>
    public string? Texteingabe { get; set; }

    /// <summary>Holt oder setzt die Antwort auf Ja/Nein-Fragen und Entscheidungen (Standard: nein).</summary>
    public bool Zustimmen { get; set; }

    /// <summary>Holt oder setzt, was mit dem Dialog der B-Varianten geschieht: <c>null</c> = nur merken, sonst die zu übernehmende Spalte.</summary>
    public Func<AlternativendialogViewModel, bool>? Varianten { get; set; }

    /// <summary>Holt oder setzt die Antwort auf die Druckauswahl (stellt sie ein und liefert, ob gedruckt wird); ohne: nicht drucken.</summary>
    public Func<DruckauswahlViewModel, bool>? Druckauswahl { get; set; }

    /// <summary>Holt den zuletzt gezeigten Dialog der B-Varianten.</summary>
    public AlternativendialogViewModel? LetzteVarianten { get; private set; }

    /// <summary>Gibt die gesammelten Meldungen und Fragen zurück und leert die Liste.</summary>
    /// <returns>Die Meldungen.</returns>
    public IReadOnlyList<string> MeldungenAbholen()
    {
        lock (meldungen)
        {
            List<string> liste = [.. meldungen];
            meldungen.Clear();
            return liste;
        }
    }

    /// <inheritdoc/>
    public void AufOberflaeche(Action aktion) => faden.Post(_ => aktion(), null);

    /// <inheritdoc/>
    public Task<string?> DateiOeffnenAsync(string titel, IReadOnlyList<Dateifilter> filter)
    {
        string? datei = DateiZumOeffnen;
        DateiZumOeffnen = null;
        return Task.FromResult(datei);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> DateienOeffnenAsync(string titel, IReadOnlyList<Dateifilter> filter) =>
        await DateiOeffnenAsync(titel, filter) is string datei ? [datei] : [];

    /// <inheritdoc/>
    public Task<string?> DateiSpeichernAsync(string titel, string vorschlag, Dateifilter filter)
    {
        string? datei = DateiZumSpeichern;
        DateiZumSpeichern = null;
        return Task.FromResult(datei);
    }

    /// <inheritdoc/>
    public Task<bool> FragenAsync(string titel, string frage)
    {
        Merken($"{titel} (Frage, Antwort {(Zustimmen ? "ja" : "nein")}): {frage}");
        return Task.FromResult(Zustimmen);
    }

    /// <inheritdoc/>
    public Task<Sicherungswahl> SicherungAbfragenAsync(Planvergleich vergleich)
    {
        // Der gesicherte Plan bleibt; der aktuelle ist ohnehin als bester Plan bzw. gemerkter Plan erreichbar.
        Merken("Plan sichern: Der gesicherte Plan bleibt erhalten.");
        return Task.FromResult(Sicherungswahl.Behalten);
    }

    /// <inheritdoc/>
    public Task<bool> AlternativenZeigenAsync(AlternativendialogViewModel dialog)
    {
        LetzteVarianten = dialog;
        return Task.FromResult(Varianten?.Invoke(dialog) ?? false);
    }

    /// <inheritdoc/>
    public Task<bool> EntscheidenAsync(string titel, string text, IReadOnlyList<string> details, string ja, string nein)
    {
        Merken($"{titel} (Entscheidung, Antwort „{(Zustimmen ? ja : nein)}“): {text} {string.Join("; ", details)}");
        return Task.FromResult(Zustimmen);
    }

    /// <inheritdoc/>
    public Task<string?> TextEingebenAsync(string titel, string frage, string vorgabe)
    {
        string? text = Texteingabe;
        Texteingabe = null;
        return Task.FromResult(text);
    }

    /// <inheritdoc/>
    public Task<Gewichtung?> GewichtungWaehlenAsync(string titel, string beschriftung, Gewichtung aktuell, IReadOnlyList<string> meldungen) =>
        Task.FromResult<Gewichtung?>(null);

    /// <inheritdoc/>
    public Task<bool> DatenBearbeitenAsync(DatenDialogViewModel dialog) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> MannschaftBearbeitenAsync(MannschaftsdialogViewModel dialog) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> AuswaertskoppelBearbeitenAsync(AuswaertskoppeldialogViewModel dialog) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> WunschterminBearbeitenAsync(WunschterminDialogViewModel dialog) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> NachbarmannschaftBearbeitenAsync(NachbarmannschaftsdialogViewModel dialog) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> NachbarspielBearbeitenAsync(NachbarspieldialogViewModel dialog) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> PflichtspielzeitBearbeitenAsync(PflichtspielzeitdialogViewModel dialog) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> VorgabespielBearbeitenAsync(VorgabespieldialogViewModel dialog) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> DruckauswahlAsync(DruckauswahlViewModel auswahl) => Task.FromResult(Druckauswahl?.Invoke(auswahl) ?? false);

    /// <inheritdoc/>
    public Task PdfErzeugenAsync(string pfad, Druckdokument dokument, Seitenformat format) =>
        Task.Run(() => PdfAusdruck.Schreiben(pfad, dokument, format));

    /// <inheritdoc/>
    public Task DateiAnzeigenAsync(string pfad) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task AdresseOeffnenAsync(Uri adresse) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task UeberAnzeigenAsync(UeberViewModel ueber) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task MeldenAsync(string titel, string text)
    {
        Merken($"{titel}: {text}");
        return Task.CompletedTask;
    }

    private void Merken(string text)
    {
        lock (meldungen)
        {
            meldungen.Add(text);
        }
    }
}
