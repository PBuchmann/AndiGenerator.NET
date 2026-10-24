// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Eine Mannschaft an einem Termin eines Spieltags.</summary>
/// <param name="Name">Name der Mannschaft.</param>
/// <param name="Tag">Tag des Spiels.</param>
/// <param name="Heimspiel">Ob sie zu Hause spielt.</param>
public sealed record Spieltagsmannschaft(string Name, DateOnly Tag, bool Heimspiel);
