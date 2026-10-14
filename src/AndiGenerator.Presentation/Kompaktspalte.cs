// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Spaltenkopf im Raster der Kompaktspieltage: eine Mannschaft.</summary>
/// <param name="Name">Name der Mannschaft.</param>
/// <param name="Gewaehlt">Mannschaft ist ausgewählt.</param>
public sealed record Kompaktspalte(string Name, bool Gewaehlt)
{
    /// <summary>Holt die Beschriftung: der volle Name, lange Namen in zwei Zeilen wie im Ausdruck.</summary>
    public string Beschriftung => string.Join('\n', Kompaktdruck.Kopfzeilen(Name));
}
