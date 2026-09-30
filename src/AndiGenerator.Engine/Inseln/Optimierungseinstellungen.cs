// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Inseln;

/// <summary>Einstellungen des Inselmodells (MIGRATIONSPLAN E6).</summary>
/// <param name="Kerne">Anzahl der Rechen-Threads; Standard: Prozessorkerne − 1 (mindestens 1).</param>
/// <param name="Inseln">Anzahl der Inseln (Original <c>cMaxThreads</c> = 4).</param>
/// <param name="Strategien">Feste Suchplätze je Insel mit ihrer Tauschstrategie (Original <c>cThreadTauschPercent</c>).</param>
/// <param name="DynamischeJeInsel">Dynamische Suchplätze je Insel (Original 10).</param>
/// <param name="DynamischeStartstrategie">Anfangsstrategie der dynamischen Plätze (Original <c>2</c>).</param>
/// <param name="DurchlaeufeVorNeustart">Durchläufe der schlechtesten Insel ohne Verbesserung bis zum Neustart (Original 100 000).</param>
/// <param name="SpezialInsel">Zusätzliche Insel mit einer extrem gewichteten Kostenart (Original <c>SpecialThread</c>).</param>
/// <param name="DurchlaeufeVorSpezialInsel">Gesamtdurchläufe, ab denen die Spezial-Insel startet (Original 3 000 000).</param>
/// <param name="MindestDurchlaeufeSpezialInsel">Durchläufe der Spezial-Insel seit dem letzten Wechsel der Kostenart, bevor ihre Lösung eine Insel neu startet (Original 200 000; Befund #10 korrigiert).</param>
/// <param name="Startwert">Startwert der Zufallsfolgen; <c>null</c> = zufällig. Nur <see cref="Inseloptimierer.Rechnen"/> ist damit reproduzierbar.</param>
public sealed record Optimierungseinstellungen(
    int Kerne,
    int Inseln,
    IReadOnlyList<string> Strategien,
    int DynamischeJeInsel,
    string DynamischeStartstrategie,
    long DurchlaeufeVorNeustart,
    bool SpezialInsel,
    long DurchlaeufeVorSpezialInsel,
    long MindestDurchlaeufeSpezialInsel,
    int? Startwert)
{
    /// <summary>Standard: Topologie des Originals (4 Inseln × 14 feste + 10 dynamische Suchplätze, Spezial-Insel), alle Kerne bis auf einen.</summary>
    public static Optimierungseinstellungen Standard { get; } = new(
        Kerne: Math.Max(1, Environment.ProcessorCount - 1),
        Inseln: 4,
        Strategien: ["R", "100", "15", "5", "2", "1", "S1,100", "S1,25", "S1,10", "S2,10", "M1,10", "M1,25", "M1,100", "M2,10"],
        DynamischeJeInsel: 10,
        DynamischeStartstrategie: "2",
        DurchlaeufeVorNeustart: 100_000,
        SpezialInsel: true,
        DurchlaeufeVorSpezialInsel: 3_000_000,
        MindestDurchlaeufeSpezialInsel: 200_000,
        Startwert: null);
}
