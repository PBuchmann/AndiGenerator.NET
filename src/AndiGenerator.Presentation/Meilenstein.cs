// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Meilenstein im Verlauf, z. B. „Basisoptimierung fertig“ oder „A bestmöglich“.</summary>
/// <param name="Sekunden">Rechenzeit seit dem Start.</param>
/// <param name="Text">Was geschah.</param>
public sealed record Meilenstein(double Sekunden, string Text);
