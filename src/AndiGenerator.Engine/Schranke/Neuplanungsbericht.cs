// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Engine.Schranke;

/// <summary>Ergebnis der Teilneuplanung mit HiGHS.</summary>
/// <param name="Plan">Der beste Plan (der Ausgangsplan, wenn nichts besser wurde).</param>
/// <param name="Vorher">Bewertung des Ausgangsplans nach Stufen.</param>
/// <param name="Nachher">Bewertung des besten Plans nach Stufen.</param>
/// <param name="Schritte">Neu geplante Ausschnitte.</param>
/// <param name="Angenommen">Davon angenommen (echt besser).</param>
/// <param name="Protokoll">Je Schritt eine Zeile.</param>
public sealed record Neuplanungsbericht(IReadOnlyList<Spiel> Plan, Stufenwert Vorher, Stufenwert Nachher, int Schritte, int Angenommen, IReadOnlyList<string> Protokoll);
