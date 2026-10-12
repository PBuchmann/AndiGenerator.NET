// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eine Zeile der Setzliste: erwarteter Platz und Mannschaft.</summary>
/// <param name="Platz">Erwarteter Platz am Ende der Saison (1 = Erster).</param>
/// <param name="Name">Name der Mannschaft.</param>
/// <param name="IstGezogen">Beim Ziehen die Lücke an der Ablagestelle (die Zeile selbst hängt am Mauszeiger).</param>
/// <param name="IstGewaehlt">Die gewählte Zeile (für „Nach oben“/„Nach unten“).</param>
public sealed record Setzlistenzeile(int Platz, string Name, bool IstGezogen, bool IstGewaehlt)
{
    /// <summary>Holt die Stärke aus der Mannschaftsmeldung, z. B. „Ø 1.623“, oder einen leeren Text.</summary>
    public string Staerke { get; init; } = string.Empty;

    /// <summary>Holt den Platz als Text, z. B. „3.“.</summary>
    public string PlatzText => Platz.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".";
}
