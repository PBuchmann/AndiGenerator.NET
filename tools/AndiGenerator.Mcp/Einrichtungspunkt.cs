// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Ein Punkt der Einrichtung.</summary>
/// <param name="Nummer">Nummer ab 1.</param>
/// <param name="Titel">Titel.</param>
/// <param name="Status">Status (offen, erledigt …).</param>
/// <param name="Text">Erklärung.</param>
/// <param name="Optionen">Wahlmöglichkeiten (bei Entscheidungen).</param>
/// <param name="Auswahl">Gewählte Option ab 0 oder -1.</param>
public sealed record Einrichtungspunkt(int Nummer, string Titel, string Status, string Text, IReadOnlyList<string> Optionen, int Auswahl);
