// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Presentation;

/// <summary>
/// Qualitätsansicht: Verstöße je Kriterium in der Rangfolge aus MIGRATIONSPLAN E14 (A = Muss, B = Sperr- und
/// Ausweichtermine, C = übrige). Zeigt auf einen Blick, was an einem Plan noch stört – unabhängig von der Gewichtung.
/// </summary>
public sealed class QualitaetAnsichtViewModel : AnsichtViewModel
{
    private IReadOnlyList<Qualitaetszeile> zeilen = [];
    private string zusammenfassung = string.Empty;
    private bool pflichtErfuellt = true;

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="hauptfenster">Hauptfenster.</param>
    /// <param name="id">Eindeutige Kennung im Layout.</param>
    public QualitaetAnsichtViewModel(HauptfensterViewModel hauptfenster, string id)
        : base(hauptfenster, "Qualität", id)
    {
    }

    /// <summary>Holt die Kriterien.</summary>
    public IReadOnlyList<Qualitaetszeile> Zeilen
    {
        get => zeilen;
        private set => SetProperty(ref zeilen, value);
    }

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

    /// <inheritdoc/>
    protected override void Anzeigen(Planstand? stand)
    {
        if (stand is null)
        {
            Zeilen = [];
            Zusammenfassung = string.Empty;
            PflichtErfuellt = true;
            return;
        }

        IReadOnlyList<Qualitaetskriterium> kriterien = Planqualitaet.Kriterien(stand.Bewertung);
        Zeilen = kriterien.Select(Zeile).ToList();
        int pflicht = kriterien.Count(k => k.IstPflicht && !k.Erfuellt);
        int uebrige = kriterien.Count(k => !k.IstPflicht && !k.Erfuellt);
        PflichtErfuellt = pflicht == 0;
        string a = pflicht == 0 ? "Pflichtstufe A erfüllt" : $"Pflichtstufe A: {pflicht} Kriterien verletzt";
        Zusammenfassung = $"{a} · {uebrige} weitere Kriterien mit Verstößen · Gesamtkosten {Kostenanzeige.Kurz(stand.Bewertung.Gesamtkosten)}";
    }

    private static Qualitaetszeile Zeile(Qualitaetskriterium k)
    {
        string verstoesse = k.Anzahl switch
        {
            null => "–",
            int anzahl when k.Gesamtzahl is int von => $"{anzahl} von {von}",
            int anzahl => anzahl.ToString(CultureInfo.InvariantCulture),
        };
        Qualitaetszustand zustand = (k.Erfuellt, k.IstPflicht) switch
        {
            (true, _) => Qualitaetszustand.Erfuellt,
            (false, true) => Qualitaetszustand.PflichtVerletzt,
            _ => Qualitaetszustand.Verletzt,
        };
        return new Qualitaetszeile(k.Stufe, k.Name, verstoesse, Kostenanzeige.Kurz(k.Kosten), zustand);
    }
}
