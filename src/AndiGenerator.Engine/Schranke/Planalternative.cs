// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Engine.Schranke;

/// <summary>
/// Variante in Stufe B: meist ein Plan mit bestmöglichem A, in dem ein B-Kriterium so wenige Verstöße hat, wie HiGHS allein für es
/// gefunden hat, und die übrigen B-Kriterien zusammen danach so wenige wie möglich. Der Staffelleiter kann so wählen, welches
/// B-Kriterium Vorrang hat – oder den besten Plan für die Summe in B, oder (mit mehr A-Verstößen) für A und B zusammen.
/// </summary>
/// <param name="Kriterium">Das B-Kriterium, das hier Vorrang hat; <c>null</c> beim Plan für die Summe in B.</param>
/// <param name="Name">Sein Name.</param>
/// <param name="Plan">Der Plan des Modells (C noch nicht geglättet).</param>
/// <param name="Bewiesen">
/// Der Wert ist bewiesen bestmöglich: beim Kriterium allein, beim Plan für die Summe in B die Summe, bei
/// <paramref name="AUndB"/> die Summe von A und B.
/// </param>
/// <param name="AUndB">
/// Plan für A und B zusammen (jeder Verstoß zählt gleich): A ist hier nicht bestmöglich – er hat mehr A-Verstöße, dafür
/// zusammen weniger.
/// </param>
public sealed record Planalternative(Kostenkriterium? Kriterium, string Name, IReadOnlyList<Spiel> Plan, bool Bewiesen = false, bool AUndB = false);
