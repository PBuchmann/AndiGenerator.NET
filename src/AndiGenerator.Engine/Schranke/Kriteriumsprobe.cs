// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Engine.Schranke;

/// <summary>
/// Probe eines Kriteriums der Stufe C bei festgehaltenem A und B (schnelle Schranke, einfacher Modus): wie weit es sich allein
/// senken lässt, ohne A oder B zu verschlechtern.
/// </summary>
/// <param name="Kriterium">Das Kriterium.</param>
/// <param name="Vorher">Seine Verstöße im Modell im besten Plan von A und B.</param>
/// <param name="Gefunden">Seine Verstöße im besten Plan der Probe; <c>null</c>, wenn keiner gefunden wurde.</param>
/// <param name="Schranke">So wenige hat jeder Plan mit diesem A und B mindestens.</param>
/// <param name="Plan">Der beste Plan der Probe (leer, wenn keiner).</param>
public sealed record Kriteriumsprobe(Kostenkriterium Kriterium, int Vorher, int? Gefunden, int Schranke, IReadOnlyList<Spiel> Plan);
