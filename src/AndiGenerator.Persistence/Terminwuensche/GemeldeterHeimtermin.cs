// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Terminwuensche;

/// <summary>Ein Terminwunsch (Heimtermin) aus dem Bericht „Terminwünsche“.</summary>
/// <param name="Zeitpunkt">Datum und Uhrzeit.</param>
/// <param name="Halle">Name des Spiellokals.</param>
/// <param name="MaxHeimspiele">„max. Heimspiele“, z. B. „2“ oder „unendlich“.</param>
/// <param name="Heimkoppel">„Heimkoppelspiel“: „-“, „wenn möglich“ oder „unbedingt“.</param>
public sealed record GemeldeterHeimtermin(DateTime Zeitpunkt, string Halle, string MaxHeimspiele, string Heimkoppel);
