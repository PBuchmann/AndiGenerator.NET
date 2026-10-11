// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using Dock.Model.Mvvm.Controls;

namespace AndiGenerator.Presentation;

/// <summary>
/// Blick in den Maschinenraum der Optimierung: was die Rechenkerne gerade schaffen, wie die Inseln suchen (beste Kosten,
/// Weg bis zum Neustart, Suchstrategien; im Automodus statt der Kosten die Verstöße A, B und C), was die Spezial-Insel
/// tut und – im Automodus – welche Gewichte er gerade gegenüber den eigenen angehoben hat. Hängt nicht vom Plan ab,
/// sondern nur von der laufenden Generierung.
/// </summary>
public sealed class MaschinenraumAnsichtViewModel : Document
{
    /// <summary>Breite eines Kostenbalkens bei 100 % (Pixel).</summary>
    private const double Balkenbreite = 90;

    /// <summary>Höchste Gewichtsstufe der Stufenleiste („extrem hoch“; „nicht berücksichtigen“ = keine Stufe).</summary>
    private const int Stufen = (int)Gewichtung.ExtremHoch;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private readonly HauptfensterViewModel hauptfenster;
    private readonly ObservableCollection<Inselanzeige> inseln = [];
    private string ueberschrift = "Keine Generierung aktiv";
    private bool rechnet;
    private bool automodus;
    private IReadOnlyList<Gewichtsgruppe> gewichte = [];
    private string gewichteSchluessel = string.Empty;

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="hauptfenster">Hauptfenster mit der Anzeige der Rechenkerne.</param>
    public MaschinenraumAnsichtViewModel(HauptfensterViewModel hauptfenster)
    {
        ArgumentNullException.ThrowIfNull(hauptfenster);
        this.hauptfenster = hauptfenster;
        Id = "Maschinenraum";
        Title = "Leitstand";
        CanClose = true;
        CanFloat = true;
        Anzeige = hauptfenster.Anzeige;
    }

    /// <summary>Holt die Erklärung des Inselmodells.</summary>
    public static string Erklaerung =>
        "Jeder Rechenkern rechnet abwechselnd an mehreren Suchplätzen. Die Suchplätze gehören zu Inseln: Jede Insel sucht für sich "
        + "und hat ihren eigenen besten Plan; die Balken zeigen, welche Kriterien in ihm die meisten Kosten verursachen. Im Schaubild "
        + "steht je Insel grob, was sie gerade tut; ein grüner Punkt wandert nur, wenn der beste Plan besser wird. Findet die "
        + "schlechteste Insel lange nichts Besseres, beginnt sie neu – mit einem leeren Plan oder mit dem Plan der Spezial-Insel, "
        + "die eine Kostenart extrem gewichtet.";

    /// <summary>Holt die Anzeige mit den Rechenkernen.</summary>
    public Generierungsanzeige Anzeige { get; }

    /// <summary>Holt das Schaubild der Maschine mit seinen Ereignissen.</summary>
    public Schaubild Schaubild { get; } = new();

    /// <summary>Holt die Inseln (zuletzt die Spezial-Insel).</summary>
    public IReadOnlyList<Inselanzeige> Inseln => inseln;

    /// <summary>Holt die Überschrift: Kerne, Inseln und Tempo.</summary>
    public string Ueberschrift
    {
        get => ueberschrift;
        private set => SetProperty(ref ueberschrift, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob gerade gerechnet wird (nicht pausiert).</summary>
    public bool Rechnet
    {
        get => rechnet;
        private set => SetProperty(ref rechnet, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob der Automodus lenkt (dann gibt es den Abschnitt der Gewichte).</summary>
    public bool Automodus
    {
        get => automodus;
        private set => SetProperty(ref automodus, value);
    }

    /// <summary>Holt die Gewichte, die der Automodus gerade angehoben hat, je Kriterium in der Reihenfolge der Einteilung.</summary>
    public IReadOnlyList<Gewichtsgruppe> Gewichte
    {
        get => gewichte;
        private set
        {
            if (SetProperty(ref gewichte, value))
            {
                OnPropertyChanged(nameof(KeineGewichte));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob der Automodus noch kein Gewicht geändert hat.</summary>
    public bool KeineGewichte => Gewichte.Count == 0;

    /// <summary>Übernimmt den Stand der Generierung (im Takt der Statusabfrage).</summary>
    /// <param name="stand">Der Stand.</param>
    public void Uebernehmen(Optimierungsstand stand)
    {
        ArgumentNullException.ThrowIfNull(stand);
        Rechnet = stand.Laeuft && !stand.Pausiert;
        if (stand.Maschinenraum is not Maschinenraum raum)
        {
            Ueberschrift = "Keine Generierung aktiv";
            inseln.Clear();
            Automodus = false;
            GewichteZeigen([]);
            Schaubild.Uebernehmen(stand, inseln);
            return;
        }

        int normale = raum.Inseln.Count(i => !i.Spezial);
        string zustand = stand.Pausiert ? " · angehalten" : string.Empty;
        Ueberschrift = string.Create(
            Deutsch,
            $"{raum.Kerne.Count} Rechenkerne · {normale} Inseln{(normale < raum.Inseln.Count ? " und Spezial-Insel" : string.Empty)} · {stand.PlaeneProSekunde:N0} Pläne/s{zustand}");
        InselnUebernehmen(raum);
        Automodus = stand.Lenkung is not null;
        GewichteZeigen(stand.Lenkung?.Gewichte ?? []);
        Schaubild.Uebernehmen(stand, inseln);
    }

    /// <summary>
    /// Text unter dem Balken einer Insel: wie viele Pläne sie schon nichts Besseres gefunden hat und was das bedeutet. Neu
    /// beginnt nur die schlechteste Insel, und erst über der Grenze; die anderen rechnen auch darüber hinaus weiter.
    /// </summary>
    /// <param name="insel">Die Insel.</param>
    /// <param name="grenze">Pläne ohne Verbesserung, ab denen die schlechteste Insel neu beginnt.</param>
    /// <returns>Der Text.</returns>
    private static string Fortschrittstext(Inselstand insel, long grenze)
    {
        string plaene = string.Create(Deutsch, $"Seit {Kostenanzeige.Kurz(insel.OhneVerbesserung)} Plänen nichts Besseres");
        if (insel.OhneVerbesserung <= grenze)
        {
            return string.Create(Deutsch, $"{plaene} – Neustart frühestens nach {Kostenanzeige.Kurz(grenze)}");
        }

        return insel.Schlechteste
            ? plaene + " – beginnt gleich neu (schlechteste Insel)"
            : plaene + " – rechnet weiter, neu beginnt nur die schlechteste Insel";
    }

    /// <summary>Skala eines Gewichts: Ring bei der Gewichtung des Anwenders, Punkt bei der aktuellen, Linie dazwischen.</summary>
    /// <param name="vorher">Gewichtung des Anwenders.</param>
    /// <param name="jetzt">Gewichtung des Automodus.</param>
    /// <returns>Die Stufen „sehr wenig“ bis „extrem hoch“.</returns>
    private static Gewichtssegment[] Segmente(Gewichtung vorher, Gewichtung jetzt)
    {
        int von = Math.Min((int)vorher, (int)jetzt);
        int bis = Math.Max((int)vorher, (int)jetzt);
        return Enumerable.Range(1, Stufen)
            .Select(stufe => new Gewichtssegment(
                stufe == (int)vorher && vorher != jetzt,
                stufe == (int)jetzt,
                stufe > von && stufe <= bis,
                stufe >= von && stufe < bis))
            .ToArray();
    }

    /// <summary>Stufe mit Nummer wie in der Qualitätsansicht: A2 … (A1 sind die harten Fehler), B1 …, C1 ….</summary>
    /// <param name="einteilung">Die vollständige Einteilung.</param>
    /// <param name="k">Das Kriterium.</param>
    /// <returns>Z. B. <c>A2</c>.</returns>
    private static string StufeMitNummer(Stufeneinteilung einteilung, Kostenkriterium k)
    {
        Stufe stufe = einteilung.StufeVon(k);
        int nummer = einteilung.Position(k) + (stufe == Stufe.A ? 2 : 1);
        return stufe.ToString() + nummer.ToString(Deutsch);
    }

    private static string Spezialhinweis(Maschinenraum raum, Inselstand insel)
    {
        if (!insel.Laeuft || raum.SpezialKostenart is not { } art)
        {
            return "Wartet noch: Sie beginnt, wenn die Inseln eine Weile gerechnet haben.";
        }

        string verwendet = raum.SpezialVerwendet == 1 ? "einmal" : raum.SpezialVerwendet.ToString(Deutsch) + "-mal";
        return $"Gewichtet „{Gewichtungsanzeige.Name(art)}“ extrem hoch. Ihr Plan hat bisher {verwendet} eine Insel neu gestartet.";
    }

    /// <summary>Baut die Gewichte nur neu auf, wenn sie sich geändert haben (sonst flackerte die Ansicht im Takt).</summary>
    /// <param name="anhebungen">Die geänderten Gewichte.</param>
    private void GewichteZeigen(IReadOnlyList<Gewichtsanhebung> anhebungen)
    {
        string schluessel = string.Join(';', anhebungen.Select(a => $"{a.Kriterium},{a.Mannschaft},{a.Vorher},{a.Jetzt}"));
        if (schluessel == gewichteSchluessel)
        {
            return;
        }

        gewichteSchluessel = schluessel;
        Stufeneinteilung einteilung = hauptfenster.Einteilung.Vervollstaendigt();
        Kostenkriterium[] reihenfolge = [.. einteilung.A, .. einteilung.B, .. einteilung.C];
        Gewichte = anhebungen
            .GroupBy(a => a.Kriterium)
            .OrderBy(g => Array.IndexOf(reihenfolge, g.Key))
            .Select(g => new Gewichtsgruppe(
                StufeMitNummer(einteilung, g.Key),
                Planqualitaet.Name(g.Key),
                g.OrderBy(a => a.Mannschaft is not null)
                    .ThenBy(a => a.Mannschaft, StringComparer.CurrentCulture)
                    .Select(a => new Gewichtszeile(
                        a.Mannschaft ?? "alle Mannschaften",
                        Segmente(a.Vorher, a.Jetzt),
                        $"{Gewichtungsanzeige.Name(a.Vorher)} → {Gewichtungsanzeige.Name(a.Jetzt)}"))
                    .ToArray()))
            .ToArray();
    }

    private void InselnUebernehmen(Maschinenraum raum)
    {
        if (inseln.Count != raum.Inseln.Count)
        {
            inseln.Clear();
            foreach (Inselstand insel in raum.Inseln)
            {
                inseln.Add(new Inselanzeige(insel.Spezial ? "Spezial-Insel" : "Insel " + insel.Nummer.ToString(Deutsch), insel.Spezial));
            }
        }

        double beste = raum.Inseln.Where(i => !i.Spezial && i.Kosten >= 0).Select(i => i.Kosten).DefaultIfEmpty(-1).Min();
        Stufenwert? besterWert = null;
        foreach (Stufenwert wert in raum.Inseln.Select(i => i.Verstoesse).OfType<Stufenwert>())
        {
            if (besterWert is not { } bisher || wert.IstBesserAls(bisher))
            {
                besterWert = wert;
            }
        }

        for (int i = 0; i < raum.Inseln.Count; i++)
        {
            Inselstand insel = raum.Inseln[i];
            Inselanzeige anzeige = inseln[i];
            anzeige.Laeuft = insel.Laeuft;
            anzeige.Kosten = insel.Kosten < 0 ? "–" : Kostenanzeige.Kurz(insel.Kosten);
            anzeige.IstBeste = insel.Verstoesse is { } eigener
                ? besterWert is { } b && !b.IstBesserAls(eigener)
                : besterWert is null && !insel.Spezial && insel.Kosten >= 0 && insel.Kosten <= beste;
            anzeige.ZeigtVerstoesse = insel.Verstoesse is not null;
            if (insel.Verstoesse is { } verstoesse)
            {
                anzeige.VerstossA = verstoesse.A.ToString(Deutsch);
                anzeige.VerstossB = verstoesse.B.ToString(Deutsch);
                anzeige.VerstossC = verstoesse.CAnzahl.ToString(Deutsch);
            }

            anzeige.Strategien = "Suchstrategien der Suchplätze: " + string.Join(" · ", insel.Strategien);
            anzeige.Kostentreiber = (insel.Kostentreiber ?? [])
                .Select(k => new Kostentreiberzeile(
                    Planqualitaet.Name(k.Kriterium),
                    Balkenbreite * k.Anteil,
                    k.Verstoesse is int verstoesse ? verstoesse.ToString(Deutsch) : k.Anteil.ToString("P0", Deutsch)))
                .ToArray();
            anzeige.Neustarts = insel.Neustarts == 1 ? "1 Neustart" : insel.Neustarts.ToString(Deutsch) + " Neustarts";
            if (insel.Spezial)
            {
                anzeige.Hinweis = Spezialhinweis(raum, insel);
                continue;
            }

            long grenze = Math.Max(1, raum.DurchlaeufeVorNeustart);
            anzeige.Fortschritt = Math.Clamp(100.0 * insel.OhneVerbesserung / grenze, 0, 100);
            anzeige.FortschrittText = Fortschrittstext(insel, grenze);
        }
    }
}
