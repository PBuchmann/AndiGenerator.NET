// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Bezug der Punkte eines Plans aus Kompaktspieltagen.</summary>
/// <param name="Km">Kilometer des kürzesten Plans (nur aus 4er-Spieltagen, wenn es solche gibt).</param>
/// <param name="Spanne">Kleinster Unterschied der Kilometer zwischen den Mannschaften unter diesen Plänen.</param>
/// <param name="MitVierern">
/// Es gibt Pläne nur aus 4er-Spieltagen: Dann kosten 3er-Spieltage Punkte. Sonst (z. B. bei 6 bis 8 Mannschaften) sind sie
/// nötig und kosten nichts.
/// </param>
public sealed record Kompaktbezug(double Km, double Spanne, bool MitVierern);
