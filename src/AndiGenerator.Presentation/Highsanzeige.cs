// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Schranke;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Presentation;

/// <summary>
/// Rechte Hälfte der Kachel „Berechnete Pläne“ im Automodus: woran HiGHS gerade rechnet – die Schritte der Untergrenze (A1,
/// Stufe A, Stufe B, A+B, Stufe C), danach dauerhaft die Teilneuplanung –, wie lange schon und was danach kommt.
/// </summary>
/// <param name="Titel">Die Überschrift, z. B. <c>HiGHS · Untergrenze</c>.</param>
/// <param name="Schritt">Woran HiGHS rechnet, z. B. <c>Summe B</c>, bzw. der Zustand der Teilneuplanung.</param>
/// <param name="Zeit">Wie lange schon und höchstens noch bzw. der Erfolg der Teilneuplanung.</param>
/// <param name="Anteil">Anteil der Zeitgrenze des Schritts (0 bis 1); <c>null</c> ohne Zeitgrenze.</param>
/// <param name="Danach">Was als Nächstes kommt (die ganze Reihe zeigen die Punkte).</param>
/// <param name="Rechnet">Gibt an, ob HiGHS gerade rechnet.</param>
/// <param name="Schritte">Die Schritte als Punktreihe (leer, wenn HiGHS fehlt).</param>
public sealed record Highsanzeige(string Titel, string Schritt, string Zeit, double? Anteil, string Danach, bool Rechnet, IReadOnlyList<Highsschritt> Schritte)
{
    /// <summary>Die Schritte in ihrer Reihenfolge.</summary>
    internal static readonly string[] Namen = ["A1", "Stufe A", "Stufe B", "A+B", "Stufe C", "Teilneuplanung"];

    private const int Teilneuplanung = 5;

    /// <summary>Holt die Zahl der B-Varianten, die zur Wahl stehen (0: keine, oder A und B sind festgelegt).</summary>
    public int Varianten { get; init; }

    /// <summary>Holt einen Wert, der angibt, ob die Kachel den Knopf zu den B-Varianten zeigt.</summary>
    public bool ZeigtVarianten => Varianten > 0;

    /// <summary>Holt den Text des Knopfs, z. B. <c>B-Varianten wählen (4) …</c>.</summary>
    public string VariantenText => string.Create(CultureInfo.InvariantCulture, $"B-Varianten wählen ({Varianten}) …");

    /// <summary>Holt einen Wert, der angibt, ob ein Balken die Zeit des Schritts zeigt.</summary>
    public bool ZeigtAnteil => Anteil is not null;

    /// <summary>Holt den Anteil in Prozent (für den Balken).</summary>
    public double Prozent => (Anteil ?? 0) * 100;

    /// <summary>Bildet die Anzeige.</summary>
    /// <param name="verfuegbar">Gibt an, ob HiGHS auf diesem Rechner verfügbar ist.</param>
    /// <param name="pausiert">Gibt an, ob die Generierung angehalten ist.</param>
    /// <param name="fortschritt">Der letzte Schritt der Untergrenze; <c>null</c>, wenn sie nicht rechnet.</param>
    /// <param name="seit">Seit wann HiGHS an diesem Schritt rechnet.</param>
    /// <param name="regel">Zeitgrenze eines Schritts: mindestens, solange es vorangeht, höchstens.</param>
    /// <param name="untergrenzeFertig">Gibt an, ob die Untergrenze fertig ist.</param>
    /// <param name="teilneuplanung">Stand der Teilneuplanung; <c>null</c>, wenn sie nicht läuft.</param>
    /// <returns>Die Anzeige.</returns>
    public static Highsanzeige Bilden(bool verfuegbar, bool pausiert, Schrankenfortschritt? fortschritt, TimeSpan seit, Laufzeitregel regel, bool untergrenzeFertig, Teilneuplanungsstand? teilneuplanung)
    {
        if (!verfuegbar)
        {
            return new Highsanzeige("HiGHS", "nicht verfügbar", "Der Löser fehlt auf diesem Rechner – die Inseln rechnen allein.", null, string.Empty, false, []);
        }

        int aktiv = (fortschritt, untergrenzeFertig) switch
        {
            ({ } f, _) => Index(f),
            (null, true) => Teilneuplanung,
            _ => 0,
        };
        IReadOnlyList<Highsschritt> schritte = [.. Namen.Select((n, i) => new Highsschritt(n, i < aktiv, i == aktiv))];
        if (aktiv == Teilneuplanung)
        {
            string erfolg = teilneuplanung is { Ausschnitte: > 0 } t
                ? string.Create(CultureInfo.InvariantCulture, $"{t.Besser} von {t.Ausschnitte} Ausschnitten besser")
                : "noch kein Ausschnitt geplant";
            return new Highsanzeige(
                "HiGHS · Teilneuplanung",
                teilneuplanung?.Zustand ?? "wartet",
                pausiert ? "angehalten · " + erfolg : erfolg,
                null,
                "läuft bis zum Ende",
                !pausiert && teilneuplanung is { Rechnet: true },
                schritte);
        }

        ArgumentNullException.ThrowIfNull(regel);
        int sekunden = (int)seit.TotalSeconds;
        int mindestens = (int)regel.Mindestens.TotalSeconds;
        int hoechstens = (int)regel.Hoechstens.TotalSeconds;
        string zeit = pausiert switch
        {
            true => "angehalten",
            _ when sekunden < mindestens => string.Create(CultureInfo.InvariantCulture, $"{sekunden} s · noch mindestens {mindestens - sekunden} s"),
            _ when sekunden <= hoechstens => string.Create(CultureInfo.InvariantCulture, $"{sekunden} s · weiter, solange es vorangeht (höchstens {hoechstens / 60} min)"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{sekunden} s · mehrere Rechnungen nacheinander"),
        };

        // Bis zur Mindestzeit füllt sich der Balken bis zur Hälfte, danach bis zur Höchstzeit die zweite Hälfte.
        double anteil = seit < regel.Mindestens
            ? seit / regel.Mindestens / 2
            : 0.5 + ((seit - regel.Mindestens) / (regel.Hoechstens - regel.Mindestens) / 2);
        return new Highsanzeige(
            "HiGHS · Untergrenze",
            fortschritt?.Text ?? "beginnt …",
            zeit,
            pausiert || fortschritt is null ? null : Math.Clamp(anteil, 0, 1),
            "dann " + Namen[aktiv + 1],
            !pausiert && fortschritt is not null,
            schritte);
    }

    /// <summary>Der Schritt der Untergrenze, an dem HiGHS rechnet (siehe <see cref="Namen"/>).</summary>
    private static int Index(Schrankenfortschritt f) => f.Text switch
    {
        _ when f.Text.StartsWith("A und B", StringComparison.Ordinal) => 3,
        _ when f.Text.StartsWith("Stufe C", StringComparison.Ordinal) => 4,
        _ => Math.Clamp(f.Schritt, 0, 2),
    };
}
