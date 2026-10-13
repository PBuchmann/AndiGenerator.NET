// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Engine.Turniere;
using AndiGenerator.Persistence.Gemeinsam;

namespace AndiGenerator.Presentation;

/// <summary>
/// Gespeicherte Pläne im Kompaktmodus (nicht im Original; Peter, 13.10.2026): Der gesicherte und die gemerkten Pläne stehen
/// in der Ansicht „Kompaktspieltage“ neben den Varianten zur Wahl. Gewählt werden sie zum aktuellen Plan für Terminplan,
/// Druck und click-TT-Export – mit genau den gespeicherten Spielen (Uhrzeiten, Hallen, Heimrecht).
/// </summary>
public sealed partial class HauptfensterViewModel
{
    private IReadOnlyList<Kompaktvariante> kompaktGespeichert = [];
    private Dictionary<Kompaktvariante, (GemerkterPlanEintrag Eintrag, Kompaktauswertung Auswertung)> kompaktGespeichertJe = [];

    /// <summary>Holt die gespeicherten Pläne (gesichert, gemerkt) als Varianten.</summary>
    internal IReadOnlyList<Kompaktvariante> KompaktGespeichertePlaene => kompaktGespeichert;

    /// <summary>Kennzahlen eines Plans aus seiner Auswertung (für gespeicherte Pläne, die der Planer nicht kennt).</summary>
    private static Kompaktkennzahlen Kennzahlen(Kompaktauswertung a) =>
        new(
            a.KmGesamt,
            a.GroessterAbstand,
            a.MeisteAmTag,
            a.Verstoesse,
            a.OhneHeimspieltag,
            0,
            a.Spieltage.Count(s => s.Form == Turnierform.Vierer),
            a.Spieltage.Count(s => s.Form == Turnierform.Dreier),
            a.Spieltage.Count(s => s.Form == Turnierform.Einzel),
            Kompaktpunkte.Spanne(a))
        {
            EinzelKm = a.Spieltage.Where(s => s.Form == Turnierform.Einzel).Sum(s => s.Km),
        };

    /// <summary>Die Spieltage eines ausgewerteten Plans, der Ausrichter zuerst.</summary>
    private static List<Planturnier> Planturniere(Kompaktauswertung a) =>
        a.Spieltage
            .Select(s => new Planturnier(s.Tag, s.Form, s.Ausrichter is string ausrichter ? [ausrichter, .. s.Gaeste] : s.Mannschaften))
            .ToList();

    /// <summary>Die Hinweise der Kompaktauswertung zu einem Plan vor dem CSV-Export, höchstens 15 Zeilen.</summary>
    private static List<string> KompaktExportHinweise(Kompaktauswertung auswertung)
    {
        const int Hoechstens = 15;
        IReadOnlyList<string> befunde = auswertung.Befunde;
        List<string> liste = befunde.Take(Hoechstens).Select(b => "• " + b).ToList();
        if (befunde.Count > Hoechstens)
        {
            liste.Add(string.Create(CultureInfo.InvariantCulture, $"… und {befunde.Count - Hoechstens} weitere (siehe Hinweise in der Ansicht Kompaktspieltage)"));
        }

        return liste;
    }

    /// <summary>Liest den gesicherten und die gemerkten Pläne der Staffel und wertet sie aus (nach Öffnen, Sichern, Merken).</summary>
    private void KompaktGespeichertAufbauen()
    {
        kompaktGespeichert = [];
        kompaktGespeichertJe = [];
        if (sitzung is not Plansitzung s || !KompaktEingeschaltet)
        {
            return;
        }

        var eintraege = new List<(string Name, GemerkterPlanEintrag Eintrag)>();
        if (s.GesicherterPlan() is GemerkterPlanEintrag gesichert)
        {
            eintraege.Add(("Gesicherter Plan", gesichert));
        }

        eintraege.AddRange(s.GemerktePlaene().Select(e => ("Gemerkt: " + e.Name, e)));
        Datenbearbeitung daten = s.DatenBearbeiten();
        DateOnly heute = DateOnly.FromDateTime(DateTime.Today);
        var plaene = eintraege
            .Select(x => (x.Name, x.Eintrag, Stand: GemerktenStandLaden(s, x.Eintrag)))
            .Where(x => x.Stand is not null)
            .Select(x => (x.Name, x.Eintrag, Auswertung: Kompaktplanung.SpieleAuswerten(s.Staffel, daten, s.Optionen.Rundenplanung, heute, x.Stand!.Spiele)))
            .Where(x => x.Auswertung.Spieltage.Count > 0)
            .ToList();
        var liste = new List<Kompaktvariante>();
        foreach ((string name, GemerkterPlanEintrag eintrag, Kompaktauswertung auswertung) in plaene)
        {
            var variante = new Kompaktvariante(name, Planturniere(auswertung), Kennzahlen(auswertung));
            liste.Add(variante);
            kompaktGespeichertJe[variante] = (eintrag, auswertung);
        }

        kompaktGespeichert = liste;
        GespeichertePunkte(Kompaktpunkte.Bezug(kompaktVarianten.Count > 0 ? kompaktVarianten : kompaktGespeichert));
    }

    /// <summary>Die Punkte der gespeicherten Pläne, mit demselben Bezug wie die Varianten.</summary>
    private void GespeichertePunkte(Kompaktbezug? bezug)
    {
        if (bezug is null)
        {
            return;
        }

        (IReadOnlyList<string> spitze, IReadOnlyList<string> keller) = KompaktGruppen();
        IReadOnlyList<IReadOnlyList<string>> gruppen = spitze.Count >= 3 && spitze.Count == keller.Count ? [spitze, keller] : [];
        foreach ((Kompaktvariante v, (_, Kompaktauswertung a)) in kompaktGespeichertJe)
        {
            kompaktPunkte[v] = Kompaktpunkte.Berechnen(a, bezug, gruppen).Summe;
        }
    }

    /// <summary>Der gespeicherte Stand einer gewählten Variante, wenn sie ein gespeicherter Plan ist.</summary>
    private Planstand? GespeicherterStand(Plansitzung s, Kompaktvariante? variante) =>
        variante is not null && kompaktGespeichertJe.TryGetValue(variante, out var gespeichert) ? GemerktenStandLaden(s, gespeichert.Eintrag) : null;
}
