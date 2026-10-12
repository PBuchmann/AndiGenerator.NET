// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Presentation;

/// <summary>Ein Fahrtwunsch in der Liste der Ansicht „Kompaktspieltage“.</summary>
/// <param name="Text">z. B. „Sinzig spielt in Trier-Olewig“.</param>
/// <param name="Wunsch">Der Wunsch.</param>
public sealed record Fahrtwunschzeile(string Text, Fahrtwunsch Wunsch);
