// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Geo;
using AndiGenerator.Domain.Turniere;

namespace AndiGenerator.Application;

/// <summary>
/// Eine Mannschaft in der Auswertung der Kompaktspieltage (nicht im Original). Überall, auch in Raster und Karte, steht ihr
/// voller Name wie in click-TT (ab der zweiten Mannschaft mit Nummer): Ein aus dem Vereinsnamen abgeleiteter Kurzname war
/// weder Ort noch eindeutig (Peter, 14.10.2026).
/// </summary>
/// <param name="Name">Name der Mannschaft.</param>
/// <param name="Lage">Lage der Halle oder <c>null</c>, wenn keine Adresse bekannt ist.</param>
/// <param name="Halle">Adresse der Halle aus dem Bericht „Terminwünsche“, sonst der Name der Halle aus click-TT oder leer.</param>
/// <param name="Meldung">Meldung je Sonntag oder <c>null</c>, wenn die Mannschaft nichts gemeldet hat.</param>
/// <param name="Bemerkung">Bemerkung des Vereins aus dem Bericht „Terminwünsche“ oder leer.</param>
/// <param name="Km">Kilometer Luftlinie aller Fahrten zu Kompaktspieltagen, hin und zurück.</param>
/// <param name="Ausgerichtet">Zahl der ausgerichteten Kompaktspieltage (ohne Einzelbegegnungen).</param>
public sealed record Kompaktmannschaft(
    string Name,
    Koordinate? Lage,
    string Halle,
    Turniermeldung? Meldung,
    string Bemerkung,
    double Km,
    int Ausgerichtet)
{
    /// <summary>Holt die Zahl der Einzelbegegnungen, in denen die Mannschaft Heimmannschaft ist (zählen als Heimspieltag mit).</summary>
    public int EinzelHeim { get; init; }

    /// <summary>Holt je Heimtermin der Halbrunde Halle und Beginn als Text (für den Tooltip im Raster).</summary>
    public IReadOnlyDictionary<DateOnly, string> Termine { get; init; } = new Dictionary<DateOnly, string>();
}
