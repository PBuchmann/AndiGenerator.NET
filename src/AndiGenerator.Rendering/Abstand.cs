// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Freiraum; am Seitenanfang entfällt er.</summary>
/// <param name="AbstandDavor">Höhe des Freiraums in Punkt.</param>
public sealed record Abstand(double AbstandDavor) : Baustein(AbstandDavor);
