// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Presentation;

/// <summary>Ein Plan zur Wahl in der Ansicht „Kompaktspieltage“: der Plan aus click-TT oder eine Variante des Planers.</summary>
/// <param name="Titel">z. B. „Ausgewogen“.</param>
/// <param name="Zeile">Kennzahlen, z. B. „2.961 km · Stand höchstens 2 Spiele auseinander“.</param>
/// <param name="Gewaehlt">Dieser Plan wird gezeigt.</param>
/// <param name="Variante">Die Variante oder <c>null</c> für den Plan aus click-TT.</param>
public sealed record Kompaktplanwahl(string Titel, string Zeile, bool Gewaehlt, Kompaktvariante? Variante);
