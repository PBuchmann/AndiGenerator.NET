// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Inseln;

/// <summary>Stand einer Insel: eine eigenständige Suche mit mehreren Suchplätzen und eigenem besten Plan.</summary>
/// <param name="Nummer">Nummer der Insel (0 = Spezial-Insel).</param>
/// <param name="Spezial">Spezial-Insel: rechnet mit einer extrem gewichteten Kostenart und gibt gute Pläne an die Inseln ab.</param>
/// <param name="Laeuft">Die Insel rechnet (die Spezial-Insel erst nach einer Anlaufzeit).</param>
/// <param name="Kosten">Kosten ihres besten Plans (Spezial-Insel: mit ihren eigenen Gewichten); negativ = noch keiner.</param>
/// <param name="Durchlaeufe">Berechnete Pläne der Insel seit dem Start.</param>
/// <param name="OhneVerbesserung">Berechnete Pläne seit ihrer letzten Verbesserung.</param>
/// <param name="Neustarts">Wie oft die Insel neu gestartet wurde.</param>
/// <param name="Strategien">Suchstrategien ihrer Suchplätze in Kurzform (R, 15, S1,25, M2,10 …).</param>
/// <param name="Verstoesse">Im Automodus (nach der Basisoptimierung): Verstöße ihres besten Plans nach Stufen.</param>
/// <param name="Erfolge">Wie oft ein Suchplatz der Insel einen besseren Plan geliefert hat.</param>
/// <param name="Kostentreiber">
/// Die größten Verursacher in ihrem besten Plan: bei der Kostenoptimierung nach Kosten (mit den aktuellen Gewichten), im
/// Automodus nach Verstößen (wichtigste Stufe zuerst).
/// </param>
/// <param name="Schlechteste">
/// Sie beginnt als Nächste neu, wenn sie länger als die Grenze nichts Besseres gefunden hat: die schlechteste Insel (höchste
/// Kosten, im Automodus mit den aktuellen Gewichten bzw. nach Verstößen; bei Gleichstand die, die am längsten feststeckt).
/// </param>
/// <param name="Start">Womit sie im Automodus zuletzt neu begonnen hat; <c>null</c>, solange nicht.</param>
public sealed record Inselstand(
    int Nummer,
    bool Spezial,
    bool Laeuft,
    double Kosten,
    long Durchlaeufe,
    long OhneVerbesserung,
    int Neustarts,
    IReadOnlyList<string> Strategien,
    Stufenwert? Verstoesse = null,
    int Erfolge = 0,
    IReadOnlyList<Kostenverursacher>? Kostentreiber = null,
    bool Schlechteste = false,
    Startverfahren? Start = null);
