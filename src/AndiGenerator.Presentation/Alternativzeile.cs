// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eine Zeile im Dialog der Alternativen: ein Kriterium oder eine Summe, je Spalte die Verstöße.</summary>
/// <param name="Stufe">Z. B. <c>B3</c>; leer bei Summen.</param>
/// <param name="Name">Kriterium oder Summe.</param>
/// <param name="IstSumme">Summe einer Stufe (fett, abgesetzt).</param>
/// <param name="Zellen">Je Spalte der Wert.</param>
public sealed record Alternativzeile(string Stufe, string Name, bool IstSumme, IReadOnlyList<Alternativzelle> Zellen);
