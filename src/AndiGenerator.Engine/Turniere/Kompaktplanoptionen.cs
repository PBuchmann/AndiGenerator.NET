// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Einstellungen eines Laufs des Kompaktplaners.</summary>
/// <param name="Dauer">Dauer der zufälligen Suche (auch mit 3er-Spieltagen).</param>
/// <param name="Threads">Anzahl paralleler Suchen.</param>
/// <param name="Startwert">Startwert der Zufallszahlen.</param>
/// <param name="Versuche">
/// Feste Zahl von Suchversuchen je Suche statt der Dauer (mit einer Suche reproduzierbar, für Tests); <c>null</c> = bis
/// die Dauer abgelaufen ist.
/// </param>
/// <param name="ExaktDauer">Höchstdauer der vollständigen Suche nach dem kürzesten Plan nur aus 4er-Spieltagen.</param>
public sealed record Kompaktplanoptionen(TimeSpan Dauer, int Threads, int Startwert, int? Versuche, TimeSpan ExaktDauer)
{
    /// <summary>
    /// Holt die Standardeinstellungen auf allen Prozessorkernen bis auf einen (die Oberfläche bleibt bedienbar): rund drei
    /// Minuten gezielte Konstruktion (bzw. vollständige Suche nach 4er-Plänen), länger nur, solange HiGHS noch vorankommt –
    /// höchstens zehn Minuten (Peter, 26.10.2026: knapp vor dem Beweis nicht abbrechen, auch auf Rechnern mit 2 oder 4
    /// Kernen gute Pläne) –, danach zehn Sekunden zufällige Suche.
    /// </summary>
    public static Kompaktplanoptionen Standard =>
        new(TimeSpan.FromSeconds(10), Math.Max(1, Environment.ProcessorCount - 1), Random.Shared.Next(), null, TimeSpan.FromMinutes(3))
        {
            ExaktHoechstens = TimeSpan.FromMinutes(10),
        };

    /// <summary>
    /// Holt die Höchstdauer der gezielten Konstruktion, wenn HiGHS über <see cref="ExaktDauer"/> hinaus noch vorankommt (ein
    /// besserer Plan oder eine deutlich kleinere Lücke zur Schranke binnen <see cref="Stillstand"/>); <c>null</c> = nicht
    /// verlängern.
    /// </summary>
    public TimeSpan? ExaktHoechstens { get; init; }

    /// <summary>Holt, wie lange HiGHS ohne Fortschritt über <see cref="ExaktDauer"/> hinaus weiterrechnet.</summary>
    public TimeSpan Stillstand { get; init; } = TimeSpan.FromSeconds(40);

    /// <summary>
    /// Holt den Löser, mit dem die Pläne zuerst gezielt nach den Kriterien konstruiert werden (ganzzahlige Optimierung,
    /// höchstens <see cref="ExaktDauer"/>); ohne ihn wie bisher vollständige 4er-Suche und Zufallssuche.
    /// </summary>
    public IGanzzahlloeser? Loeser { get; init; }
}
