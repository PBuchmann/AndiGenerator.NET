// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation;

/// <summary>
/// Dienste der Oberfläche, die die ViewModels brauchen (Dateiauswahl, Rückfragen, UI-Thread),
/// ohne selbst von Avalonia abzuhängen. Umgesetzt in <c>AndiGenerator.UI</c>.
/// </summary>
public interface IOberflaeche
{
    /// <summary>Führt eine Aktion auf dem UI-Thread aus (asynchron eingereiht).</summary>
    /// <param name="aktion">Die Aktion.</param>
    void AufOberflaeche(Action aktion);

    /// <summary>Lässt eine vorhandene Datei auswählen.</summary>
    /// <param name="titel">Titel des Dialogs.</param>
    /// <param name="filter">Dateifilter.</param>
    /// <returns>Der lokale Pfad oder <c>null</c> bei Abbruch.</returns>
    Task<string?> DateiOeffnenAsync(string titel, IReadOnlyList<Dateifilter> filter);

    /// <summary>Lässt eine Zieldatei auswählen.</summary>
    /// <param name="titel">Titel des Dialogs.</param>
    /// <param name="vorschlag">Vorgeschlagener Dateiname.</param>
    /// <param name="filter">Dateifilter.</param>
    /// <returns>Der lokale Pfad oder <c>null</c> bei Abbruch.</returns>
    Task<string?> DateiSpeichernAsync(string titel, string vorschlag, Dateifilter filter);

    /// <summary>Stellt eine Ja/Nein-Frage.</summary>
    /// <param name="titel">Titel des Dialogs.</param>
    /// <param name="frage">Frage.</param>
    /// <returns><c>true</c> bei „Ja“.</returns>
    Task<bool> FragenAsync(string titel, string frage);

    /// <summary>Stellt eine Entscheidungsfrage mit Detailtext und eigenen Schaltflächen.</summary>
    /// <param name="titel">Titel des Dialogs.</param>
    /// <param name="text">Einleitender Text.</param>
    /// <param name="details">Zeilen, die darunter in einem Textfeld erscheinen.</param>
    /// <param name="ja">Beschriftung der Zustimmung.</param>
    /// <param name="nein">Beschriftung der Ablehnung.</param>
    /// <returns><c>true</c> bei Zustimmung.</returns>
    Task<bool> EntscheidenAsync(string titel, string text, IReadOnlyList<string> details, string ja, string nein);

    /// <summary>Fragt einen Text ab.</summary>
    /// <param name="titel">Titel des Dialogs.</param>
    /// <param name="frage">Beschriftung des Eingabefelds.</param>
    /// <param name="vorgabe">Vorbelegung.</param>
    /// <returns>Der eingegebene Text oder <c>null</c> bei Abbruch.</returns>
    Task<string?> TextEingebenAsync(string titel, string frage, string vorgabe);

    /// <summary>Lässt eine Gewichtung wählen (Original <c>TDialogForSingleGewichtungsOptions</c>).</summary>
    /// <param name="titel">Titel des Dialogs.</param>
    /// <param name="beschriftung">Was gewichtet wird, z. B. der Name der Kostenart.</param>
    /// <param name="aktuell">Aktuelle Gewichtung.</param>
    /// <param name="meldungen">Meldungen zur Anzeige im Dialog; leer = ohne Meldungsbereich.</param>
    /// <returns>Die gewählte Gewichtung oder <c>null</c> bei Abbruch.</returns>
    Task<Gewichtung?> GewichtungWaehlenAsync(string titel, string beschriftung, Gewichtung aktuell, IReadOnlyList<string> meldungen);

    /// <summary>Zeigt den Dialog „Spielplandaten bearbeiten“ modal.</summary>
    /// <param name="dialog">Der Dialog.</param>
    /// <returns><c>true</c> bei OK (dann ist <see cref="DatenDialogViewModel.Bestaetigen"/> erfüllt).</returns>
    Task<bool> DatenBearbeitenAsync(DatenDialogViewModel dialog);

    /// <summary>Zeigt den Dialog „Mannschaft“ modal (Original <c>TFormEditTeamName</c>).</summary>
    /// <param name="dialog">Der Dialog.</param>
    /// <returns><c>true</c> bei OK mit gültigen Eingaben.</returns>
    Task<bool> MannschaftBearbeitenAsync(MannschaftsdialogViewModel dialog);

    /// <summary>Zeigt den Dialog „Auswärtskoppelwunsch“ modal (Original <c>TDialogEditOneAuswaertsKoppel</c>).</summary>
    /// <param name="dialog">Der Dialog.</param>
    /// <returns><c>true</c> bei OK mit gültigen Eingaben.</returns>
    Task<bool> AuswaertskoppelBearbeitenAsync(AuswaertskoppeldialogViewModel dialog);

    /// <summary>Zeigt den Dialog „Wunschtermin“ modal (Original <c>TDialogEditOneHomeDay</c>).</summary>
    /// <param name="dialog">Der Dialog.</param>
    /// <returns><c>true</c> bei OK mit gültigen Eingaben.</returns>
    Task<bool> WunschterminBearbeitenAsync(WunschterminDialogViewModel dialog);

    /// <summary>Zeigt den Dialog „Nachbarmannschaft“ modal (Original <c>TDialogEditSisterTeam</c>).</summary>
    /// <param name="dialog">Der Dialog.</param>
    /// <returns><c>true</c> bei OK mit gültigen Eingaben.</returns>
    Task<bool> NachbarmannschaftBearbeitenAsync(NachbarmannschaftsdialogViewModel dialog);

    /// <summary>Zeigt den Dialog „Begegnung“ modal (Original <c>TDialogEditOneSisterGame</c>).</summary>
    /// <param name="dialog">Der Dialog.</param>
    /// <returns><c>true</c> bei OK mit gültigen Eingaben.</returns>
    Task<bool> NachbarspielBearbeitenAsync(NachbarspieldialogViewModel dialog);

    /// <summary>Zeigt den Dialog „Pflichtspieltag“ modal (Original <c>TFormEditMandatoryDatesDialog</c>).</summary>
    /// <param name="dialog">Der Dialog.</param>
    /// <returns><c>true</c> bei OK mit gültigen Eingaben.</returns>
    Task<bool> PflichtspielzeitBearbeitenAsync(PflichtspielzeitdialogViewModel dialog);

    /// <summary>Zeigt den Dialog „Begegnung“ für eine festgelegte Begegnung modal (Original <c>TDialogEditOneGame</c>).</summary>
    /// <param name="dialog">Der Dialog.</param>
    /// <returns><c>true</c> bei OK mit gültigen Eingaben.</returns>
    Task<bool> VorgabespielBearbeitenAsync(VorgabespieldialogViewModel dialog);

    /// <summary>Zeigt die Druckauswahl (Original <c>TFormPrintSelection</c>) modal an.</summary>
    /// <param name="auswahl">Die Auswahl.</param>
    /// <returns><c>true</c>, wenn gedruckt werden soll.</returns>
    Task<bool> DruckauswahlAsync(DruckauswahlViewModel auswahl);

    /// <summary>Umbricht einen Ausdruck auf Seiten und schreibt ihn als PDF-Datei.</summary>
    /// <param name="pfad">Pfad der PDF-Datei.</param>
    /// <param name="dokument">Der Ausdruck.</param>
    /// <param name="format">Das Seitenformat.</param>
    /// <returns>Erledigt, sobald die Datei geschrieben ist.</returns>
    Task PdfErzeugenAsync(string pfad, Druckdokument dokument, Seitenformat format);

    /// <summary>Öffnet eine Datei mit dem zugeordneten Programm des Systems (PDF: zum Ansehen und Drucken).</summary>
    /// <param name="pfad">Pfad der Datei.</param>
    /// <returns>Erledigt, sobald das Öffnen angestoßen ist.</returns>
    Task DateiAnzeigenAsync(string pfad);

    /// <summary>Öffnet eine Internetadresse im Browser des Systems.</summary>
    /// <param name="adresse">Die Adresse.</param>
    /// <returns>Erledigt, sobald das Öffnen angestoßen ist.</returns>
    Task AdresseOeffnenAsync(Uri adresse);

    /// <summary>Zeigt den Dialog „Über“ modal an.</summary>
    /// <param name="ueber">Der Dialog.</param>
    /// <returns>Erledigt, sobald der Dialog geschlossen ist.</returns>
    Task UeberAnzeigenAsync(UeberViewModel ueber);

    /// <summary>Zeigt eine Meldung.</summary>
    /// <param name="titel">Titel des Dialogs.</param>
    /// <param name="text">Meldungstext.</param>
    /// <returns>Erledigt, sobald die Meldung geschlossen ist.</returns>
    Task MeldenAsync(string titel, string text);
}
