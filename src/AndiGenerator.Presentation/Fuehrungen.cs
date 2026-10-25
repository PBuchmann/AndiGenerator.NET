// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>
/// Die Führungen „Schritt für Schritt“ (Peter, 24.10.2026): je Art der Optimierung die Schritte mit dem Element, auf das sie
/// zeigen, und dem Abschnitt der Anleitung. Die Anleitung nennt dieselben Schritte (Test).
/// </summary>
public static class Fuehrungen
{
    private static IReadOnlyList<Fuehrungsschritt> Kosten { get; } =
    [
        new(
            "Verfahren und Ausgangsplan wählen",
            "Wählen Sie oben rechts unter „Verfahren“ die Kostenoptimierung. Unter „Start mit“ legen Sie fest, wovon die Suche ausgeht: von einem leeren Plan, dem Plan aus click-TT oder einem gemerkten Plan.",
            Fuehrungsziel.Verfahren,
            "5.1")
        {
            Erledigt = h => !h.Automodus && h.HatLaufendenPlan,
        },
        new(
            "Generierung starten",
            "Der blaue Knopf ▶ startet die Suche. Sie rechnet mit allen Kernen bis auf einen und zeigt laufend den besten Plan; nach wenigen Sekunden steht ein erster Plan.",
            Fuehrungsziel.Start,
            "5.3")
        {
            Erledigt = h => !h.Automodus && h.HatLaufendenPlan,
        },
        new(
            "Kacheln lesen",
            "Die Kacheln zeigen die Gesamtkosten des besten Plans, ob die Pflichtregeln (Stufe A) eingehalten sind und wie oft der Plan besser wurde. Solange Verbesserungen schnell kommen: einfach rechnen lassen.",
            Fuehrungsziel.Kacheln,
            "5.2"),
        new(
            "Teuerste Kosten finden",
            "In der Ansicht „Kosten“ stehen die teuersten Mannschaften und Kostenarten links oben; je röter eine Zelle, desto mehr kostet sie. Ein Klick auf eine Zelle zeigt rechts, was genau verletzt ist.",
            Fuehrungsziel.KostenMatrix,
            "5.7")
        {
            Vorbereiten = h => Ausfuehren(h.KostenansichtCommand),
        },
        new(
            "Verstöße nachlesen",
            "Die Ansicht „Meldungen“ listet je Mannschaft jeden Verstoß mit Datum – so sehen Sie, ob ein Verstoß wirklich stört.",
            Fuehrungsziel.NavMeldungen,
            "5.9"),
        new(
            "Gewichtung anpassen",
            "Stört ein Verstoß, heben Sie seine Gewichtung an: in der Kostenansicht im Detailfeld rechts (Kostenart, Mannschaft oder eine Zelle) oder in den Einstellungen. Ändern Sie immer nur eine Gewichtung; die laufende Suche übernimmt sie sofort.",
            Fuehrungsziel.KostenMatrix,
            "5.10")
        {
            Vorbereiten = h => Ausfuehren(h.KostenansichtCommand),
        },
        new(
            "Fortschritt beobachten",
            "Die Kachel „Verbesserungen“ öffnet den Verlauf, „Berechnete Pläne“ den Leitstand. Wird die Kurve flach und kommen kaum noch Verbesserungen, bringt längeres Rechnen wenig.",
            Fuehrungsziel.KachelVerlauf,
            "5.6"),
        new(
            "Qualität und Diagramme prüfen",
            "Die Ansicht „Qualität“ zeigt, ob die Pflichtregeln eingehalten sind und wie viele Verstöße je Kriterium bleiben; die Diagramme zeigen Spieltage, Wechsel Heim/Auswärts und Spielverteilung auf einen Blick.",
            Fuehrungsziel.NavQualitaet,
            "5.8"),
        new(
            "Anhalten und Plan merken",
            "Passt der Plan, halten Sie mit ⏸ an und merken ihn unter einem Namen. Gemerkte Pläne lassen sich später vergleichen oder als Ausgangsplan wählen.",
            Fuehrungsziel.PlanMerken,
            "5.11"),
        new(
            "Exportieren und drucken",
            "„Für click-TT exportieren“ schreibt den Plan als CSV-Datei für den Import in click-TT; „Drucken“ erzeugt ein PDF.",
            Fuehrungsziel.NavExport,
            "8.1"),
    ];

    private static IReadOnlyList<Fuehrungsschritt> Auto { get; } =
    [
        new(
            "Automodus wählen",
            "Wählen Sie oben rechts unter „Verfahren“ den Automodus. Er lenkt die Gewichte selbst: erst die Pflichtstufe A, dann B, danach glättet er C.",
            Fuehrungsziel.Verfahren,
            "5.4")
        {
            Erledigt = h => h.Automodus,
        },
        new(
            "Einteilung prüfen",
            "In der Ansicht „Qualität“ legen Sie fest, welche Kriterien in A (muss), B (soll) und C (wird geglättet) stehen: mit den Knöpfen A, B, C und den Pfeilen oder durch Ziehen am Griff links. Die Standard-Einteilung passt für die meisten Staffeln.",
            Fuehrungsziel.QualitaetTabelle,
            "5.8")
        {
            Vorbereiten = h => Ausfuehren(h.QualitaetsansichtCommand),
        },
        new(
            "Generierung starten",
            "Der blaue Knopf ▶ startet den Automodus. Er läuft ohne Zeitgrenze, bis Sie anhalten oder stoppen.",
            Fuehrungsziel.Start,
            "5.3")
        {
            Erledigt = h => h.Automodus && h.HatLaufendenPlan,
        },
        new(
            "Phasen verfolgen",
            "Die Ergebniskachel zeigt die Phase (Basisoptimierung, Stufe A, B, C) und die Verstöße je Stufe; rechts rechnet HiGHS mit. Warten Sie, bis die Basisoptimierung vorbei ist.",
            Fuehrungsziel.KachelErgebnis,
            "5.4")
        {
            Erledigt = h => h.Automodus && h.Anzeige.Titel.Contains("Stufe", StringComparison.Ordinal),
        },
        new(
            "Bestmöglich ablesen",
            "In der Qualität steht je Kriterium, wie viele Verstöße jeder Plan mindestens hat. Ein grüner Haken heißt: besser geht es nicht; „Optimum erreicht“ über einer Stufe heißt, weiteres Rechnen bringt dort nichts mehr.",
            Fuehrungsziel.QualitaetTabelle,
            "5.8")
        {
            Vorbereiten = h => Ausfuehren(h.QualitaetsansichtCommand),
            Erledigt = h => h.Automodus && h.Anzeige.Titel.Contains("Stufe C", StringComparison.Ordinal),
        },
        new(
            "B-Varianten prüfen",
            "Hat HiGHS Varianten für Stufe B gefunden, zeigt die Kachel „B-Varianten wählen“. Im Dialog vergleichen Sie sie mit dem Plan; „Übernehmen“ legt A und B fest, danach glättet der Automodus nur noch C. Ohne Knopf gibt es keine bessere Variante – weiter.",
            Fuehrungsziel.BVarianten,
            "5.8")
        {
            Erledigt = h => h.Festlegung is not null,
        },
        new(
            "C glätten lassen",
            "Nun glättet der Automodus die übrigen Kriterien. Verlauf und Leitstand (Kacheln) zeigen, ob noch etwas vorangeht; werden die Verbesserungen selten, ist der Plan fertig.",
            Fuehrungsziel.KachelVerlauf,
            "5.6"),
        new(
            "Stoppen und Plan merken",
            "Mit ■ beenden Sie den Automodus; der beste Plan bleibt erhalten. Merken Sie ihn unter einem Namen, um ihn später zu vergleichen.",
            Fuehrungsziel.PlanMerken,
            "5.11"),
        new(
            "Exportieren und drucken",
            "„Für click-TT exportieren“ schreibt den Plan als CSV-Datei für den Import in click-TT; „Drucken“ erzeugt ein PDF.",
            Fuehrungsziel.NavExport,
            "8.1"),
    ];

    private static IReadOnlyList<Fuehrungsschritt> Kompakt { get; } =
    [
        new(
            "Ansicht Kompaktspieltage öffnen",
            "Links unter „Ansichten“ öffnet „Kompaktspieltage“ die Hauptansicht: Pläne, Raster „Meldungen und Plan“, Karte, Spieltage und Hinweise.",
            Fuehrungsziel.NavKompakt,
            "6.3")
        {
            Erledigt = h => h.KompaktAnsichtOffen,
        },
        new(
            "Meldungen und Hinweise prüfen",
            "Das Raster zeigt je Sonntag und Mannschaft, was gemeldet ist: Heim, auswärts, beides oder kann nicht. Darunter stehen Hinweise, etwa Hallen ohne bekannte Lage.",
            Fuehrungsziel.KompaktRaster,
            "6.7")
        {
            Vorbereiten = h => Ausfuehren(h.KompaktspieltageCommand),
        },
        new(
            "Meldungen korrigieren",
            "Mit dem Schalter „Bearbeiten“ steht ein Rahmen auf einem Feld; A, H, B und Entf setzen auswärts, nur Heim, beides oder kann nicht. Für Heimtermine lassen sich Beginn, Halle und zwei Tische einstellen.",
            Fuehrungsziel.KompaktBearbeiten,
            "6.9")
        {
            Vorbereiten = h => Ausfuehren(h.KompaktspieltageCommand),
        },
        new(
            "Pläne berechnen",
            "Der blaue Knopf ▶ berechnet die Pläne mit HiGHS; der Balken zeigt den Fortschritt. Warten Sie, bis die Varianten vorliegen.",
            Fuehrungsziel.Start,
            "6.5")
        {
            Erledigt = h => h.Kompaktvarianten.Count > 0 && !h.KompaktRechnet,
        },
        new(
            "Pläne vergleichen",
            "Jede Variante steht für ein Ziel (z. B. „Bestes Gesamtpaket“, „Kürzeste Fahrten“). Ein Klick zeigt sie im Raster und erläutert, wie sie im Vergleich abschneidet.",
            Fuehrungsziel.KompaktPlaene,
            "6.6")
        {
            Vorbereiten = h => Ausfuehren(h.KompaktspieltageCommand),
        },
        new(
            "Karte und Spieltage ansehen",
            "Die Karte zeigt die Fahrten; ein Klick auf einen Sonntag, eine Mannschaft oder einen Spieltag zeigt nur deren Fahrten. Die Spieltage nennen Spielfolge, Ausrichter und die in click-TT zu verlegenden Hallen.",
            Fuehrungsziel.KompaktKarte,
            "6.7")
        {
            Vorbereiten = h => Ausfuehren(h.KompaktspieltageCommand),
        },
        new(
            "Ausrichter im Diagramm",
            "Unter „Diagramme“ zeigt „Ausrichter und Spielorte“ je Mannschaft, wann sie ausrichtet und wo sie spielt.",
            Fuehrungsziel.NavDiagramme,
            "7.2"),
        new(
            "Plan merken",
            "„Plan merken…“ an der gewählten Variante oder oben rechts speichert den Plan; gemerkte Pläne stehen beim nächsten Öffnen wieder zur Wahl.",
            Fuehrungsziel.PlanMerken,
            "6.10")
        {
            Erledigt = h => h.KompaktGespeichertePlaene.Count > 0,
        },
        new(
            "Drucken und exportieren",
            "„Drucken“ erzeugt die Übersicht für die Vereine, „Für click-TT exportieren“ die CSV-Datei. Beim 4er-Spieltag die Spiele der zweiten Heimmannschaft in click-TT in die Halle des Ausrichters verlegen.",
            Fuehrungsziel.NavDrucken,
            "6.11"),
    ];

    /// <summary>Holt den Titel einer Führung, z. B. „Schritt für Schritt: Automodus“.</summary>
    /// <param name="art">Die Führung.</param>
    /// <returns>Der Titel.</returns>
    public static string Titel(Fuehrungsart art) => "Schritt für Schritt: " + art switch
    {
        Fuehrungsart.Kostenoptimierung => "Kostenoptimierung",
        Fuehrungsart.Automodus => "Automodus",
        _ => "Kompaktspieltage",
    };

    /// <summary>Holt die Schritte einer Führung.</summary>
    /// <param name="art">Die Führung.</param>
    /// <returns>Die Schritte in ihrer Reihenfolge.</returns>
    public static IReadOnlyList<Fuehrungsschritt> Schritte(Fuehrungsart art) => art switch
    {
        Fuehrungsart.Kostenoptimierung => Kosten,
        Fuehrungsart.Automodus => Auto,
        _ => Kompakt,
    };

    private static void Ausfuehren(CommunityToolkit.Mvvm.Input.IRelayCommand befehl)
    {
        if (befehl.CanExecute(null))
        {
            befehl.Execute(null);
        }
    }
}
