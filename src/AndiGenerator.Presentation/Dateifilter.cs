// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Dateifilter für Dateiauswahl-Dialoge.</summary>
/// <param name="Name">Anzeigename, z. B. <c>click-TT-Dateien</c>.</param>
/// <param name="Muster">Muster, z. B. <c>*.xml</c>.</param>
public sealed record Dateifilter(string Name, IReadOnlyList<string> Muster);
