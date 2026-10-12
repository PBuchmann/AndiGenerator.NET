// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Turniere;

namespace AndiGenerator.Engine.Turniere;

/// <summary>Eingaben des Kompaktplaners (nicht im Original).</summary>
/// <param name="Mannschaften">Alle Mannschaften der Staffel.</param>
/// <param name="Tage">Mögliche Spieltage (Sonntage der Halbrunde).</param>
/// <param name="Meldungen">Meldungen der Mannschaften; ohne Meldung gilt jeder Tag als möglich.</param>
/// <param name="Entfernung">Einfache Entfernung zwischen den Hallen zweier Mannschaften in km (Luftlinie).</param>
/// <param name="Festgehalten">Spieltage, die unverändert in jeden Plan übernommen werden.</param>
/// <param name="Fahrtwuensche">Fahrtwünsche: Diese Paarungen finden in der Halle des Gastgebers statt.</param>
public sealed record Kompaktaufgabe(
    IReadOnlyList<string> Mannschaften,
    IReadOnlyList<DateOnly> Tage,
    IReadOnlyList<Turniermeldung> Meldungen,
    Func<string, string, double> Entfernung,
    IReadOnlyList<Planturnier> Festgehalten,
    IReadOnlyList<Fahrtwunsch> Fahrtwuensche);
