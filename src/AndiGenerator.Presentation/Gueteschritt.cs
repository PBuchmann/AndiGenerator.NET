// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Punkt des Güteverlaufs im normalen Modus: der Wert des besten Plans zu einem Zeitpunkt.</summary>
/// <param name="Sekunden">Sekunden seit dem Start der Planung (ohne Pausen).</param>
/// <param name="Wert">Die Schönheitsfehler (Feinschliff) des besten Plans.</param>
/// <param name="Grenze">So viele hat jeder Plan mindestens (aus HiGHS), oder <c>null</c>, wenn nichts bekannt ist.</param>
public sealed record Gueteschritt(double Sekunden, int Wert, int? Grenze);
