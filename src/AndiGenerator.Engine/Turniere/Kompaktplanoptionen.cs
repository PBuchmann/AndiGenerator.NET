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
    /// Holt die Standardeinstellungen auf allen Prozessorkernen bis auf einen: bis zu zwei Minuten vollständige Suche nach
    /// 4er-Plänen, danach zehn Sekunden zufällige Suche.
    /// </summary>
    public static Kompaktplanoptionen Standard =>
        new(TimeSpan.FromSeconds(10), Math.Max(1, Environment.ProcessorCount - 1), Random.Shared.Next(), null, TimeSpan.FromMinutes(2));
}
