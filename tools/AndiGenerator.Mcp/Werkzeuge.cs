// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AndiGenerator.Mcp;

/// <summary>
/// Die Werkzeuge des MCP-Servers. Sie entsprechen den Schritten der Oberfläche; die Antworten sind deutsch und enthalten den
/// Stand danach, damit die KI ohne zusätzliche Abfrage weiß, wie es weitergeht.
/// </summary>
[McpServerToolType]
internal sealed class Werkzeuge
{
    /// <summary>Anleitung für die KI (wird beim Verbinden übermittelt).</summary>
    internal const string Anleitung =
        "AndiGenerator.NET plant Spielpläne für Tischtennis-Staffeln aus click-TT. Ablauf: staffel_oeffnen (XML-Export aus click-TT) → "
        + "einrichtung (offene Punkte ansehen, mit einrichtung_waehlen entscheiden) → einrichtung_abschliessen → verfahren_waehlen "
        + "(automodus empfohlen; bei Kompaktspieltagen entfällt das) → starten → warten (wiederholt) und qualitaet bzw. kompakt lesen → "
        + "bei Bedarf einstufen oder b_variante_festlegen → stoppen → plan_merken → csv_exportieren bzw. drucken. Bei der Kostenoptimierung "
        + "steuert gewichtung_setzen, was wie viel zählt (immer nur eine Gewichtung ändern und rechnen lassen). Spielplandaten (Termine, "
        + "Sperren, spielfreie Tage, Setzliste, 60-km-Regel, festgelegte Begegnungen, Pflichtspieltage) lesen spielplandaten und mannschaft; "
        + "Mannschaften, Spiellokale, Koppeln, Nachbarmannschaften und Heimrecht ändern die Werkzeuge mit diesen Namen. Den Plan zeigen "
        + "terminplan (Wochen mit allen Spielen) und diagramm (Bild mit Kennzahlen). Änderungen gelten sofort, auch für die laufende Generierung. „schritt_fuer_schritt“ nennt den "
        + "nächsten sinnvollen Schritt. Optimiert wird vom Programm (Suche und HiGHS); entscheiden und erklären ist Aufgabe der KI. "
        + "Vor Änderungen an der Einteilung, Festlegungen und dem Export den Anwender fragen.";

    private readonly Fernsteuerung s;

    /// <summary>Initialisiert die Werkzeuge.</summary>
    /// <param name="steuerung">Die Steuerung.</param>
    public Werkzeuge(Fernsteuerung steuerung) => s = steuerung;

    /// <summary>Stand der Planung.</summary>
    /// <returns>Der Stand.</returns>
    [McpServerTool(Name = "status", ReadOnly = true)]
    [Description("Stand: geöffnete Staffel, Einrichtung, Verfahren, ob gerechnet wird, Kacheln der Generierung, nächster Schritt und Meldungen.")]
    public Task<Stand> Status() => s.StandAsync();

    /// <summary>Staffel öffnen.</summary>
    /// <param name="pfad">Pfad der XML-Datei.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "staffel_oeffnen")]
    [Description("Öffnet eine Staffel: den XML-Export aus click-TT (Änderungen in der .modifications-Datei daneben werden übernommen). Eine laufende Generierung wird beendet.")]
    public Task<Aktionsergebnis> StaffelOeffnen([Description("Vollständiger Pfad der XML-Datei.")] string pfad) => s.OeffnenAsync(pfad);

    /// <summary>Staffel schließen.</summary>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "staffel_schliessen")]
    [Description("Schließt die Staffel; der Plan wird vorher gesichert (der gesicherte Plan bleibt erhalten).")]
    public Task<Aktionsergebnis> StaffelSchliessen() => s.SchliessenAsync();

    /// <summary>Punkte der Einrichtung.</summary>
    /// <returns>Die Punkte.</returns>
    [McpServerTool(Name = "einrichtung", ReadOnly = true)]
    [Description("Die offenen Punkte der Einrichtung nach dem Öffnen (z. B. Runde, Kompaktspieltage, Setzliste) mit Erklärung und Optionen.")]
    public Task<IReadOnlyList<Einrichtungspunkt>> Einrichtung() => s.EinrichtungAsync();

    /// <summary>Option der Einrichtung wählen.</summary>
    /// <param name="nummer">Nummer des Punkts.</param>
    /// <param name="option">Titel der Option.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "einrichtung_waehlen")]
    [Description("Wählt bei einem Punkt der Einrichtung eine Option (gilt beim Abschließen).")]
    public Task<Aktionsergebnis> EinrichtungWaehlen([Description("Nummer des Punkts.")] int nummer, [Description("Titel der Option, wie in „einrichtung“.")] string option) =>
        s.EinrichtungWaehlenAsync(nummer, option);

    /// <summary>Einrichtung abschließen.</summary>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "einrichtung_abschliessen")]
    [Description("Schließt die Einrichtung ab: gewählte bzw. vorgewählte Optionen gelten, übrige Punkte bleiben unverändert.")]
    public Task<Aktionsergebnis> EinrichtungAbschliessen() => s.EinrichtungAbschliessenAsync();

    /// <summary>Verfahren wählen.</summary>
    /// <param name="verfahren">„automodus“ oder „kostenoptimierung“.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "verfahren_waehlen")]
    [Description("Wählt das Verfahren für normale Runden: „automodus“ (lenkt sich selbst nach den Stufen A, B, C; empfohlen) oder „kostenoptimierung“ (nach Gewichtungen). Während der Generierung wird gewechselt.")]
    public Task<Aktionsergebnis> VerfahrenWaehlen([Description("„automodus“ oder „kostenoptimierung“.")] string verfahren) =>
        s.VerfahrenAsync(verfahren.Trim().StartsWith("auto", StringComparison.OrdinalIgnoreCase));

    /// <summary>Ausgangsplan wählen.</summary>
    /// <param name="name">Name aus „Startplaene“.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "startplan_waehlen")]
    [Description("Wählt den Ausgangsplan des nächsten Starts (Namen siehe „status“: Startplaene, z. B. leerem Plan, in click-TT vorhandenem Plan, gemerktem Plan …).")]
    public Task<Aktionsergebnis> StartplanWaehlen([Description("Name des Ausgangsplans.")] string name) => s.StartplanAsync(name);

    /// <summary>Starten.</summary>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "starten")]
    [Description("Startet die Generierung (bzw. bei Kompaktspieltagen die Berechnung der Pläne). Sie läuft im Hintergrund; mit „warten“ verfolgen.")]
    public Task<Aktionsergebnis> Starten() => s.StartenAsync();

    /// <summary>Anhalten.</summary>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "anhalten")]
    [Description("Hält die Generierung an; der beste Plan bleibt, Ansichten zeigen ihn.")]
    public Task<Aktionsergebnis> Anhalten() => s.PauseAsync(true);

    /// <summary>Fortsetzen.</summary>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "fortsetzen")]
    [Description("Setzt eine angehaltene Generierung fort.")]
    public Task<Aktionsergebnis> Fortsetzen() => s.PauseAsync(false);

    /// <summary>Stoppen.</summary>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "stoppen")]
    [Description("Beendet die Generierung bzw. Berechnung; der beste Plan bleibt erhalten.")]
    public Task<Aktionsergebnis> Stoppen() => s.StoppenAsync();

    /// <summary>Warten.</summary>
    /// <param name="sekunden">Höchstens so lange.</param>
    /// <returns>Der Stand danach.</returns>
    [McpServerTool(Name = "warten", ReadOnly = true)]
    [Description("Wartet, bis sich etwas Wesentliches tut (Verbesserung, neue Phase, Ende einer Berechnung), höchstens die angegebene Zeit, und liefert den Stand.")]
    public Task<Stand> Warten([Description("Höchstens so viele Sekunden (1–120).")] int sekunden = 30) => s.WartenAsync(sekunden);

    /// <summary>Qualität.</summary>
    /// <returns>Die Qualität.</returns>
    [McpServerTool(Name = "qualitaet", ReadOnly = true)]
    [Description("Qualität des Plans: je Kriterium Stufe (A muss, B soll, C wird geglättet), Verstöße und im Automodus „bestmöglich“ aus HiGHS; je Stufe, ob das Optimum erreicht ist.")]
    public Task<Qualitaet?> Qualitaet() => s.QualitaetAsync();

    /// <summary>Verstöße eines Kriteriums.</summary>
    /// <param name="kriterium">Name des Kriteriums.</param>
    /// <returns>Die Verstöße.</returns>
    [McpServerTool(Name = "verstoesse", ReadOnly = true)]
    [Description("Die Verstöße eines Kriteriums je Mannschaft mit Datum, und was es kosten würde, das Kriterium bestmöglich zu machen.")]
    public Task<Verstossliste?> Verstoesse([Description("Name des Kriteriums wie in „qualitaet“, z. B. Sperrtermine.")] string kriterium) => s.VerstoesseAsync(kriterium);

    /// <summary>Einstufen.</summary>
    /// <param name="kriterium">Name des Kriteriums.</param>
    /// <param name="stufe">A, B oder C.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "einstufen")]
    [Description("Stuft ein Kriterium im Automodus in A (muss), B (soll) oder C (wird geglättet) ein; gilt sofort, auch während der Generierung. Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> Einstufen([Description("Name des Kriteriums.")] string kriterium, [Description("„A“, „B“ oder „C“.")] string stufe) =>
        s.EinstufenAsync(kriterium, stufe);

    /// <summary>B-Varianten.</summary>
    /// <returns>Die Varianten.</returns>
    [McpServerTool(Name = "b_varianten", ReadOnly = true)]
    [Description("Die B-Varianten aus HiGHS im Vergleich zum aktuellen Plan (nur solche, die ihn in einem Kriterium verbessern), je Kriterium die Werte.")]
    public Task<Variantentabelle?> BVarianten() => s.VariantenAsync();

    /// <summary>B-Variante festlegen.</summary>
    /// <param name="titel">Titel der Variante.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "b_variante_festlegen")]
    [Description("Übernimmt eine B-Variante: Kein A- oder B-Kriterium darf danach mehr Verstöße haben, der Automodus glättet nur noch C. Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> BVarianteFestlegen([Description("Titel der Variante wie in „b_varianten“ (ohne die Summen in Klammern).")] string titel) =>
        s.VarianteFestlegenAsync(titel);

    /// <summary>Festlegung aufheben.</summary>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "festlegung_aufheben")]
    [Description("Hebt die Festlegung von A und B wieder auf.")]
    public Task<Aktionsergebnis> FestlegungAufheben() => s.FestlegungAufhebenAsync();

    /// <summary>Plan merken.</summary>
    /// <param name="name">Name.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "plan_merken")]
    [Description("Merkt den besten Plan unter einem Namen (auch in der Oberfläche als gemerkter Plan sichtbar).")]
    public Task<Aktionsergebnis> PlanMerken([Description("Name des Plans.")] string name) => s.MerkenAsync(name);

    /// <summary>CSV exportieren.</summary>
    /// <param name="pfad">Zieldatei.</param>
    /// <param name="trotzHinweisen">Trotz Hinweisen exportieren.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "csv_exportieren")]
    [Description("Exportiert den aktuellen Plan als CSV-Datei für den Import in click-TT. Hat der Plan harte Fehler bzw. Hinweise, wird ohne trotzHinweisen nicht exportiert (die Hinweise stehen in den Meldungen). Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> CsvExportieren(
        [Description("Vollständiger Pfad der Zieldatei (.csv).")] string pfad,
        [Description("Auch bei harten Fehlern bzw. Hinweisen exportieren.")] bool trotzHinweisen = false) => s.ExportierenAsync(pfad, trotzHinweisen);

    /// <summary>Kompaktspieltage.</summary>
    /// <returns>Der Stand.</returns>
    [McpServerTool(Name = "kompakt", ReadOnly = true)]
    [Description("Kompaktspieltage: Pläne zur Wahl mit Punkten, Erläuterung des gewählten Plans, Spieltage und Hinweise; während der Berechnung der Fortschritt.")]
    public Task<Kompaktstand?> Kompakt() => s.KompaktAsync();

    /// <summary>Kompaktplan wählen.</summary>
    /// <param name="titel">Titel des Plans.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "kompakt_plan_waehlen")]
    [Description("Wählt einen Plan der Kompaktspieltage; er gilt dann für Terminplan, Merken und Export.")]
    public Task<Aktionsergebnis> KompaktPlanWaehlen([Description("Titel des Plans wie in „kompakt“, z. B. Bestes Gesamtpaket.")] string titel) =>
        s.KompaktplanAsync(titel);

    /// <summary>Schritt für Schritt.</summary>
    /// <returns>Die Schritte.</returns>
    [McpServerTool(Name = "schritt_fuer_schritt", ReadOnly = true)]
    [Description("Die Führung „Schritt für Schritt“ der Oberfläche für die aktuelle Lage: alle Schritte und der nächste.")]
    public Task<Schrittfolge> SchrittFuerSchritt() => s.SchritteAsync();

    /// <summary>Gewichtungen.</summary>
    /// <param name="nurGeaenderte">Nur geänderte.</param>
    /// <returns>Die Gewichtungen.</returns>
    [McpServerTool(Name = "gewichtungen", ReadOnly = true)]
    [Description("Die Gewichtungen der Kostenoptimierung: je Plan-Kostenart, je Mannschafts-Kostenart, je Mannschaft und abweichende Kostenarten einzelner Mannschaften (Stufen: Nicht berücksichtigen, sehr wenig, wenig, normal, hoch, sehr hoch, extrem hoch).")]
    public Task<IReadOnlyList<Gewichtungsstand>> Gewichtungen([Description("Nur Gewichtungen, die nicht „normal“ sind.")] bool nurGeaenderte = false) =>
        s.GewichtungenAsync(nurGeaenderte);

    /// <summary>Gewichtung setzen.</summary>
    /// <param name="stufe">Stufe.</param>
    /// <param name="kostenart">Kostenart.</param>
    /// <param name="mannschaft">Mannschaft.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "gewichtung_setzen")]
    [Description("Ändert eine Gewichtung (vor allem für die Kostenoptimierung): nur Kostenart = für alle Mannschaften, nur Mannschaft = alle Kosten dieser Mannschaft, beides = diese Kostenart bei dieser Mannschaft. Gilt sofort, auch für die laufende Generierung. Immer nur eine Gewichtung ändern und dann rechnen lassen. Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> GewichtungSetzen(
        [Description("Stufe: Nicht berücksichtigen (oder „aus“), sehr wenig, wenig, normal, hoch, sehr hoch, extrem hoch.")] string stufe,
        [Description("Name der Kostenart wie in „gewichtungen“, z. B. Sperrtermine; leer für die ganze Mannschaft.")] string kostenart = "",
        [Description("Name der Mannschaft; leer für alle.")] string mannschaft = "") => s.GewichtungSetzenAsync(kostenart, mannschaft, stufe);

    /// <summary>Spielplandaten.</summary>
    /// <returns>Der Überblick.</returns>
    [McpServerTool(Name = "spielplandaten", ReadOnly = true)]
    [Description("Überblick über die Spielplandaten: Mannschaften mit Zahl der Heim- und Sperrtermine, spielfreie Tage, Pflichtspielzeiten, festgelegte Begegnungen, Setzliste.")]
    public Task<Spielplandaten?> Spielplandaten() => s.SpielplandatenAsync();

    /// <summary>Daten einer Mannschaft.</summary>
    /// <param name="name">Name.</param>
    /// <returns>Die Daten.</returns>
    [McpServerTool(Name = "mannschaft", ReadOnly = true)]
    [Description("Die Spielplandaten einer Mannschaft: Heim-, Ausweich- und Sperrtermine mit Uhrzeit und Spiellokal, Gegner nur am Wochenende (60-km-Regel), Bemerkung des Vereins.")]
    public Task<Mannschaftstermine?> Mannschaft([Description("Name der Mannschaft.")] string name) => s.MannschaftAsync(name);

    /// <summary>Termin setzen.</summary>
    /// <param name="mannschaft">Mannschaft.</param>
    /// <param name="datum">Tag.</param>
    /// <param name="art">Art.</param>
    /// <param name="uhrzeit">Uhrzeit.</param>
    /// <param name="spiellokal">Spiellokal.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "termin_setzen")]
    [Description("Setzt einen Termin einer Mannschaft (ersetzt, was an dem Tag eingetragen war): Heimtermin, Ausweichtermin, Sperrtermin oder entfernen. Gilt sofort, auch für die laufende Generierung. Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> TerminSetzen(
        [Description("Name der Mannschaft.")] string mannschaft,
        [Description("Tag, TT.MM.JJJJ.")] string datum,
        [Description("„heim“, „ausweich“, „sperre“ oder „entfernen“.")] string art,
        [Description("Beginn bei Heim- und Ausweichterminen, z. B. 19:30.")] string uhrzeit = "",
        [Description("Nummer des Spiellokals (leer = Standard).")] string spiellokal = "") => s.TerminSetzenAsync(mannschaft, datum, art, uhrzeit, spiellokal);

    /// <summary>Spielfreie Tage.</summary>
    /// <param name="tage">Tage.</param>
    /// <param name="eintragen">Eintragen oder austragen.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "spielfreie_tage")]
    [Description("Trägt spielfreie Tage ein oder aus (an ihnen spielt niemand, z. B. Feiertage, Ferien). Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> SpielfreieTage(
        [Description("Die Tage, TT.MM.JJJJ.")] string[] tage,
        [Description("true = eintragen, false = austragen.")] bool eintragen = true) => s.SpielfreieTageAsync(tage, eintragen);

    /// <summary>Setzliste.</summary>
    /// <param name="aktiv">Aktiv.</param>
    /// <param name="reihenfolge">Reihenfolge.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "setzliste_setzen")]
    [Description("Schaltet die Setzliste ein oder aus und setzt bei Bedarf die erwartete Abschlusstabelle (stärkste Mannschaft zuerst, jede genau einmal). Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> SetzlisteSetzen(
        [Description("Setzliste berücksichtigen.")] bool aktiv,
        [Description("Alle Mannschaften in der erwarteten Reihenfolge; leer = Reihenfolge behalten.")] string[]? reihenfolge = null) =>
        s.SetzlisteAsync(reihenfolge ?? [], aktiv);

    /// <summary>Nur am Wochenende.</summary>
    /// <param name="mannschaft">Gast.</param>
    /// <param name="gegner">Gastgeber.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "nur_wochenende")]
    [Description("Legt fest, bei welchen Gastgebern eine Mannschaft auswärts nur am Wochenende spielt (60-km-Regel); ersetzt die bisherige Liste. Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> NurWochenende(
        [Description("Die Gastmannschaft.")] string mannschaft,
        [Description("Die Gastgeber (leer = keine).")] string[] gegner) => s.NurWochenendeAsync(mannschaft, gegner);

    /// <summary>Begegnung festlegen.</summary>
    /// <param name="heim">Heim.</param>
    /// <param name="gast">Gast.</param>
    /// <param name="termin">Termin.</param>
    /// <param name="spiellokal">Spiellokal.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "begegnung_festlegen")]
    [Description("Legt eine Begegnung von Hand fest (die Generierung verschiebt sie nicht). Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> BegegnungFestlegen(
        [Description("Heimmannschaft.")] string heim,
        [Description("Gastmannschaft.")] string gast,
        [Description("Termin, TT.MM.JJJJ HH:MM.")] string termin,
        [Description("Nummer des Spiellokals oder leer.")] string spiellokal = "") => s.BegegnungFestlegenAsync(heim, gast, termin, spiellokal);

    /// <summary>Begegnung löschen.</summary>
    /// <param name="nummer">Nummer.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "begegnung_loeschen")]
    [Description("Löscht eine festgelegte Begegnung (Nummer aus „spielplandaten“). Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> BegegnungLoeschen([Description("Nummer ab 1.")] int nummer) => s.BegegnungLoeschenAsync(nummer);

    /// <summary>Pflichtspielzeit.</summary>
    /// <param name="von">Von.</param>
    /// <param name="bis">Bis.</param>
    /// <param name="anzahl">Anzahl.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "pflichtspielzeit")]
    [Description("Trägt einen Zeitraum ein, in dem jede Mannschaft mindestens so viele Spiele hat (Pflichtspieltage); Anzahl 0 löscht ihn. Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> Pflichtspielzeit(
        [Description("Erster Tag, TT.MM.JJJJ.")] string von,
        [Description("Letzter Tag, TT.MM.JJJJ.")] string bis,
        [Description("Mindestens so viele Spiele (1–10); 0 löscht den Zeitraum.")] int anzahl) => s.PflichtspielzeitAsync(von, bis, anzahl);

    /// <summary>Meldung im Raster der Kompaktspieltage.</summary>
    /// <param name="mannschaft">Mannschaft.</param>
    /// <param name="datum">Tag.</param>
    /// <param name="meldung">Meldung.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "kompakt_meldung")]
    [Description("Setzt bei Kompaktspieltagen die Meldung einer Mannschaft an einem Tag: A auswärts, H nur Heim, B beides, - kann nicht. Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> KompaktMeldung(
        [Description("Name der Mannschaft.")] string mannschaft,
        [Description("Tag, TT.MM.JJJJ.")] string datum,
        [Description("„A“, „H“, „B“ oder „-“.")] string meldung) => s.KompaktMeldungAsync(mannschaft, datum, meldung);

    /// <summary>Mannschaft anlegen oder ändern.</summary>
    /// <param name="name">Name.</param>
    /// <param name="neuerName">Neuer Name.</param>
    /// <param name="nummer">Nummer.</param>
    /// <param name="id">ID.</param>
    /// <param name="vereinsId">Vereins-ID.</param>
    /// <param name="spiellokal">Spiellokal.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "mannschaft_speichern")]
    [Description("Legt eine Mannschaft an (wenn es den Namen nicht gibt) oder ändert Name, Mannschaftsnummer, IDs aus click-TT und Standard-Spiellokal. Leere Angaben bleiben unverändert. Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> MannschaftSpeichern(
        [Description("Name der bestehenden oder neuen Mannschaft.")] string name,
        [Description("Neuer Name (leer = unverändert).")] string neuerName = "",
        [Description("Mannschaftsnummer im Verein (0 = unverändert).")] int nummer = 0,
        [Description("Mannschafts-ID aus click-TT (bei einer neuen Mannschaft nötig; leer = unverändert).")] string id = "",
        [Description("Vereins-ID aus click-TT (leer = unverändert).")] string vereinsId = "",
        [Description("Standard-Spiellokal 1–5 („-“ = keines, leer = unverändert).")] string spiellokal = "") =>
        s.MannschaftSpeichernAsync(name, neuerName, nummer, id, vereinsId, spiellokal.Trim() switch { "" => null, "-" => string.Empty, var l => l });

    /// <summary>Mannschaft löschen.</summary>
    /// <param name="name">Name.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "mannschaft_loeschen", Destructive = true)]
    [Description("Löscht eine Mannschaft aus der Staffel (mit ihren Terminen und Wünschen). Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> MannschaftLoeschen([Description("Name der Mannschaft.")] string name) => s.MannschaftLoeschenAsync(name);

    /// <summary>Spiellokal setzen.</summary>
    /// <param name="mannschaft">Mannschaft.</param>
    /// <param name="spiellokal">Spiellokal.</param>
    /// <param name="datum">Tag.</param>
    /// <param name="nachbar">Nachbarmannschaft.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "spiellokal_setzen")]
    [Description("Setzt ein Spiellokal (Nummer 1–5, leer = keines): ohne Tag und Nachbar das Standard-Spiellokal der Mannschaft, mit Tag das eines Heimtermins, mit Nachbar das einer Nachbarmannschaft. Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> SpiellokalSetzen(
        [Description("Name der Mannschaft.")] string mannschaft,
        [Description("Nummer des Spiellokals 1–5 oder leer.")] string spiellokal,
        [Description("Tag eines Heimtermins, TT.MM.JJJJ (leer = Standard).")] string datum = "",
        [Description("Bezeichnung einer Nachbarmannschaft (leer = keine).")] string nachbar = "") => s.SpiellokalAsync(mannschaft, spiellokal, datum, nachbar);

    /// <summary>Heimkoppel setzen.</summary>
    /// <param name="mannschaft">Mannschaft.</param>
    /// <param name="datum">Tag.</param>
    /// <param name="wunsch">Wunsch.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "heimkoppel_setzen")]
    [Description("Setzt an einem Heimtermin den Wunsch, ihn mit einem zweiten Heimspiel zu koppeln (zwei Heimspiele am Tag bzw. Doppelspieltag). Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> HeimkoppelSetzen(
        [Description("Name der Mannschaft.")] string mannschaft,
        [Description("Tag des Heimtermins, TT.MM.JJJJ.")] string datum,
        [Description("keiner, moeglich, gewuenscht, hoch, doppel_moeglich, doppel_gewuenscht oder doppel_hoch.")] string wunsch) => s.HeimkoppelAsync(mannschaft, datum, wunsch);

    /// <summary>Auswärtskoppel setzen.</summary>
    /// <param name="mannschaft">Mannschaft.</param>
    /// <param name="gegnerA">Erster Gastgeber.</param>
    /// <param name="gegnerB">Zweiter Gastgeber.</param>
    /// <param name="art">Art.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "auswaertskoppel_setzen")]
    [Description("Legt fest, dass eine Mannschaft die Auswärtsspiele bei zwei Gastgebern zusammenlegen möchte (oder ändert die Art). Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> AuswaertskoppelSetzen(
        [Description("Die reisende Mannschaft.")] string mannschaft,
        [Description("Erster Gastgeber.")] string gegnerA,
        [Description("Zweiter Gastgeber.")] string gegnerB,
        [Description("„am gleichen Tag“, „mit Übernachtung“ oder „am gleichen Tag oder mit Übernachtung“.")] string art = "am gleichen Tag") =>
        s.AuswaertskoppelAsync(mannschaft, gegnerA, gegnerB, art);

    /// <summary>Auswärtskoppel löschen.</summary>
    /// <param name="mannschaft">Mannschaft.</param>
    /// <param name="gegnerA">Erster Gastgeber.</param>
    /// <param name="gegnerB">Zweiter Gastgeber.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "auswaertskoppel_loeschen")]
    [Description("Löscht eine Auswärtskoppel. Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> AuswaertskoppelLoeschen(
        [Description("Die reisende Mannschaft.")] string mannschaft,
        [Description("Erster Gastgeber.")] string gegnerA,
        [Description("Zweiter Gastgeber.")] string gegnerB) => s.AuswaertskoppelLoeschenAsync(mannschaft, gegnerA, gegnerB);

    /// <summary>Nachbarmannschaft anlegen oder ändern.</summary>
    /// <param name="mannschaft">Mannschaft.</param>
    /// <param name="name">Name.</param>
    /// <param name="art">Art.</param>
    /// <param name="nummer">Nummer.</param>
    /// <param name="spiellokal">Spiellokal.</param>
    /// <param name="keineParallelenSpiele">Keine parallelen Spiele.</param>
    /// <param name="paralleleHeimspiele">Parallele Heimspiele.</param>
    /// <param name="bisher">Bisherige Bezeichnung.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "nachbarmannschaft_speichern")]
    [Description("Legt eine Nachbarmannschaft an (eine andere Mannschaft desselben Vereins, die dieselbe Halle nutzt) oder ändert sie. Ihre Spiele trägt nachbarspiel_setzen ein. Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> NachbarmannschaftSpeichern(
        [Description("Die Mannschaft der Staffel.")] string mannschaft,
        [Description("Vereinsname der Nachbarmannschaft.")] string name,
        [Description("Art, z. B. Herren, Damen, Jugend.")] string art,
        [Description("Mannschaftsnummer.")] int nummer = 1,
        [Description("Spiellokal 1–5 oder leer.")] string spiellokal = "",
        [Description("Gleichzeitige Spiele mit ihr vermeiden (z. B. gemeinsame Spieler).")] bool keineParallelenSpiele = false,
        [Description("Gleichzeitige Heimspiele mit ihr erwünscht.")] bool paralleleHeimspiele = false,
        [Description("Bezeichnung der bestehenden Nachbarmannschaft (aus „mannschaft“); leer = neu anlegen.")] string bisher = "") =>
        s.NachbarmannschaftAsync(mannschaft, bisher, name, art, nummer, spiellokal, keineParallelenSpiele, paralleleHeimspiele);

    /// <summary>Nachbarmannschaft löschen.</summary>
    /// <param name="mannschaft">Mannschaft.</param>
    /// <param name="nachbar">Nachbarmannschaft.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "nachbarmannschaft_loeschen")]
    [Description("Löscht eine Nachbarmannschaft mit ihren Spielen. Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> NachbarmannschaftLoeschen(
        [Description("Die Mannschaft der Staffel.")] string mannschaft,
        [Description("Bezeichnung oder Name der Nachbarmannschaft.")] string nachbar) => s.NachbarmannschaftLoeschenAsync(mannschaft, nachbar);

    /// <summary>Spiel einer Nachbarmannschaft.</summary>
    /// <param name="mannschaft">Mannschaft.</param>
    /// <param name="nachbar">Nachbarmannschaft.</param>
    /// <param name="termin">Termin.</param>
    /// <param name="gegner">Gegner.</param>
    /// <param name="heimspiel">Heimspiel.</param>
    /// <param name="spiellokal">Spiellokal.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "nachbarspiel_setzen")]
    [Description("Trägt ein Spiel einer Nachbarmannschaft ein (ersetzt eines zum selben Termin); ohne Gegner wird das Spiel zu diesem Termin gelöscht. Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> NachbarspielSetzen(
        [Description("Die Mannschaft der Staffel.")] string mannschaft,
        [Description("Bezeichnung oder Name der Nachbarmannschaft.")] string nachbar,
        [Description("Termin, TT.MM.JJJJ HH:MM.")] string termin,
        [Description("Gegner der Nachbarmannschaft; leer = Spiel löschen.")] string gegner = "",
        [Description("Die Nachbarmannschaft spielt zu Hause.")] bool heimspiel = true,
        [Description("Spiellokal 1–5 oder leer.")] string spiellokal = "") => s.NachbarspielAsync(mannschaft, nachbar, termin, gegner, heimspiel, spiellokal);

    /// <summary>Heimrecht setzen.</summary>
    /// <param name="mannschaft">Mannschaft.</param>
    /// <param name="heimspieleVorrunde">Heimspiele in der Vorrunde.</param>
    /// <param name="gegner">Gegner.</param>
    /// <param name="runde">Runde.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "heimrecht_setzen")]
    [Description("Setzt das Heimrecht einer Mannschaft: wie viele Heimspiele sie in der Vorrunde hat (-1 = automatisch) und/oder in welcher Runde sie gegen einen Gegner zu Hause spielt. Vorher den Anwender fragen.")]
    public Task<Aktionsergebnis> HeimrechtSetzen(
        [Description("Name der Mannschaft.")] string mannschaft,
        [Description("Heimspiele in der Vorrunde; -1 = automatisch; weglassen = unverändert.")] int? heimspieleVorrunde = null,
        [Description("Gegner (leer = keiner).")] string gegner = "",
        [Description("Heimspiel gegen den Gegner: vorrunde, rueckrunde oder egal.")] string runde = "egal") => s.HeimrechtAsync(mannschaft, heimspieleVorrunde, gegner, runde);

    /// <summary>Terminplan lesen.</summary>
    /// <param name="mannschaft">Mannschaft.</param>
    /// <param name="plan">Plan.</param>
    /// <returns>Der Terminplan.</returns>
    [McpServerTool(Name = "terminplan", ReadOnly = true)]
    [Description("Der Terminplan wie in der Ansicht „Terminplan“: Woche für Woche alle Spiele mit Nummer, Tag, Uhrzeit, Heim – Gast, Runde und Hinweisen (z. B. Ausweichtermin, Sperrtermin), dazu Spiele ohne Termin.")]
    public async Task<Terminplan> Terminplan(
        [Description("Nur die Spiele dieser Mannschaft (leer = alle).")] string mannschaft = "",
        [Description("Plan wie unter „Plaene“ der Antwort (z. B. Laufende Generierung, in click-TT vorhandener Plan, gemerkter Plan); leer = der zuletzt gezeigte.")] string plan = "")
    {
        (Terminplan? t, string? fehler) = await s.TerminplanAsync(mannschaft, plan).ConfigureAwait(false);
        return t ?? throw new McpException(fehler ?? "Kein Plan.");
    }

    /// <summary>Diagramm zeichnen.</summary>
    /// <param name="diagramm">Diagramm.</param>
    /// <param name="plan">Plan.</param>
    /// <returns>Kennzahlen und Bild.</returns>
    [McpServerTool(Name = "diagramm", ReadOnly = true)]
    [Description("Ein Diagramm wie in der Ansicht „Diagramme“ als Bild (PNG) mit Beschreibung und den Verstößen je Mannschaft (Wechsel Heim/Auswärts, enge Termine, zwei Spiele pro Woche, lange Pausen, Abstand Hin-/Rückspiel, Setzliste).")]
    public async Task<CallToolResult> Diagramm(
        [Description("spieltage, wechsel, ausrichter (Kompaktspieltage), spielverteilung, abstand, woche oder setzliste; leer = das erste.")] string diagramm = "",
        [Description("Plan (z. B. Laufende Generierung, in click-TT vorhandener Plan, gemerkter Plan); leer = der zuletzt gezeigte.")] string plan = "")
    {
        (Diagrammbild? bild, string? fehler) = await s.DiagrammAsync(diagramm, plan).ConfigureAwait(false);
        if (bild is null)
        {
            throw new McpException(fehler ?? "Kein Plan.");
        }

        string text = $"{bild.Titel} ({bild.Plan}): {bild.Beschreibung}\nDiagramme: {string.Join(", ", bild.Diagramme)}\n" + string.Join("\n", bild.Kennzahlen);
        return new CallToolResult { Content = [new TextContentBlock { Text = text }, ImageContentBlock.FromBytes(bild.Png, "image/png")] };
    }

    /// <summary>Drucken.</summary>
    /// <param name="pfad">Zieldatei.</param>
    /// <param name="plan">Plan.</param>
    /// <param name="abschnitte">Abschnitte.</param>
    /// <param name="querformat">Querformat.</param>
    /// <returns>Das Ergebnis.</returns>
    [McpServerTool(Name = "drucken")]
    [Description("Schreibt den Ausdruck als PDF wie „Drucken“ in der Oberfläche. Ohne Angaben gilt die Vorgabe (bei Kompaktspieltagen die Übersicht für die Vereine).")]
    public Task<Aktionsergebnis> Drucken(
        [Description("Vollständiger Pfad der PDF-Datei.")] string pfad,
        [Description("Plan wie unter „Startplaene“ (z. B. Laufende Generierung, in click-TT vorhandener Plan, gemerkter Plan); leer = Vorgabe.")] string plan = "",
        [Description("Abschnitte: spielplan, mannschaftsplaene, mannschaftsplaene_mit_nachbarn, terminwuensche, nachbartermine, kosten, diagramme, kompaktspieltage; leer = Vorgabe.")] string[]? abschnitte = null,
        [Description("Querformat (sonst Vorgabe).")] bool? querformat = null) => s.DruckenAsync(pfad, plan, abschnitte ?? [], querformat);
}
