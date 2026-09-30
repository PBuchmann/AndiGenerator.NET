// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Zeile der Kostentabelle.</summary>
/// <param name="Mannschaft">Mannschaftsname (Klick: Gewichtung der Mannschaft).</param>
/// <param name="Zellen">Zellen je sichtbarer Kostenart (Klick: Gewichtung der Kostenart bei dieser Mannschaft).</param>
/// <param name="Gesamt">Summe der Mannschaft (Klick: Gewichtung der Mannschaft).</param>
/// <param name="Meldungen">Klartextmeldungen (zeilenweise), leer wenn keine.</param>
public sealed record Kostenzeile(Kostenfeld Mannschaft, IReadOnlyList<Kostenfeld> Zellen, Kostenfeld Gesamt, string Meldungen);
