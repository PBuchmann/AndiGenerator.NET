// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Ergebnis einer Aktion: ob sie geklappt hat, was dabei gemeldet wurde, und der Stand danach.</summary>
/// <param name="Erfolg">Die Aktion hat geklappt.</param>
/// <param name="Text">Was geschehen ist bzw. warum nicht.</param>
/// <param name="Stand">Der Stand danach.</param>
public sealed record Aktionsergebnis(bool Erfolg, string Text, Stand Stand);
