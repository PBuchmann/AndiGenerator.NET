// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eintrag „Zuletzt geöffnet“ auf der Startseite.</summary>
/// <param name="Name">Name der Staffel.</param>
/// <param name="Pfad">Pfad der Datei (Parameter für das Öffnen).</param>
/// <param name="Ordner">Ordner der Datei zur Anzeige.</param>
/// <param name="Zeit">Zeitpunkt, z. B. „heute, 14:32“.</param>
public sealed record Zuletztzeile(string Name, string Pfad, string Ordner, string Zeit);
