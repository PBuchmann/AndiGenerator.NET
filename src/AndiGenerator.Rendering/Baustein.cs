// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Baustein eines Ausdrucks. Seitenumbrüche fallen immer zwischen Bausteine bzw. zwischen Tabellenzeilen.</summary>
/// <param name="AbstandDavor">Abstand zum vorigen Baustein; entfällt am Seitenanfang.</param>
public abstract record Baustein(double AbstandDavor = 0);
