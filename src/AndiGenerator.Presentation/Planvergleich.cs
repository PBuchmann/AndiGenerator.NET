// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Presentation;

/// <summary>
/// Vergleich des aktuellen Plans mit dem gesicherten, bevor der aktuelle durch einen Neustart oder das Schließen verloren
/// ginge: Verstöße je Stufe (A mit den harten Fehlern), die Kosten und die Kriterien, bei denen sich die beiden
/// unterscheiden. Die Güte ordnet wie der Automodus: erst A, dann B, dann C, zuletzt die Kosten. Im Kompaktmodus
/// vergleicht er stattdessen die Kennzahlen der Kompaktspieltage (<see cref="Kompakt"/>).
/// </summary>
public sealed class Planvergleich
{
    /// <summary>So viele Kriterien mit Unterschieden zeigt der Vergleich höchstens.</summary>
    private const int UnterschiedeHoechstens = 8;

    /// <summary>Erklärung der Bewertung im Kompaktmodus.</summary>
    private const string Kompakthinweis =
        "Bewertet wie das beste Gesamtpaket: je ein Punkt für 10 % mehr Kilometer, einen Spieltag Rückstand im Stand, "
        + "keinen Sonntag mit zwei 4er-Spieltagen und ungleich weite Fahrten; Verstöße gegen die Meldungen je 10 Punkte, "
        + "Mannschaften ohne 3er- oder 4er-Heimspieltag je 15, ein fehlender Spitzen- oder Abstiegsspieltag je 3, 3er-Spieltage je 3, "
        + "Einzelspiele bei ungerader Mannschaftszahl je 2.";

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
        Empfehlung = EmpfehlungFuer(vergleich);
        Frage = FrageFuer(anlass, gesichertAm);
        Bewertungshinweis = "Bewertet wie im Automodus: erst die Verstöße der Stufe A, dann B, dann C, zuletzt die Kosten.";
    }

    private Planvergleich(string frage, IReadOnlyList<Vergleichszeile> zeilen, int vergleich, string bewertungshinweis)
    {
        Frage = frage;
        Zeilen = zeilen;
        Unterschiede = [];
        AktuellBesser = vergleich < 0;
        GesichertBesser = vergleich > 0;
        Empfehlung = EmpfehlungFuer(vergleich);
        Bewertungshinweis = bewertungshinweis;
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

    /// <summary>Holt die Erklärung, wonach bewertet wird.</summary>
    public string Bewertungshinweis { get; }

    /// <summary>
    /// Vergleich im Kompaktmodus: die Kennzahlen der Kompaktspieltage (Meldungen, Heimspieltage, Fahrten, Stand,
    /// gemeinsamer Spieltag, Spitzen- und Abstiegsspieltag) und die Punkte wie beim besten Gesamtpaket.
    /// </summary>
    /// <param name="anlass">Was geschieht, z. B. „Die Staffel wird geschlossen.“.</param>
    /// <param name="aktuell">Auswertung des aktuellen Plans.</param>
    /// <param name="gesichert">Auswertung des gesicherten Plans.</param>
    /// <param name="gruppen">Die vier stärksten und die vier schwächsten Mannschaften (leer, wenn nicht bekannt).</param>
    /// <param name="bezug">
    /// Bezug der Punkte wie in der Erläuterung: Kilometer des kürzesten Plans nur aus 4er-Spieltagen und kleinster
    /// Unterschied zwischen den Mannschaften unter den berechneten Varianten; <c>null</c>, wenn es keine gibt (dann der
    /// bessere der beiden Pläne).
    /// </param>
    /// <param name="gesichertAm">Wann der gesicherte Plan gesichert wurde.</param>
    /// <returns>Der Vergleich.</returns>
    public static Planvergleich Kompakt(string anlass, Kompaktauswertung aktuell, Kompaktauswertung gesichert, IReadOnlyList<IReadOnlyList<string>> gruppen, Kompaktbezug? bezug, DateTime gesichertAm)
    {
        ArgumentNullException.ThrowIfNull(aktuell);
        ArgumentNullException.ThrowIfNull(gesichert);
        ArgumentNullException.ThrowIfNull(gruppen);
        List<IReadOnlyList<string>> bekannt = gruppen.Where(g => g.Count >= 3).ToList();
        static double Bewertungskm(Kompaktauswertung a) => a.KmGesamt + a.Spieltage.Where(s => s.Form == Turnierform.Einzel).Sum(s => s.Km);
        Kompaktbezug b = bezug ?? new Kompaktbezug(
            Math.Min(Bewertungskm(aktuell), Bewertungskm(gesichert)),
            Math.Min(Kompaktpunkte.Spanne(aktuell), Kompaktpunkte.Spanne(gesichert)),
            true);
        double punkteNeu = Kompaktpunkte.Berechnen(aktuell, b, bekannt).Summe;
        double punkteAlt = Kompaktpunkte.Berechnen(gesichert, b, bekannt).Summe;
        List<Vergleichszeile> zeilen =
        [
            Zahlzeile("Verstöße gegen die Meldungen", aktuell.Verstoesse, gesichert.Verstoesse),
            Zahlzeile("Mannschaften ohne Heimspieltag", aktuell.OhneHeimspieltag, gesichert.OhneHeimspieltag),
            new Vergleichszeile("Spieltage", aktuell.Formen, gesichert.Formen, false, false),
            Kmzeile("Fahrten gesamt (Luftlinie)", aktuell.KmGesamt, gesichert.KmGesamt),
            Kmzeile("Unterschied weiteste – kürzeste Mannschaft", Kompaktpunkte.Spanne(aktuell), Kompaktpunkte.Spanne(gesichert)),
            Zahlzeile("Stand: größter Abstand (Spiele)", aktuell.GroessterAbstand, gesichert.GroessterAbstand),
            new Vergleichszeile(
                "Höchstens gemeinsam an einem Sonntag",
                aktuell.MeisteAmTag.ToString(Deutsch),
                gesichert.MeisteAmTag.ToString(Deutsch),
                aktuell.MeisteAmTag > gesichert.MeisteAmTag,
                aktuell.MeisteAmTag < gesichert.MeisteAmTag),
        ];
        if (bekannt.Count > 0)
        {
            int neu = Kompaktpunkte.Gruppentage(aktuell, bekannt);
            int alt = Kompaktpunkte.Gruppentage(gesichert, bekannt);
            zeilen.Add(new Vergleichszeile(
                "Spitzen- und Abstiegsspieltag",
                string.Create(Deutsch, $"{neu} von {bekannt.Count}"),
                string.Create(Deutsch, $"{alt} von {bekannt.Count}"),
                neu > alt,
                neu < alt));
        }

        int vergleich = Math.Abs(punkteNeu - punkteAlt) < 0.05 ? 0 : punkteNeu.CompareTo(punkteAlt);
        zeilen.Add(new Vergleichszeile("Punkte (kleiner ist besser)", punkteNeu.ToString("N1", Deutsch), punkteAlt.ToString("N1", Deutsch), vergleich < 0, vergleich > 0));
        return new Planvergleich(FrageFuer(anlass, gesichertAm), zeilen, vergleich, Kompakthinweis);
    }

    private static string Schluessel(Qualitaetskriterium k) => k.Stufe + "|" + k.Name;

    private static string EmpfehlungFuer(int vergleich) => vergleich switch
    {
        < 0 => "Der aktuelle Plan ist besser.",
        > 0 => "Der gesicherte Plan ist besser.",
        _ => "Beide Pläne sind gleich gut.",
    };

    private static string FrageFuer(string anlass, DateTime gesichertAm) => string.Create(
        Deutsch,
        $"{anlass} Dabei ginge der aktuelle Plan verloren. Es gibt schon einen gesicherten Plan (gesichert am {gesichertAm:dd.MM.yyyy} um {gesichertAm:HH:mm} Uhr). Soll er durch den aktuellen Plan ersetzt werden?");

    /// <summary>Verstöße je Stufe (nur Kriterien mit Anzahl; die harten Fehler zählen zu A).</summary>
    private static (int A, int B, int C) Summen(IReadOnlyList<Qualitaetskriterium> kriterien)
    {
        int Summe(char stufe) => kriterien.Where(k => k.Stufe.StartsWith(stufe)).Sum(k => k.Anzahl ?? 0);
        return (Summe('A'), Summe('B'), Summe('C'));
    }

    private static Vergleichszeile Zahlzeile(string bezeichnung, int aktuell, int gesichert) =>
        new(bezeichnung, aktuell.ToString(Deutsch), gesichert.ToString(Deutsch), aktuell < gesichert, aktuell > gesichert);

    private static Vergleichszeile Kmzeile(string bezeichnung, double aktuell, double gesichert) =>
        new(bezeichnung, aktuell.ToString("N0", Deutsch) + " km", gesichert.ToString("N0", Deutsch) + " km", aktuell < gesichert - 0.5, aktuell > gesichert + 0.5);
}
