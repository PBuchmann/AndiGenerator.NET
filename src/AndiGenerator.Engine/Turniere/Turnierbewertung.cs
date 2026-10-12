// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Ein geprüftes Turnier.</summary>
/// <param name="Turnier">Das Turnier.</param>
/// <param name="Ausrichter">Gewählter Ausrichter oder <c>null</c>, wenn keiner der möglichen ausrichten kann.</param>
/// <param name="Hinweise">Verstöße gegen die Meldungen.</param>
/// <param name="Ausweichtage">Bei Befunden: Tage, an denen das Turnier ohne Befund stattfinden könnte.</param>
public sealed record Turnierbewertung(Turnier Turnier, string? Ausrichter, IReadOnlyList<string> Hinweise, IReadOnlyList<DateOnly> Ausweichtage);
