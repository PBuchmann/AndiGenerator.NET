// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Geo;
using AndiGenerator.Domain.Turniere;

namespace AndiGenerator.Application;

/// <summary>Eine Mannschaft in der Auswertung der Kompaktspieltage (nicht im Original).</summary>
/// <param name="Name">Name der Mannschaft.</param>
/// <param name="Kurzname">Kurzer Name für Raster und Karte, z. B. „Engers“ für „ESV Engers“.</param>
/// <param name="Lage">Lage der Halle oder <c>null</c>, wenn keine Adresse bekannt ist.</param>
/// <param name="Halle">Name der Halle aus click-TT oder leer.</param>
/// <param name="Meldung">Meldung je Sonntag oder <c>null</c>, wenn die Mannschaft nichts gemeldet hat.</param>
/// <param name="Bemerkung">Bemerkung des Vereins aus dem Bericht „Terminwünsche“ oder leer.</param>
/// <param name="Km">Kilometer Luftlinie aller Fahrten zu Kompaktspieltagen, hin und zurück.</param>
/// <param name="Ausgerichtet">Zahl der ausgerichteten Kompaktspieltage (ohne Einzelbegegnungen).</param>
public sealed record Kompaktmannschaft(
    string Name,
    string Kurzname,
    Koordinate? Lage,
    string Halle,
    Turniermeldung? Meldung,
    string Bemerkung,
    double Km,
    int Ausgerichtet);
