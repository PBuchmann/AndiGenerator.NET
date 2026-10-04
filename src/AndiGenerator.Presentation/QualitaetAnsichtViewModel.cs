// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Qualitätsansicht: Verstöße je Kriterium in der Rangfolge aus MIGRATIONSPLAN E14. Im Automodus legt der Staffelleiter
/// hier die Einteilung in die Stufen A, B und C und die Reihenfolge darin fest – mit Knöpfen oder durch Ziehen und Ablegen
/// mit Vorschau (gespeichert mit den Spielplandaten); der Automodus lenkt danach. Kosten zeigt die Ansicht nur bei der
/// Kostenoptimierung.
/// </summary>
public sealed class QualitaetAnsichtViewModel : AnsichtViewModel
{
    private IReadOnlyList<Qualitaetszeile> zeilen = [];
    private string zusammenfassung = string.Empty;
    private bool pflichtErfuellt = true;
    private bool zeigtKosten = true;
    private bool bearbeitbar;
    private Planstand? letzter;
    private Qualitaetszeile? schwebend;
    private Kostenkriterium? gezogen;
    private Stufeneinteilung? ausgangslage;
    private Stufeneinteilung? vorschau;
    private string? gewaehlt;
    private (Planstand? Stand, string? Name) detailsFuer;
    private IReadOnlyList<Meldungsgruppe> details = [];
    private string detailTitel = string.Empty;

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="hauptfenster">Hauptfenster.</param>
    /// <param name="id">Eindeutige Kennung im Layout.</param>
    public QualitaetAnsichtViewModel(HauptfensterViewModel hauptfenster, string id)
        : base(hauptfenster, "Qualität", id)
    {
        DetailsSchliessenCommand = new RelayCommand(() => Waehlen(null));
    }

    /// <summary>Holt die Kriterien.</summary>
    public IReadOnlyList<Qualitaetszeile> Zeilen
    {
        get => zeilen;
        private set => SetProperty(ref zeilen, value);
    }

    /// <summary>Holt die Verstöße des gewählten Kriteriums je Mannschaft (rechts neben der Tabelle); leer = nichts anzeigen.</summary>
    public IReadOnlyList<Meldungsgruppe> Details
    {
        get => details;
        private set
        {
            if (SetProperty(ref details, value))
            {
                OnPropertyChanged(nameof(ZeigtDetails));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob rechts Verstöße angezeigt werden.</summary>
    public bool ZeigtDetails => Details.Count > 0;

    /// <summary>Holt die Überschrift der Verstöße, z. B. <c>B3 · Sperrtermine</c>.</summary>
    public string DetailTitel
    {
        get => detailTitel;
        private set => SetProperty(ref detailTitel, value);
    }

    /// <summary>Holt den Befehl, der die Verstöße rechts schließt (Auswahl aufheben).</summary>
    public IRelayCommand DetailsSchliessenCommand { get; }

    /// <summary>Holt die Zusammenfassung, z. B. <c>Pflichtstufe A erfüllt · 3 Kriterien mit Verstößen</c>.</summary>
    public string Zusammenfassung
    {
        get => zusammenfassung;
        private set => SetProperty(ref zusammenfassung, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob alle Kriterien der Pflichtstufe A erfüllt sind.</summary>
    public bool PflichtErfuellt
    {
        get => pflichtErfuellt;
        private set => SetProperty(ref pflichtErfuellt, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob die Kosten gezeigt werden (nur bei der Kostenoptimierung).</summary>
    public bool ZeigtKosten
    {
        get => zeigtKosten;
        private set => SetProperty(ref zeigtKosten, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob die Einteilung bearbeitet werden kann (nur im Automodus).</summary>
    public bool Bearbeitbar
    {
        get => bearbeitbar;
        private set => SetProperty(ref bearbeitbar, value);
    }

    /// <summary>
    /// Holt die gezogene Zeile, wie sie am Mauszeiger hängt (mit der Stufe, die sie beim Ablegen bekäme); <c>null</c>,
    /// solange nicht gezogen wird. In der Tabelle steht an ihrer Stelle eine Lücke.
    /// </summary>
    public Qualitaetszeile? Schwebend
    {
        get => schwebend;
        private set => SetProperty(ref schwebend, value);
    }

    /// <summary>
    /// Wählt eine Zeile: rechts daneben stehen ihre Verstöße je Mannschaft. Ein zweiter Klick auf dieselbe Zeile oder
    /// <c>null</c> hebt die Auswahl auf. Gibt es keine Verstöße, bleibt die rechte Tabelle zu.
    /// </summary>
    /// <param name="zeile">Die Zeile oder <c>null</c>.</param>
    public void Waehlen(Qualitaetszeile? zeile)
    {
        gewaehlt = zeile is null || zeile.Name == gewaehlt ? null : zeile.Name;
        AuswahlZeigen();
    }

    /// <summary>Beginnt das Ziehen einer Zeile (nur im Automodus, nicht bei den harten Fehlern).</summary>
    /// <param name="zeile">Die Zeile.</param>
    /// <returns><c>true</c>, wenn gezogen wird.</returns>
    public bool ZiehenBeginnen(Qualitaetszeile zeile)
    {
        ArgumentNullException.ThrowIfNull(zeile);
        if (!Bearbeitbar || zeile.Kriterium is not Kostenkriterium k)
        {
            return false;
        }

        gezogen = k;
        ausgangslage = Hauptfenster.Einteilung.Vervollstaendigt();
        vorschau = ausgangslage;
        Neu();
        return true;
    }

    /// <summary>
    /// Die gezogene Zeile ist über <paramref name="ziel"/>: Vorschau, wie die Tabelle nach dem Ablegen aussähe (Stufe und
    /// Nummer ergeben sich aus der Stelle). Über der oberen Hälfte einer Zeile kommt sie davor, sonst dahinter.
    /// </summary>
    /// <param name="ziel">Die Zeile unter dem Zeiger.</param>
    /// <param name="davor"><c>true</c> = obere Hälfte.</param>
    public void ZiehenUeber(Qualitaetszeile ziel, bool davor)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        if (gezogen is not Kostenkriterium k || ausgangslage is null || ziel.Kriterium is not Kostenkriterium nachbar || nachbar == k)
        {
            return;
        }

        Stufeneinteilung neu = ausgangslage.Platzieren(k, nachbar, davor);
        if (neu.Text() != vorschau?.Text())
        {
            vorschau = neu;
            Neu();
        }
    }

    /// <summary>Beendet das Ziehen: übernimmt die Vorschau (Ablegen) oder verwirft sie (Abbrechen).</summary>
    /// <param name="uebernehmen"><c>true</c> = ablegen.</param>
    public void ZiehenBeenden(bool uebernehmen)
    {
        Stufeneinteilung? neu = vorschau;
        Stufeneinteilung? alt = ausgangslage;
        gezogen = null;
        vorschau = null;
        ausgangslage = null;
        if (uebernehmen && neu is not null && alt is not null && neu.Text() != alt.Text())
        {
            _ = Hauptfenster.EinteilungAendernAsync(_ => neu);
        }

        Neu();
    }

    /// <summary>Das Verfahren hat gewechselt: Kosten und Bearbeitung zeigen oder ausblenden.</summary>
    internal void VerfahrenGeaendert() => Neu();

    /// <inheritdoc/>
    protected override void Anzeigen(Planstand? stand)
    {
        letzter = stand;
        ZeigtKosten = !Hauptfenster.Automodus;
        Bearbeitbar = Hauptfenster.Automodus;
        if (stand is null)
        {
            Zeilen = [];
            Zusammenfassung = string.Empty;
            PflichtErfuellt = true;
            AuswahlZeigen();
            return;
        }

        Stufeneinteilung einteilung = vorschau ?? Hauptfenster.Einteilung.Vervollstaendigt();
        IReadOnlyList<Qualitaetskriterium> kriterien = Planqualitaet.Kriterien(stand.Bewertung, einteilung);

        // Gleiche Zeilen (Stufe und Kriterium) werden nur aktualisiert, damit eine offene Auswahl nicht verschwindet.
        bool gleich = Zeilen.Count == kriterien.Count && Zeilen.Zip(kriterien).All(p => p.First.Stufe == p.Second.Stufe && p.First.Name == p.Second.Name);
        if (!gleich)
        {
            Zeilen = kriterien.Select((k, i) => NeueZeile(k, Nachbar(kriterien, i, -1), Nachbar(kriterien, i, +1))).ToList();
        }

        foreach ((Qualitaetszeile zeile, Qualitaetskriterium k) in Zeilen.Zip(kriterien))
        {
            Werte(zeile, k);
        }

        Schwebend = gezogen is null ? null : SchwebendeZeile(kriterien);
        AuswahlZeigen();

        int pflicht = kriterien.Count(k => k.IstPflicht && !k.Erfuellt);
        int uebrige = kriterien.Count(k => !k.IstPflicht && !k.Erfuellt);
        PflichtErfuellt = pflicht == 0;
        string a = pflicht == 0 ? "Pflichtstufe A erfüllt" : $"Pflichtstufe A: {pflicht} Kriterien verletzt";
        Zusammenfassung = ZeigtKosten
            ? $"{a} · {uebrige} weitere Kriterien mit Verstößen · Gesamtkosten {Kostenanzeige.Kurz(stand.Bewertung.Gesamtkosten)}"
            : $"{a} · {uebrige} weitere Kriterien mit Verstößen";
    }

    /// <summary>Das sichtbare Nachbarkriterium in derselben Stufe (−1 darüber, +1 darunter); <c>null</c> am Rand der Stufe.</summary>
    private static Kostenkriterium? Nachbar(IReadOnlyList<Qualitaetskriterium> kriterien, int index, int richtung)
    {
        int j = index + richtung;
        if (kriterien[index].Kriterium is null || j < 0 || j >= kriterien.Count || kriterien[j].Kriterium is not Kostenkriterium nachbar)
        {
            return null;
        }

        return kriterien[j].Stufe[0] == kriterien[index].Stufe[0] ? nachbar : null;
    }

    /// <summary>Verschiebt <paramref name="k"/> an <paramref name="nachbar"/> vorbei (dazwischen liegen ggf. ausgeblendete Kriterien).</summary>
    private static Stufeneinteilung Vorbei(Stufeneinteilung e, Kostenkriterium k, Kostenkriterium nachbar, int richtung)
    {
        Stufeneinteilung neu = e;
        for (int i = 0; i < Stufeneinteilung.AnzahlKriterien && Math.Sign(neu.Position(nachbar) - neu.Position(k)) == richtung; i++)
        {
            neu = neu.Verschieben(k, richtung);
        }

        return neu;
    }

    private static void Werte(Qualitaetszeile zeile, Qualitaetskriterium k)
    {
        zeile.Verstoesse = k.Anzahl switch
        {
            null => "–",
            int anzahl when k.Gesamtzahl is int von => $"{anzahl} von {von}",
            int anzahl => anzahl.ToString(CultureInfo.InvariantCulture),
        };
        zeile.Kosten = Kostenanzeige.Kurz(k.Kosten);
        zeile.Zustand = (k.Erfuellt, k.IstPflicht) switch
        {
            (true, _) => Qualitaetszustand.Erfuellt,
            (false, true) => Qualitaetszustand.PflichtVerletzt,
            _ => Qualitaetszustand.Verletzt,
        };
    }

    /// <summary>Kopie der gezogenen Zeile für den Mauszeiger (nicht bearbeitbar, mit ihren Werten).</summary>
    private Qualitaetszeile? SchwebendeZeile(IReadOnlyList<Qualitaetskriterium> kriterien)
    {
        if (kriterien.FirstOrDefault(k => k.Kriterium == gezogen) is not Qualitaetskriterium k)
        {
            return null;
        }

        var zeile = new Qualitaetszeile(k.Stufe, k.Name, k.Kriterium);
        Werte(zeile, k);
        return zeile;
    }

    /// <summary>Markiert die gewählte Zeile und bildet ihre Verstöße (nur neu, wenn sich Plan oder Auswahl geändert haben).</summary>
    private void AuswahlZeigen()
    {
        Qualitaetszeile? zeile = Zeilen.FirstOrDefault(z => z.Name == gewaehlt);
        foreach (Qualitaetszeile z in Zeilen)
        {
            z.IstGewaehlt = z == zeile;
        }

        DetailTitel = zeile is null ? string.Empty : $"{zeile.Stufe} · {zeile.Name}";
        if (detailsFuer != (letzter, zeile?.Name))
        {
            detailsFuer = (letzter, zeile?.Name);
            Details = zeile is null ? [] : Hauptfenster.Kriteriumsdetails(zeile.Kriterium is null, zeile.Kriterium, letzter);
        }
    }

    /// <summary>Baut die Zeilen neu auf (Bearbeitbarkeit oder Vorschau geändert).</summary>
    private void Neu()
    {
        Zeilen = [];
        Anzeigen(letzter);
    }

    private Qualitaetszeile NeueZeile(Qualitaetskriterium k, Kostenkriterium? oben, Kostenkriterium? unten)
    {
        if (k.Kriterium is not Kostenkriterium kriterium || !Bearbeitbar)
        {
            return new Qualitaetszeile(k.Stufe, k.Name, k.Kriterium);
        }

        // Hoch/runter innerhalb der Stufe; am Rand der Stufe in die Nachbarstufe: nach unten an deren erste Stelle,
        // nach oben ans Ende (Stufeneinteilung.Einstufen). Spieltage ohne Anzahl bleiben in C.
        Stufe klasse = (Stufe)(k.Stufe[0] - 'A');
        Action? hoch = oben switch
        {
            Kostenkriterium o => () => _ = Hauptfenster.EinteilungAendernAsync(e => Vorbei(e, kriterium, o, -1)),
            null when klasse > Stufe.A && Stufeneinteilung.HatAnzahl(kriterium) => () => _ = Hauptfenster.EinteilungAendernAsync(e => e.Einstufen(kriterium, klasse - 1)),
            _ => null,
        };
        Action? runter = unten switch
        {
            Kostenkriterium u => () => _ = Hauptfenster.EinteilungAendernAsync(e => Vorbei(e, kriterium, u, +1)),
            null when klasse < Stufe.C => () => _ = Hauptfenster.EinteilungAendernAsync(e => e.Einstufen(kriterium, klasse + 1)),
            _ => null,
        };
        var bearbeitung = new Zeilenbearbeitung((x, stufe) => _ = Hauptfenster.EinteilungAendernAsync(e => e.Einstufen(x, stufe)), hoch, runter);
        return new Qualitaetszeile(k.Stufe, k.Name, kriterium, bearbeitung, kriterium == gezogen);
    }
}
