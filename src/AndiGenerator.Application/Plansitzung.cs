// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Persistence.ClickTt;
using AndiGenerator.Persistence.Csv;
using AndiGenerator.Persistence.Gemeinsam;
using AndiGenerator.Persistence.Optionen;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>
/// Eine geöffnete Staffel (Ablauflogik des Original-Hauptfensters ohne Oberfläche): click-TT-Datei samt
/// <c>.modifications</c> bzw. eigene Plandatei, Optionen und gemerkte Pläne im Staffelordner, Bewertung und CSV-Export.
/// </summary>
public sealed class Plansitzung
{
    /// <summary>Name des eigenen Ordners unter <c>%LOCALAPPDATA%</c>.</summary>
    public const string Ordnername = "AndiGenerator.NET";

    /// <summary>Früherer Name des eigenen Ordners (bis zur Umbenennung in AndiGenerator.NET am 29.09.2026).</summary>
    public const string FruehererOrdnername = "AndiGeneratorNeu";

    /// <summary>Kosten je hartem Fehler im Original (10 × cMaxKostenNoHardError).</summary>
    private const double KostenJeHartemFehler = 10.0 * 1_000_000_000.0;
    private const string Uebernahmevermerk = "uebernommen-aus-AndiGeneratorNeu.txt";

    private readonly DatenKnoten stand;
    private bool standardKriterienstufen;

    private Plansitzung(string pfad, DatenKnoten stand, bool istClickTt, bool ersterStart, string basisordner, string? uebernahmeAus)
    {
        Pfad = pfad;
        this.stand = stand;
        IstClickTtDatei = istClickTt;
        ErsterStart = ersterStart;
        Staffel = StaffelAbbildung.AusPlanDaten(stand);
        Staffelordner = Persistence.Gemeinsam.Staffelordner.Pfad(basisordner, Staffel.Name, Staffel.Beginn);
        if (uebernahmeAus is not null && !Directory.Exists(Staffelordner))
        {
            UebernommenAus = Uebernehmen(Persistence.Gemeinsam.Staffelordner.Pfad(uebernahmeAus, Staffel.Name, Staffel.Beginn), Staffelordner);
        }

        string optionsdatei = Path.Combine(Staffelordner, OptionenDatei.Dateiname);
        Optionen = File.Exists(optionsdatei) ? OptionenDatei.Laden(optionsdatei) : Berechnungsoptionen.Standard;
    }

    /// <summary>Pfad der geöffneten Datei.</summary>
    public string Pfad { get; }

    /// <summary>Die Datei ist ein click-TT-Export (Änderungen stehen dann in der <c>.modifications</c>-Datei daneben).</summary>
    public bool IstClickTtDatei { get; }

    /// <summary>click-TT-Datei ohne <c>.modifications</c>: das Original startet dann den Erststart-Assistenten.</summary>
    public bool ErsterStart { get; }

    /// <summary>Die Staffel (Stammdaten mit allen Änderungen; bestehender Plan = „in Click-TT vorhandener Plan“).</summary>
    public Staffel Staffel { get; private set; }

    /// <summary>Ordner für Optionen und gemerkte Pläne (gleicher Aufbau wie beim Original, Basis siehe <see cref="EigeneBasis"/>).</summary>
    public string Staffelordner { get; }

    /// <summary>Staffelordner des Originals, aus dem Optionen und gemerkte Pläne beim ersten Öffnen kopiert wurden; sonst <c>null</c>.</summary>
    public string? UebernommenAus { get; }

    /// <summary>Aktuelle Berechnungsoptionen.</summary>
    public Berechnungsoptionen Optionen { get; private set; }

    /// <summary>Die Optionen weichen von den Standardoptionen ab (Original <c>getDiffToDefault</c>).</summary>
    public bool OptionenWeichenVomStandardAb => !Gleich(Optionen, Berechnungsoptionen.Standard);

    /// <summary>
    /// Basis der Staffelordner von AndiGenerator.NET: <c>%LOCALAPPDATA%\AndiGenerator.NET</c>. Die Daten des Originals
    /// (<c>%LOCALAPPDATA%\Andi-Generator</c>) werden nur gelesen und beim ersten Öffnen einer Staffel kopiert, nie verändert.
    /// </summary>
    /// <returns>Der Pfad.</returns>
    public static string EigeneBasis() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Ordnername);

    /// <summary>
    /// Liefert <see cref="EigeneBasis"/> und übernimmt dabei einmalig die Staffelordner aus dem früheren eigenen Ordner
    /// <c>%LOCALAPPDATA%\AndiGeneratorNeu</c>. Scheitert das Kopieren, wird ohne die alten Daten weitergearbeitet.
    /// </summary>
    /// <returns>Der Pfad.</returns>
    public static string EigeneBasisVorbereiten()
    {
        string basis = EigeneBasis();
        try
        {
            FruehereDatenUebernehmen(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FruehererOrdnername), basis);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            // Die alten Daten bleiben unverändert liegen; der Anwender kann sie von Hand kopieren.
        }

        return basis;
    }

    /// <summary>
    /// Kopiert einmalig alles außer den Build-Ausgaben (<c>artifacts</c>) aus einem früheren eigenen Ordner in die Basis.
    /// Vorhandene Dateien werden nie überschrieben, der frühere Ordner wird nicht verändert. Ein Vermerk in der Basis
    /// verhindert, dass später gelöschte Pläne wieder auftauchen.
    /// </summary>
    /// <param name="frueher">Der frühere Ordner.</param>
    /// <param name="basis">Die neue Basis.</param>
    /// <returns><c>true</c>, wenn übernommen wurde.</returns>
    public static bool FruehereDatenUebernehmen(string frueher, string basis)
    {
        ArgumentException.ThrowIfNullOrEmpty(frueher);
        ArgumentException.ThrowIfNullOrEmpty(basis);
        string vermerk = Path.Combine(basis, Uebernahmevermerk);
        if (!Directory.Exists(frueher) || File.Exists(vermerk))
        {
            return false;
        }

        IEnumerable<string> ordner = Directory.EnumerateDirectories(frueher)
            .Where(o => !string.Equals(Path.GetFileName(o), "artifacts", StringComparison.OrdinalIgnoreCase));
        foreach (string quelle in ordner)
        {
            Zusammenfuehren(quelle, Path.Combine(basis, Path.GetFileName(quelle)));
        }

        Directory.CreateDirectory(basis);
        string zeitpunkt = DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        File.WriteAllText(vermerk, $"Staffelordner übernommen aus {frueher} am {zeitpunkt}.{Environment.NewLine}");
        return true;
    }

    /// <summary>Basis der Staffelordner des Originals (<c>%LOCALAPPDATA%</c>).</summary>
    /// <returns>Der Pfad.</returns>
    public static string OriginalBasis() => Persistence.Gemeinsam.Staffelordner.StandardBasis();

    /// <summary>Öffnet eine click-TT-Exportdatei oder eine eigene Plandatei (Original <c>OpenFile</c>).</summary>
    /// <param name="pfad">Pfad der Datei.</param>
    /// <param name="basisordner">Basis der Staffelordner; <c>null</c> = <c>%LOCALAPPDATA%</c> wie im Original.</param>
    /// <param name="uebernahmeAus">
    /// Basis, aus der Optionen und gemerkte Pläne kopiert werden, wenn der Staffelordner noch nicht existiert
    /// (z. B. <see cref="OriginalBasis"/>); <c>null</c> = keine Übernahme.
    /// </param>
    /// <returns>Die geöffnete Staffel.</returns>
    /// <exception cref="ClickTtFormatException">Die click-TT-Datei ist ungültig.</exception>
    /// <exception cref="PlanDatenFormatException">Die Plandatei oder die <c>.modifications</c> ist ungültig.</exception>
    public static Plansitzung Oeffnen(string pfad, string? basisordner = null, string? uebernahmeAus = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(pfad);
        string basis = basisordner ?? Persistence.Gemeinsam.Staffelordner.StandardBasis();
        if (!ClickTtLeser.IstClickTtDatei(pfad))
        {
            return new Plansitzung(pfad, PlanDatenDatei.Laden(pfad), istClickTt: false, ersterStart: false, basis, uebernahmeAus);
        }

        DatenKnoten stand = ClickTtNachPlanDaten.Laden(pfad);
        string modifikationen = ModifikationenPfad(pfad);
        bool vorhanden = File.Exists(modifikationen);
        if (vorhanden)
        {
            stand.Zusammenfuehren(PlanDatenDatei.Laden(modifikationen));
        }

        return new Plansitzung(pfad, stand, istClickTt: true, ersterStart: !vorhanden, basis, uebernahmeAus);
    }

    /// <summary>
    /// Legt eine leere eigene Plandatei zur komplett manuellen Eingabe an (Original <c>MenuNewClick</c>:
    /// <c>&lt;andigenerator&gt;&lt;plan/&gt;&lt;/andigenerator&gt;</c>). Eine vorhandene Datei wird wie im Original überschrieben.
    /// </summary>
    /// <param name="pfad">Pfad der neuen Datei.</param>
    /// <exception cref="IOException">Die Datei konnte nicht geschrieben werden.</exception>
    public static void NeueDateiAnlegen(string pfad)
    {
        ArgumentException.ThrowIfNullOrEmpty(pfad);
        PlanDatenDatei.Speichern(pfad, new DatenKnoten("plan"));
    }

    /// <summary>Pfad der Änderungsdatei zu einer click-TT-Datei (Original <c>cModificationExtension</c>).</summary>
    /// <param name="pfad">Pfad der click-TT-Datei.</param>
    /// <returns>Gleicher Pfad mit der Endung <c>.modifications</c>.</returns>
    public static string ModifikationenPfad(string pfad) => Path.ChangeExtension(pfad, ".modifications");

    /// <summary>Erzeugt eine Arbeitskopie der Spielplandaten für den Dialog „Spielplandaten bearbeiten“.</summary>
    /// <returns>Die Arbeitskopie; der Standard ist bei click-TT-Dateien der unveränderte Import.</returns>
    public Datenbearbeitung DatenBearbeiten() =>
        new(stand, IstClickTtDatei ? ClickTtNachPlanDaten.Laden(Pfad) : null);

    /// <summary>
    /// Übernimmt eine bearbeitete Arbeitskopie wie das Original (<c>DoOnAfterPlanChanged</c> mit <c>SavePlan</c>): Bei
    /// click-TT-Dateien wird die Differenz zum Import als <c>.modifications</c> gespeichert, eigene Plandateien werden
    /// ganz gespeichert. Danach gelten die neuen Stammdaten; der Staffelordner bleibt wie im Original derselbe.
    /// </summary>
    /// <param name="bearbeitung">Die Arbeitskopie aus <see cref="DatenBearbeiten"/>.</param>
    /// <exception cref="IOException">Die Datei konnte nicht geschrieben werden.</exception>
    public void DatenUebernehmen(Datenbearbeitung bearbeitung)
    {
        ArgumentNullException.ThrowIfNull(bearbeitung);
        Staffel neu = StaffelAbbildung.AusPlanDaten(bearbeitung.Arbeitsstand);
        if (IstClickTtDatei)
        {
            DatenKnoten differenz = DatenKnoten.Differenz(bearbeitung.Arbeitsstand, ClickTtNachPlanDaten.Laden(Pfad));
            PlanDatenDatei.Speichern(ModifikationenPfad(Pfad), differenz);
        }
        else
        {
            PlanDatenDatei.Speichern(Pfad, bearbeitung.Arbeitsstand);
        }

        stand.Zuweisen(bearbeitung.Arbeitsstand);
        Staffel = standardKriterienstufen ? neu with { Kriterienstufen = string.Empty } : neu;
    }

    /// <summary>
    /// Speichert die vom Staffelleiter gewählte Einteilung der Kriterien (Stufen A, B, C mit Reihenfolge) mit den
    /// Spielplandaten: bei click-TT-Dateien in der <c>.modifications</c>, sonst in der Plandatei (Attribut am Knoten
    /// <c>plan</c>, das der alte AndiGenerator übernimmt, ohne es zu beachten).
    /// </summary>
    /// <param name="text">Die Einteilung als Text (<c>Stufeneinteilung.Text</c>).</param>
    /// <exception cref="IOException">Die Datei konnte nicht geschrieben werden.</exception>
    public void KriterienstufenSpeichern(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        standardKriterienstufen = false;
        Datenbearbeitung bearbeitung = DatenBearbeiten();
        bearbeitung.Arbeitsstand.Setzen(StaffelAbbildung.KriterienAttribut, text);
        DatenUebernehmen(bearbeitung);
    }

    /// <summary>
    /// Rechnet in dieser Sitzung mit der Standard-Einteilung der Kriterien, ohne die gespeicherte zu überschreiben (wie
    /// <see cref="StandardoptionenFuerDieseSitzung"/>). Ändert der Staffelleiter die Einteilung, gilt und speichert er seine.
    /// </summary>
    public void StandardKriterienstufenFuerDieseSitzung()
    {
        standardKriterienstufen = true;
        Staffel = Staffel with { Kriterienstufen = string.Empty };
    }

    /// <summary>Übernimmt neue Optionen und speichert sie im Staffelordner.</summary>
    /// <param name="neu">Neue Optionen.</param>
    public void OptionenSpeichern(Berechnungsoptionen neu)
    {
        ArgumentNullException.ThrowIfNull(neu);
        Directory.CreateDirectory(Staffelordner);
        OptionenDatei.Speichern(Path.Combine(Staffelordner, OptionenDatei.Dateiname), neu);
        Optionen = neu;
    }

    /// <summary>
    /// Rechnet in dieser Sitzung mit den Standardoptionen, ohne die gespeicherten Optionen zu überschreiben.
    /// Das Original setzt sie in diesem Fall zurück und speichert sofort (Befund #34); hier bleibt die Datei unverändert.
    /// </summary>
    public void StandardoptionenFuerDieseSitzung() => Optionen = Berechnungsoptionen.Standard;

    /// <summary>Vorschlag zur Rundenplanung wie im Original beim Öffnen (ohne die fest einkodierte Corona-Abfrage 2020).</summary>
    /// <param name="heute">Heutiges Datum.</param>
    /// <returns>Der Vorschlag oder <see cref="Rundenvorschlag.Keiner"/>.</returns>
    public Rundenvorschlag RundeVorschlagen(DateOnly heute)
    {
        if (Staffel.Beginn is DateOnly beginn && Staffel.Ende is DateOnly ende
            && Math.Abs(ende.DayNumber - beginn.DayNumber) < 6 * 30
            && Optionen.Rundenplanung != Rundenplanung.Halbrunde)
        {
            return Rundenvorschlag.Halbrunde;
        }

        if (Staffel.Rueckrundenbeginn is DateOnly rueckrunde
            && Math.Abs(heute.DayNumber - rueckrunde.DayNumber) < 60
            && Optionen.Rundenplanung is not (Rundenplanung.NurRueckrunde or Rundenplanung.Corona))
        {
            return Rundenvorschlag.NurRueckrunde;
        }

        return Rundenvorschlag.Keiner;
    }

    /// <summary>Gemerkte Pläne im Staffelordner, neueste zuerst.</summary>
    /// <returns>Die Einträge.</returns>
    public IReadOnlyList<GemerkterPlanEintrag> GemerktePlaene() => Persistence.Gemeinsam.Staffelordner.GemerktePlaene(Staffelordner);

    /// <summary>Speichert einen Plan unter einem Namen (Original „Plan merken“; ein gleichnamiger Plan wird ersetzt).</summary>
    /// <param name="name">Anzeigename.</param>
    /// <param name="spiele">Spiele des Plans.</param>
    /// <returns>Der neue Eintrag.</returns>
    public GemerkterPlanEintrag PlanMerken(string name, IEnumerable<Spiel> spiele)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(spiele);
        Directory.CreateDirectory(Staffelordner);
        string pfad = Persistence.Gemeinsam.Staffelordner.GemerkterPlanPfad(Staffelordner, name.Trim());
        GespeicherterPlanDatei.Speichern(pfad, spiele.Select(s => new GespeichertesSpiel(s.Zeitpunkt, s.Heim, s.Gast)));
        return new GemerkterPlanEintrag(name.Trim(), pfad, File.GetLastWriteTime(pfad));
    }

    /// <summary>Lädt die Spiele eines gemerkten Plans.</summary>
    /// <param name="eintrag">Eintrag aus <see cref="GemerktePlaene"/>.</param>
    /// <returns>Die Spiele (ohne Spiellokal).</returns>
    /// <exception cref="PlanDatenFormatException">Die Datei ist ungültig.</exception>
    public IReadOnlyList<Spiel> GemerktenPlanLaden(GemerkterPlanEintrag eintrag)
    {
        PruefenGehoertZurStaffel(eintrag);
        return GespeicherterPlanDatei.Laden(eintrag.Pfad).Select(s => new Spiel(s.Zeitpunkt, s.Heim, s.Gast, string.Empty)).ToList();
    }

    /// <summary>Entfernt einen gemerkten Plan.</summary>
    /// <param name="eintrag">Eintrag aus <see cref="GemerktePlaene"/>.</param>
    public void GemerktenPlanEntfernen(GemerkterPlanEintrag eintrag)
    {
        PruefenGehoertZurStaffel(eintrag);
        File.Delete(eintrag.Pfad);
    }

    /// <summary>Bewertet einen Plan dieser Staffel wie der Kosten-Tab des Originals (mit Meldungen).</summary>
    /// <param name="spiele">Spiele des Plans.</param>
    /// <returns>Die Bewertung.</returns>
    public Planbewertung Bewerten(IReadOnlyList<Spiel> spiele) =>
        Referenzbewertung.Bewerten(Staffel with { BestehenderSpielplan = spiele }, Optionen);

    /// <summary>Terminwünsche aller Mannschaften mit Auswertung (Tab „Terminwünsche“ des Originals, unabhängig vom Plan).</summary>
    /// <returns>Die Übersicht.</returns>
    public Terminwunschuebersicht Terminwuensche() => Terminwunschauswertung.Ermitteln(Staffel, Optionen);

    /// <summary>Spiele der Nachbarmannschaften je Mannschaft (Tab „Termine der Nachbarmannschaften“ des Originals).</summary>
    /// <returns>Je Mannschaft die Nachbartermine nach Tagen.</returns>
    public IReadOnlyList<MannschaftsNachbartermine> Nachbartermine() => Nachbarterminauswertung.Ermitteln(Staffel, Optionen);

    /// <summary>Daten der Diagramme (Tab „Diagramme“ des Originals) für einen Plan.</summary>
    /// <param name="spiele">Spiele des Plans.</param>
    /// <returns>Die Diagrammdaten.</returns>
    public Diagrammdaten Diagramme(IReadOnlyList<Spiel> spiele) =>
        Diagrammauswertung.Ermitteln(Staffel with { BestehenderSpielplan = spiele }, Optionen);

    /// <summary>Terminplan mit den Hinweisen und Nachbarterminen des Originals (Tab „Terminplan“).</summary>
    /// <param name="spiele">Spiele des Plans.</param>
    /// <returns>Die notwendigen Spiele in der Reihenfolge des Originals.</returns>
    public IReadOnlyList<Terminplanzeile> Terminplan(IReadOnlyList<Spiel> spiele) =>
        Terminplanauswertung.Zeilen(Staffel with { BestehenderSpielplan = spiele }, Optionen);

    /// <summary>Abweichungen der aktuellen Optionen vom Standard (Rückfrage beim Öffnen wie im Original).</summary>
    /// <returns>Die Meldungen; leer, wenn alles Standard ist.</returns>
    public IReadOnlyList<string> AbweichungenVomStandard() => Optionsabweichungen.Meldungen(Optionen, Staffel);

    /// <summary>Hinweise für den ersten Start (nur sinnvoll, wenn <see cref="ErsterStart"/> gesetzt ist).</summary>
    /// <returns>Die Hinweise.</returns>
    public IReadOnlyList<Erststarthinweis> Erststarthinweise() => Erststart.Hinweise(Staffel);

    /// <summary>Automatisch ermittelte Enddaten der 1. und 3. Viertelrunde für die Doppelrunde.</summary>
    /// <returns>Die beiden Daten.</returns>
    public (DateOnly Mitte1, DateOnly Mitte2) AutomatischeRundenmitten() => Referenzbewertung.AutomatischeRundenmitten(Staffel, Optionen);

    /// <summary>Übernimmt einen Rundenvorschlag in die Optionen und speichert sie (Original: Rückfrage beim Öffnen).</summary>
    /// <param name="vorschlag">Der Vorschlag; <see cref="Rundenvorschlag.Keiner"/> ändert nichts.</param>
    public void RundeUebernehmen(Rundenvorschlag vorschlag)
    {
        Rundenplanung? neu = vorschlag switch
        {
            Rundenvorschlag.Halbrunde => Rundenplanung.Halbrunde,
            Rundenvorschlag.NurRueckrunde => Rundenplanung.NurRueckrunde,
            _ => null,
        };
        if (neu is Rundenplanung runde)
        {
            OptionenSpeichern(Optionen with { Rundenplanung = runde });
        }
    }

    /// <summary>Meldungen einer Mannschaft zu einer Kostenart (Dialog beim Klick auf eine Zelle der Kostentabelle im Original).</summary>
    /// <param name="spiele">Spiele des Plans.</param>
    /// <param name="mannschaftsName">Name der Mannschaft.</param>
    /// <param name="art">Kostenart.</param>
    /// <returns>Die Meldungen; leer, wenn keine anfallen.</returns>
    public IReadOnlyList<string> Meldungen(IReadOnlyList<Spiel> spiele, string mannschaftsName, MannschaftsKostenart art) =>
        Referenzbewertung.Meldungen(Staffel with { BestehenderSpielplan = spiele }, Optionen, mannschaftsName, art);

    /// <summary>Meldungen aller Mannschaften zu einer Kostenart (Einzelheiten eines Kriteriums in der Qualitätsansicht).</summary>
    /// <param name="spiele">Spiele des Plans.</param>
    /// <param name="art">Kostenart.</param>
    /// <returns>Je Mannschaft ihre Meldungen.</returns>
    public IReadOnlyList<Mannschaftsmeldungen> MeldungenJeMannschaft(IReadOnlyList<Spiel> spiele, MannschaftsKostenart art) =>
        Referenzbewertung.MeldungenJeMannschaft(Staffel with { BestehenderSpielplan = spiele }, Optionen, art);

    /// <summary>Ändert eine Gewichtung und speichert die Optionen im Staffelordner (wie das Original nach dem Gewichtungsdialog).</summary>
    /// <param name="ziel">Die Gewichtung.</param>
    /// <param name="wert">Der neue Wert.</param>
    public void GewichtungAendern(Gewichtungsziel ziel, Gewichtung wert)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        OptionenSpeichern(ziel.Setzen(Optionen, wert));
    }

    /// <summary>Meldungen zu harten Fehlern vor dem Export (Original <c>getHardErrorMessages</c>).</summary>
    /// <param name="spiele">Spiele des Plans.</param>
    /// <returns>Die Meldungen; leer, wenn der Plan keine harten Fehler hat.</returns>
    public IReadOnlyList<string> HarteFehler(IReadOnlyList<Spiel> spiele)
    {
        Planbewertung bewertung = Bewerten(spiele);
        var meldungen = new List<string>();
        Hinzufuegen(meldungen, bewertung.NichtTerminiert, "Spiel(e) ohne Termin");
        Hinzufuegen(meldungen, bewertung.UngueltigeSpiele, "Spiel(e) mit ungültigem Termin");
        Hinzufuegen(meldungen, bewertung.SpieleAnSpielfreienTagen, "Spiel(e) an spielfreien Tagen");
        return meldungen;
    }

    /// <summary>Exportiert einen Plan als CSV für den Import in click-TT (Windows-1252, wie das Original).</summary>
    /// <param name="pfad">Zieldatei.</param>
    /// <param name="spiele">Spiele des Plans.</param>
    public void CsvExportieren(string pfad, IReadOnlyList<Spiel> spiele)
    {
        ArgumentException.ThrowIfNullOrEmpty(pfad);
        ArgumentNullException.ThrowIfNull(spiele);
        bool halbrunde = Optionen.Rundenplanung == Rundenplanung.Halbrunde;
        var lokale = new Spiellokale(stand, halbrunde);
        DateTime beginn = (Staffel.Beginn ?? DateOnly.FromDateTime(DelphiKompatibel.DelphiNull)).ToDateTime(TimeOnly.MinValue);
        DateTime rueckrunde = (Staffel.Rueckrundenbeginn ?? DateOnly.FromDateTime(DelphiKompatibel.DelphiNull)).ToDateTime(TimeOnly.MinValue);
        var optionen = new CsvExportOptionen(beginn, rueckrunde, halbrunde, Rundenpruefung.Erzeugen(Staffel, Optionen));
        ClickTtCsvExport.Schreiben(
            pfad,
            spiele.Select(s => new CsvSpiel(s.Zeitpunkt, s.Heim, s.Gast, lokale.Ermitteln(s.Zeitpunkt, s.Heim, s.Gast))),
            Staffel.Mannschaften.Select(m => new CsvMannschaft(m.Name, m.VereinsId, m.Id)),
            optionen);
    }

    private static bool Gleich(Berechnungsoptionen a, Berechnungsoptionen b) =>
        OptionenDatei.ErzeugenBytes(a).AsSpan().SequenceEqual(OptionenDatei.ErzeugenBytes(b));

    private static void Hinzufuegen(List<string> meldungen, double kosten, string text)
    {
        int anzahl = (int)Math.Round(kosten / KostenJeHartemFehler);
        if (anzahl > 0)
        {
            meldungen.Add($"{anzahl} {text}");
        }
    }

    private static void Zusammenfuehren(string quelle, string ziel)
    {
        Directory.CreateDirectory(ziel);
        foreach (string datei in Directory.EnumerateFiles(quelle).Where(d => !File.Exists(Path.Combine(ziel, Path.GetFileName(d)))))
        {
            File.Copy(datei, Path.Combine(ziel, Path.GetFileName(datei)));
        }

        foreach (string ordner in Directory.EnumerateDirectories(quelle))
        {
            Zusammenfuehren(ordner, Path.Combine(ziel, Path.GetFileName(ordner)));
        }
    }

    /// <summary>Kopiert Optionen und gemerkte Pläne (nur Dateien der obersten Ebene) in einen neuen Staffelordner.</summary>
    /// <param name="quelle">Staffelordner des Originals.</param>
    /// <param name="ziel">Neuer Staffelordner.</param>
    /// <returns>Der Quellordner, wenn kopiert wurde; sonst <c>null</c>.</returns>
    private static string? Uebernehmen(string quelle, string ziel)
    {
        if (!Directory.Exists(quelle))
        {
            return null;
        }

        Directory.CreateDirectory(ziel);
        IEnumerable<string> dateien = Directory.EnumerateFiles(quelle).Where(d =>
            Path.GetFileName(d) == OptionenDatei.Dateiname || string.Equals(Path.GetExtension(d), ".xml", StringComparison.OrdinalIgnoreCase));
        foreach (string datei in dateien)
        {
            File.Copy(datei, Path.Combine(ziel, Path.GetFileName(datei)), overwrite: false);
        }

        return quelle;
    }

    private void PruefenGehoertZurStaffel(GemerkterPlanEintrag eintrag)
    {
        ArgumentNullException.ThrowIfNull(eintrag);
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(eintrag.Pfad)), Path.GetFullPath(Staffelordner), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Der Plan gehört nicht zu dieser Staffel.", nameof(eintrag));
        }
    }
}
