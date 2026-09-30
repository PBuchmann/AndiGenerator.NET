// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Tabellen;

/// <summary>Arbeitsblatt einer Arbeitsmappe.</summary>
/// <param name="Name">Name des Blatts; unzulässige Zeichen werden beim Speichern ersetzt, zu lange Namen gekürzt.</param>
/// <param name="Zeilen">Die Zeilen, jede mit ihren Zellen von links.</param>
/// <param name="Kopfzeile">
/// Index der Kopfzeile einer Liste oder <c>null</c>: Sie bleibt beim Blättern stehen und erhält einen Filter über alle
/// folgenden Zeilen.
/// </param>
public sealed record Arbeitsblatt(string Name, IReadOnlyList<IReadOnlyList<Blattzelle>> Zeilen, int? Kopfzeile = null);
