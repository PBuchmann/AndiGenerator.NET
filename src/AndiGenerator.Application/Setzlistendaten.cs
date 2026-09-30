// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Setzliste (Original <c>TDialogPanelRanking</c>): erwartete Tabelle am Saisonende.</summary>
/// <param name="Aktiv">Die Setzliste wird bei der Generierung berücksichtigt.</param>
/// <param name="Reihenfolge">Alle Mannschaften in der erwarteten Reihenfolge (Platz 1 zuerst).</param>
public sealed record Setzlistendaten(bool Aktiv, IReadOnlyList<string> Reihenfolge);
