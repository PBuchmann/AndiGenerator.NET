// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Referenz;

/// <summary>Ein Kompaktspieltag für das Diagramm „Ausrichter und Spielorte“: wer ausrichtet und wer gegen wen spielt.</summary>
/// <param name="Tag">Der Tag.</param>
/// <param name="Ausrichter">Die Mannschaft, in deren Halle gespielt wird; <c>null</c>, wenn das nicht eindeutig ist.</param>
/// <param name="Spiele">Die Spiele des Spieltags.</param>
public sealed record Ausrichtertag(DateOnly Tag, string? Ausrichter, IReadOnlyList<Spiel> Spiele);
