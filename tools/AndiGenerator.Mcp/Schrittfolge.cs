// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Die Führung „Schritt für Schritt“, die zur Lage passt.</summary>
/// <param name="Titel">Titel der Führung.</param>
/// <param name="NaechsterSchritt">Der nächste Schritt.</param>
/// <param name="Schritte">Alle Schritte mit Text und Abschnitt der Anleitung.</param>
public sealed record Schrittfolge(string Titel, string NaechsterSchritt, IReadOnlyList<string> Schritte);
