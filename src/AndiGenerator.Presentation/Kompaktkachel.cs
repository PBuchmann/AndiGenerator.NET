// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eine Kachel oben im Hauptfenster im Kompaktmodus.</summary>
/// <param name="Titel">z. B. „Fahrten (Luftlinie)“.</param>
/// <param name="Wert">Großer Wert, z. B. „2.961 km“.</param>
/// <param name="Zusatz">Zeile darunter.</param>
/// <param name="Gut">Wert grün darstellen.</param>
/// <param name="Schlecht">Wert rot darstellen.</param>
public sealed record Kompaktkachel(string Titel, string Wert, string Zusatz, bool Gut, bool Schlecht);
