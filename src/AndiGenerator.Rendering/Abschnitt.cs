// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Abschnitt eines Ausdrucks; jeder beginnt wie im Original auf einer neuen Seite.</summary>
/// <param name="Titel">Überschrift, z. B. <c>Spielplan</c>.</param>
/// <param name="Bausteine">Der Inhalt.</param>
public sealed record Abschnitt(string Titel, IReadOnlyList<Baustein> Bausteine);
