// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>
/// Die Namen der Elemente, auf die eine Führung zeigen kann. In der Oberfläche trägt das Element denselben Namen
/// (angehängte Eigenschaft <c>Fuehrungsmarke.Ziel</c>); ein Test prüft, dass es jedes Ziel gibt.
/// </summary>
public static class Fuehrungsziel
{
    /// <summary>Die Wahl des Verfahrens (Kostenoptimierung oder Automodus).</summary>
    public const string Verfahren = "Verfahren";

    /// <summary>Die Wahl des Ausgangsplans („Start mit“).</summary>
    public const string StartMit = "StartMit";

    /// <summary>Der Hauptknopf (Start, Pause, Fortsetzen).</summary>
    public const string Start = "Start";

    /// <summary>Der Knopf „Stopp“.</summary>
    public const string Stopp = "Stopp";

    /// <summary>Der Knopf „Plan merken“.</summary>
    public const string PlanMerken = "PlanMerken";

    /// <summary>Die Kacheln mit dem Stand der Generierung.</summary>
    public const string Kacheln = "Kacheln";

    /// <summary>Die Ergebniskachel (Kosten bzw. Verstöße und Phase des Automodus).</summary>
    public const string KachelErgebnis = "KachelErgebnis";

    /// <summary>Die Kachel „Verbesserungen“ mit dem Verlauf.</summary>
    public const string KachelVerlauf = "KachelVerlauf";

    /// <summary>Die Kachel „Berechnete Pläne“ (öffnet den Leitstand).</summary>
    public const string KachelLeitstand = "KachelLeitstand";

    /// <summary>Der Knopf „B-Varianten wählen“ in der Kachel.</summary>
    public const string BVarianten = "BVarianten";

    /// <summary>Links: Einstellungen (Gewichtungen).</summary>
    public const string NavEinstellungen = "NavEinstellungen";

    /// <summary>Links: Spielplandaten.</summary>
    public const string NavDaten = "NavDaten";

    /// <summary>Links: Ansicht Kompaktspieltage.</summary>
    public const string NavKompakt = "NavKompakt";

    /// <summary>Links: Ansicht Kosten.</summary>
    public const string NavKosten = "NavKosten";

    /// <summary>Links: Ansicht Qualität.</summary>
    public const string NavQualitaet = "NavQualitaet";

    /// <summary>Links: Ansicht Meldungen.</summary>
    public const string NavMeldungen = "NavMeldungen";

    /// <summary>Links: Ansicht Diagramme.</summary>
    public const string NavDiagramme = "NavDiagramme";

    /// <summary>Links: Für click-TT exportieren.</summary>
    public const string NavExport = "NavExport";

    /// <summary>Links: Drucken.</summary>
    public const string NavDrucken = "NavDrucken";

    /// <summary>Die Kostenmatrix der Ansicht Kosten.</summary>
    public const string KostenMatrix = "KostenMatrix";

    /// <summary>Die Tabelle der Ansicht Qualität (Einteilung, Verstöße, Bestmöglich).</summary>
    public const string QualitaetTabelle = "QualitaetTabelle";

    /// <summary>Die Pläne der Ansicht Kompaktspieltage.</summary>
    public const string KompaktPlaene = "KompaktPlaene";

    /// <summary>Das Raster „Meldungen und Plan“.</summary>
    public const string KompaktRaster = "KompaktRaster";

    /// <summary>Der Schalter „Bearbeiten“ des Rasters.</summary>
    public const string KompaktBearbeiten = "KompaktBearbeiten";

    /// <summary>Die Karte der Fahrten.</summary>
    public const string KompaktKarte = "KompaktKarte";

    /// <summary>Die Karten der Spieltage.</summary>
    public const string KompaktSpieltage = "KompaktSpieltage";

    /// <summary>Holt alle Ziele (für den Test, dass es jedes in der Oberfläche gibt).</summary>
    public static IReadOnlyList<string> Alle { get; } =
    [
        Verfahren, StartMit, Start, Stopp, PlanMerken, Kacheln, KachelErgebnis, KachelVerlauf, KachelLeitstand, BVarianten,
        NavEinstellungen, NavDaten, NavKompakt, NavKosten, NavQualitaet, NavMeldungen, NavDiagramme, NavExport, NavDrucken,
        KostenMatrix, QualitaetTabelle, KompaktPlaene, KompaktRaster, KompaktBearbeiten, KompaktKarte, KompaktSpieltage,
    ];
}
