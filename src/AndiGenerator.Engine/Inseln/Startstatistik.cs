// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Inseln;

/// <summary>Wie oft Inseln mit einem Startverfahren neu begonnen haben und wie oft sie danach den besten Plan verbessert haben.</summary>
/// <param name="Verfahren">Das Startverfahren.</param>
/// <param name="Starts">So oft hat eine Insel damit begonnen.</param>
/// <param name="Erfolge">So oft hat eine damit begonnene Insel den besten Plan verbessert.</param>
public sealed record Startstatistik(Startverfahren Verfahren, int Starts, int Erfolge);
