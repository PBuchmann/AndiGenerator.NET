// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Presentation;
using AndiGenerator.Rendering;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AndiGenerator.UI;

/// <summary>Dienste der Oberfläche für die ViewModels, umgesetzt mit Avalonia.</summary>
/// <param name="fenster">Fenster, zu dem die Dialoge gehören.</param>
public sealed class AvaloniaOberflaeche(Window fenster) : IOberflaeche
{
    /// <inheritdoc/>
    public void AufOberflaeche(Action aktion) => Dispatcher.UIThread.Post(aktion);

    /// <inheritdoc/>
    public async Task<string?> DateiOeffnenAsync(string titel, IReadOnlyList<Dateifilter> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        IReadOnlyList<IStorageFile> dateien = await fenster.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = titel,
            AllowMultiple = false,
            FileTypeFilter = filter.Select(Dateityp).ToList(),
        });
        return dateien.Count == 0 ? null : dateien[0].TryGetLocalPath();
    }

    /// <inheritdoc/>
    public async Task<string?> DateiSpeichernAsync(string titel, string vorschlag, Dateifilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        IStorageFile? datei = await fenster.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = titel,
            SuggestedFileName = vorschlag,
            FileTypeChoices = [Dateityp(filter)],
            ShowOverwritePrompt = true,
        });
        return datei?.TryGetLocalPath();
    }

    /// <inheritdoc/>
    public Task<bool> FragenAsync(string titel, string frage) =>
        Zeigen<bool>(new Dialogfenster(titel, frage, eingabe: null, mitAbbrechen: true));

    /// <inheritdoc/>
    public Task<bool> EntscheidenAsync(string titel, string text, IReadOnlyList<string> details, string ja, string nein) =>
        Zeigen<bool>(new Dialogfenster(titel, text, details, ja, nein));

    /// <inheritdoc/>
    public async Task<string?> TextEingebenAsync(string titel, string frage, string vorgabe)
    {
        var dialog = new Dialogfenster(titel, frage, vorgabe, mitAbbrechen: true);
        return await Zeigen<bool>(dialog) ? dialog.Eingabe : null;
    }

    /// <inheritdoc/>
    public Task<Gewichtung?> GewichtungWaehlenAsync(string titel, string beschriftung, Gewichtung aktuell, IReadOnlyList<string> meldungen) =>
        Zeigen<Gewichtung?>(new Gewichtungsdialog(titel, beschriftung, aktuell, meldungen));

    /// <inheritdoc/>
    public Task<bool> DatenBearbeitenAsync(DatenDialogViewModel dialog) =>
        Zeigen<bool>(new Datendialog(dialog));

    /// <inheritdoc/>
    public Task<bool> MannschaftBearbeitenAsync(MannschaftsdialogViewModel dialog) =>
        Zeigen<bool>(new Mannschaftsdialog(dialog));

    /// <inheritdoc/>
    public Task<bool> AuswaertskoppelBearbeitenAsync(AuswaertskoppeldialogViewModel dialog) =>
        Zeigen<bool>(new Auswaertskoppeldialog(dialog));

    /// <inheritdoc/>
    public Task<bool> WunschterminBearbeitenAsync(WunschterminDialogViewModel dialog) =>
        Zeigen<bool>(new Wunschtermindialog(dialog));

    /// <inheritdoc/>
    public Task<bool> NachbarmannschaftBearbeitenAsync(NachbarmannschaftsdialogViewModel dialog) =>
        Zeigen<bool>(new Nachbarmannschaftsdialog(dialog));

    /// <inheritdoc/>
    public Task<bool> NachbarspielBearbeitenAsync(NachbarspieldialogViewModel dialog) =>
        Zeigen<bool>(new Nachbarspieldialog(dialog));

    /// <inheritdoc/>
    public Task<bool> PflichtspielzeitBearbeitenAsync(PflichtspielzeitdialogViewModel dialog) =>
        Zeigen<bool>(new Pflichtspielzeitdialog(dialog));

    /// <inheritdoc/>
    public Task<bool> VorgabespielBearbeitenAsync(VorgabespieldialogViewModel dialog) =>
        Zeigen<bool>(new Vorgabespieldialog(dialog));

    /// <inheritdoc/>
    public Task<bool> DruckauswahlAsync(DruckauswahlViewModel auswahl) => Zeigen<bool>(new Druckauswahldialog(auswahl));

    /// <inheritdoc/>
    public Task PdfErzeugenAsync(string pfad, Druckdokument dokument, Seitenformat format) =>
        Task.Run(() => PdfAusdruck.Schreiben(pfad, dokument, format));

    /// <inheritdoc/>
    public async Task DateiAnzeigenAsync(string pfad)
    {
        if (!await fenster.Launcher.LaunchFileInfoAsync(new FileInfo(pfad)))
        {
            await MeldenAsync("Drucken", "Die PDF-Datei wurde gespeichert, konnte aber nicht geöffnet werden:" + Environment.NewLine + pfad);
        }
    }

    /// <inheritdoc/>
    public async Task AdresseOeffnenAsync(Uri adresse)
    {
        if (!await fenster.Launcher.LaunchUriAsync(adresse))
        {
            await MeldenAsync("Adresse öffnen", "Die Adresse konnte nicht geöffnet werden:" + Environment.NewLine + adresse);
        }
    }

    /// <inheritdoc/>
    public Task UeberAnzeigenAsync(UeberViewModel ueber) => Zeigen<object?>(new Ueberdialog(ueber));

    /// <inheritdoc/>
    public Task MeldenAsync(string titel, string text) =>
        Zeigen<bool>(new Dialogfenster(titel, text, eingabe: null, mitAbbrechen: false));

    private static FilePickerFileType Dateityp(Dateifilter filter) => new(filter.Name) { Patterns = filter.Muster.ToList() };

    /// <summary>Leerer Auftrag mit niedriger Priorität: Wenn er an der Reihe ist, sind alle Eingaben davor verarbeitet.</summary>
    private static void KlickAbwarten()
    {
        // Absichtlich leer; nur der Zeitpunkt der Ausführung zählt.
    }

    /// <summary>
    /// Zeigt einen modalen Dialog, aber erst, nachdem der auslösende Klick vollständig verarbeitet ist (Maustaste
    /// losgelassen, Zeigerfang freigegeben). Öffnet sich der Dialog noch mitten im Klick, sperrt er das Hauptfenster,
    /// während dieses den Klick noch abschließt; Windows quittiert das mit Fehlerton und blinkendem Dialog.
    /// </summary>
    private async Task<T> Zeigen<T>(Window dialog)
    {
        await Dispatcher.UIThread.InvokeAsync(KlickAbwarten, DispatcherPriority.Background);
        return await dialog.ShowDialog<T>(Besitzer());
    }

    /// <summary>
    /// Besitzer eines neuen Dialogs: das oberste sichtbare Fenster (z. B. „Spielplandaten bearbeiten“), damit ein Dialog
    /// aus einem Dialog nicht hinter diesen rutscht.
    /// </summary>
    private Window Besitzer() =>
        Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.LastOrDefault(w => w.IsVisible) ?? fenster
            : fenster;
}
