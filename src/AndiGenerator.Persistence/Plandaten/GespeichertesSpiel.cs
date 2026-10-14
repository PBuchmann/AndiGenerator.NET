// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Plandaten;

/// <summary>Ein Spiel eines gemerkten Plans.</summary>
/// <param name="Zeitpunkt">Termin; <c>null</c> = nicht terminiert (im Original 0.0 = 30.12.1899 00:00).</param>
/// <param name="Heim">Name der Heimmannschaft.</param>
/// <param name="Gast">Name der Gastmannschaft.</param>
/// <param name="Spiellokal">
/// Spiellokal „1“ bis „5“ oder leer (nicht im Original): Bei Kompaktspieltagen trägt es nur der Ausrichter; so bleibt er beim
/// Wiederöffnen erkennbar. Leer wird es nicht geschrieben, die Datei bleibt dann wie im Original.
/// </param>
public sealed record GespeichertesSpiel(DateTime? Zeitpunkt, string Heim, string Gast, string Spiellokal = "");
