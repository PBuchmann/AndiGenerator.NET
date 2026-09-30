// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Eine zuletzt geöffnete Staffel.</summary>
/// <param name="Pfad">Pfad der Datei.</param>
/// <param name="Name">Name der Staffel.</param>
/// <param name="Zeitpunkt">Zeitpunkt des letzten Öffnens.</param>
public sealed record ZuletztGeoeffnet(string Pfad, string Name, DateTime Zeitpunkt);
