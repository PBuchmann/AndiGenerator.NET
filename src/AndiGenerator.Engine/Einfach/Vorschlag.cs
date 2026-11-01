// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Einfach;

/// <summary>
/// Ein Plan, den der einfache Modus dem Klassenleiter zur Wahl anbietet (wie im Kompaktmodus): die Empfehlung oder ein Plan,
/// der eine Gruppe besonders gut erfüllt und sich wesentlich von den anderen unterscheidet.
/// </summary>
/// <param name="Name">Kurzname, z. B. „Empfehlung“ oder „Besser bei Heim/Auswärts und Abstände“.</param>
/// <param name="Gruppe">
/// Die Gruppe, in der er gegenüber der Empfehlung am meisten gewinnt (Index in <see cref="Massstab.Gruppen"/>; bei der
/// Empfehlung der Hebel), oder <c>null</c>.
/// </param>
/// <param name="Plan">Der Plan mit Verstößen und Gruppenwerten.</param>
/// <param name="Unterschiede">Worin er sich von der Empfehlung unterscheidet (die größten zuerst; leer bei der Empfehlung).</param>
public sealed record Vorschlag(string Name, int? Gruppe, Grenzplan Plan, IReadOnlyList<Unterschied> Unterschiede);
