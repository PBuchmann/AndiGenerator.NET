// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Presentation;

/// <summary>Ein Plan samt Bewertung, wie ihn die Ansichten anzeigen.</summary>
/// <param name="Spiele">Alle Spiele des Plans.</param>
/// <param name="Bewertung">Bewertung wie im Kosten-Tab des Originals.</param>
public sealed record Planstand(IReadOnlyList<Spiel> Spiele, Planbewertung Bewertung);
