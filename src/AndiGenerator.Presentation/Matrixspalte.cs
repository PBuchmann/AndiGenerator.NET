// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Presentation;

/// <summary>Eine Spalte (Kostenart) der Kostenmatrix.</summary>
/// <param name="Kostenart">Die Kostenart.</param>
/// <param name="Name">Anzeigename.</param>
/// <param name="Marke">Gewichtungsmarke der Kostenart (+1, −1, X) oder leer.</param>
/// <param name="Balken">Kosten relativ zur teuersten Kostenart (0–1).</param>
/// <param name="HatKosten">Die Kostenart verursacht Kosten.</param>
/// <param name="Gewaehlt">Die Kostenart ist ausgewählt.</param>
public sealed record Matrixspalte(MannschaftsKostenart Kostenart, string Name, string Marke, double Balken, bool HatKosten, bool Gewaehlt);
