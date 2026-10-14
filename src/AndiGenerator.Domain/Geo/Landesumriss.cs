// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Geo;

/// <summary>Umriss eines Bundeslandes für die Karte (nicht im Original).</summary>
/// <param name="Name">Name, z. B. „Rheinland-Pfalz“.</param>
/// <param name="Ringe">Geschlossene Linienzüge (Festland und Inseln), je Punkt Breite und Länge.</param>
public sealed record Landesumriss(string Name, IReadOnlyList<IReadOnlyList<Koordinate>> Ringe);
