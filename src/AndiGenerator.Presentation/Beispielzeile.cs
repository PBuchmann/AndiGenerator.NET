// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Beispiel auf der Startseite: eine der Staffeln, die dem Programm beiliegen.</summary>
/// <param name="Name">Name, z. B. „Liga mit Koppelterminen“.</param>
/// <param name="Beschreibung">Was das Beispiel zeigt.</param>
/// <param name="Pfad">Pfad der beiliegenden Datei (Parameter für das Öffnen).</param>
public sealed record Beispielzeile(string Name, string Beschreibung, string Pfad);
