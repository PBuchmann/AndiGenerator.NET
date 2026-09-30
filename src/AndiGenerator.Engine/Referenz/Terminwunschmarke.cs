// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Marke im Terminwunsch-Diagramm.</summary>
/// <param name="Tag">Tag.</param>
/// <param name="Art">Art der Marke.</param>
public sealed record Terminwunschmarke(DateOnly Tag, Terminwunschart Art);
