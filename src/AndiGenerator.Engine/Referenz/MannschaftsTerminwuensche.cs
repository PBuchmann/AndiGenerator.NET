// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Terminwünsche einer Mannschaft.</summary>
/// <param name="Name">Mannschaftsname.</param>
/// <param name="Marken">Marken für das Diagramm.</param>
/// <param name="Zeilen">Textzeilen wie im Original (Wünsche, Sperrtermine, Koppeln, 60 km, Heimrecht, Auswertung).</param>
public sealed record MannschaftsTerminwuensche(string Name, IReadOnlyList<Terminwunschmarke> Marken, IReadOnlyList<Terminwunschtext> Zeilen);
