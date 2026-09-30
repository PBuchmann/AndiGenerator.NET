// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Csv;

/// <summary>Rahmenbedingungen des Exports.</summary>
/// <param name="Staffelbeginn">Im Original <c>DateBegin</c>: Datum für nicht terminierte Spiele.</param>
/// <param name="Rueckrundenbeginn">Im Original <c>DateBeginRueckrundeInXML</c> (click-TT-Attribut <c>mid</c>, 00:00 Uhr).</param>
/// <param name="Halbrunde">Rundenplanung <c>rpHalfRound</c>: dann immer „Vor/Rück“ = 0.</param>
/// <param name="IstInZuPlanenderRunde">Im Original <c>TPlan.IsInRoundToGenerate</c>; erhält das Datum (bzw. <c>null</c> für nicht terminiert).</param>
public sealed record CsvExportOptionen(
    DateTime Staffelbeginn,
    DateTime Rueckrundenbeginn,
    bool Halbrunde,
    Func<DateTime?, bool> IstInZuPlanenderRunde);
