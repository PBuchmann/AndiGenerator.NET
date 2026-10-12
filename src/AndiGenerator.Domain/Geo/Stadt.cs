// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Geo;

/// <summary>Stadt zur Orientierung auf der Karte (nicht im Original).</summary>
/// <param name="Name">Name, z. B. „Koblenz“.</param>
/// <param name="Rang">Bedeutung: 1 = Großstadt oder Landeshauptstadt, 2 = Großstadt, 3 = regionales Zentrum.</param>
/// <param name="Lage">Lage.</param>
public sealed record Stadt(string Name, int Rang, Koordinate Lage);
