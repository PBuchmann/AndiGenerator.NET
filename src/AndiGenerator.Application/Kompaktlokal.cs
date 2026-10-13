// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Ein Spiellokal einer Mannschaft zur Wahl im Raster der Kompaktspieltage (nicht im Original).</summary>
/// <param name="Nummer">Nummer des Spiellokals („1“ bis „5“).</param>
/// <param name="Halle">Name und Adresse der Halle aus den Terminwünschen oder leer.</param>
public sealed record Kompaktlokal(string Nummer, string Halle)
{
    /// <inheritdoc/>
    public override string ToString() => Halle.Length > 0 ? $"{Nummer} – {Halle}" : $"Spiellokal {Nummer}";
}
