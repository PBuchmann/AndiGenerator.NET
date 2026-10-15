// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Engine.Schranke;

/// <summary>Ein Ausschnitt der Teilneuplanung beginnt oder ist fertig (für den Leitstand).</summary>
/// <param name="Mannschaften">So viele Mannschaften hat der Ausschnitt.</param>
/// <param name="Ergebnis">Läuft noch oder wie er ausgegangen ist.</param>
/// <param name="Bester">Der beste Plan der Neuplanung danach.</param>
public sealed record Ausschnittsmeldung(int Mannschaften, Ausschnittsergebnis Ergebnis, Stufenwert Bester);
