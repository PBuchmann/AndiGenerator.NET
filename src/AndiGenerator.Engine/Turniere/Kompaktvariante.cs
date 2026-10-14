// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Ein Vorschlag des Kompaktplaners.</summary>
/// <param name="Name">Wofür die Variante steht, z. B. „Ausgewogen“ oder „Kürzeste Fahrten · Gleichmäßiger Stand“.</param>
/// <param name="Turniere">Die Spieltage nach Datum.</param>
/// <param name="Kennzahlen">Die Kennzahlen.</param>
public sealed record Kompaktvariante(string Name, IReadOnlyList<Planturnier> Turniere, Kompaktkennzahlen Kennzahlen)
{
    /// <summary>Holt die Ziele anderer Varianten, die dieser Plan (das beste Gesamtpaket) zugleich erfüllt, z. B. „Gemeinsamer Spieltag“.</summary>
    public IReadOnlyList<string> Zugleich { get; init; } = [];
}
