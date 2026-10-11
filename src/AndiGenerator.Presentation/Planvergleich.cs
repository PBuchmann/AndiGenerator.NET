// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Presentation;

/// <summary>
/// Vergleich des aktuellen Plans mit dem gesicherten, bevor der aktuelle durch einen Neustart oder das Schließen verloren
/// ginge: Verstöße je Stufe (A mit den harten Fehlern), die Kosten und die Kriterien, bei denen sich die beiden
/// unterscheiden. Die Güte ordnet wie der Automodus: erst A, dann B, dann C, zuletzt die Kosten.
/// </summary>
public sealed class Planvergleich
{
    /// <summary>So viele Kriterien mit Unterschieden zeigt der Vergleich höchstens.</summary>
    private const int UnterschiedeHoechstens = 8;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Initialisiert den Vergleich.</summary>
    /// <param name="anlass">Was geschieht, z. B. „Die Staffel wird geschlossen.“.</param>
    /// <param name="aktuell">Bewertung des aktuellen Plans.</param>
    /// <param name="gesichert">Bewertung des gesicherten Plans.</param>
    /// <param name="einteilung">Einteilung der Kriterien in A, B und C.</param>
    /// <param name="gesichertAm">Wann der gesicherte Plan gesichert wurde.</param>
    public Planvergleich(string anlass, Planbewertung aktuell, Planbewertung gesichert, Stufeneinteilung einteilung, DateTime gesichertAm)
    {
        ArgumentNullException.ThrowIfNull(aktuell);
        ArgumentNullException.ThrowIfNull(gesichert);
        IReadOnlyList<Qualitaetskriterium> neu = Planqualitaet.Kriterien(aktuell, einteilung);
        IReadOnlyList<Qualitaetskriterium> alt = Planqualitaet.Kriterien(gesichert, einteilung);
        (int A, int B, int C) summeNeu = Summen(neu);
        (int A, int B, int C) summeAlt = Summen(alt);
        Zeilen =
        [
            Zahlzeile("Stufe A (Pflicht, mit harten Fehlern)", summeNeu.A, summeAlt.A),
            Zahlzeile("Stufe B", summeNeu.B, summeAlt.B),
            Zahlzeile("Stufe C", summeNeu.C, summeAlt.C),
            new Vergleichszeile(
                "Kosten",
                Kostenanzeige.Kurz(aktuell.Gesamtkosten),
                Kostenanzeige.Kurz(gesichert.Gesamtkosten),
                aktuell.Gesamtkosten < gesichert.Gesamtkosten,
                aktuell.Gesamtkosten > gesichert.Gesamtkosten),
        ];
        Dictionary<string, Qualitaetskriterium> vorige = alt.Where(k => k.Anzahl is not null).GroupBy(Schluessel).ToDictionary(g => g.Key, g => g.First());
        Unterschiede = neu
            .Where(k => k.Anzahl is int && vorige.TryGetValue(Schluessel(k), out Qualitaetskriterium? v) && v.Anzahl != k.Anzahl)
            .Take(UnterschiedeHoechstens)
            .Select(k => Zahlzeile($"{k.Stufe} · {k.Name}", k.Anzahl.GetValueOrDefault(), vorige[Schluessel(k)].Anzahl.GetValueOrDefault()))
            .ToArray();
        int vergleich = (summeNeu.A, summeNeu.B, summeNeu.C, aktuell.Gesamtkosten).CompareTo((summeAlt.A, summeAlt.B, summeAlt.C, gesichert.Gesamtkosten));
        AktuellBesser = vergleich < 0;
        GesichertBesser = vergleich > 0;
        Empfehlung = vergleich switch
        {
            < 0 => "Der aktuelle Plan ist besser.",
            > 0 => "Der gesicherte Plan ist besser.",
            _ => "Beide Pläne sind gleich gut.",
        };
        Frage = string.Create(
            Deutsch,
            $"{anlass} Dabei ginge der aktuelle Plan verloren. Es gibt schon einen gesicherten Plan (gesichert am {gesichertAm:dd.MM.yyyy} um {gesichertAm:HH:mm} Uhr). Soll er durch den aktuellen Plan ersetzt werden?");
    }

    /// <summary>Holt die Frage mit dem Anlass.</summary>
    public string Frage { get; }

    /// <summary>Holt die Verstöße je Stufe und die Kosten beider Pläne.</summary>
    public IReadOnlyList<Vergleichszeile> Zeilen { get; }

    /// <summary>Holt die Kriterien, bei denen sich die Zahl der Verstöße unterscheidet (wichtigste Stufe zuerst).</summary>
    public IReadOnlyList<Vergleichszeile> Unterschiede { get; }

    /// <summary>Holt einen Wert, der angibt, ob sich die beiden bei einzelnen Kriterien unterscheiden.</summary>
    public bool HatUnterschiede => Unterschiede.Count > 0;

    /// <summary>Holt einen Wert, der angibt, ob der aktuelle Plan insgesamt besser ist.</summary>
    public bool AktuellBesser { get; }

    /// <summary>Holt einen Wert, der angibt, ob der gesicherte Plan insgesamt besser ist.</summary>
    public bool GesichertBesser { get; }

    /// <summary>Holt das Urteil, z. B. „Der aktuelle Plan ist besser.“.</summary>
    public string Empfehlung { get; }

    private static string Schluessel(Qualitaetskriterium k) => k.Stufe + "|" + k.Name;

    /// <summary>Verstöße je Stufe (nur Kriterien mit Anzahl; die harten Fehler zählen zu A).</summary>
    private static (int A, int B, int C) Summen(IReadOnlyList<Qualitaetskriterium> kriterien)
    {
        int Summe(char stufe) => kriterien.Where(k => k.Stufe.StartsWith(stufe)).Sum(k => k.Anzahl ?? 0);
        return (Summe('A'), Summe('B'), Summe('C'));
    }

    private static Vergleichszeile Zahlzeile(string bezeichnung, int aktuell, int gesichert) =>
        new(bezeichnung, aktuell.ToString(Deutsch), gesichert.ToString(Deutsch), aktuell < gesichert, aktuell > gesichert);
}
