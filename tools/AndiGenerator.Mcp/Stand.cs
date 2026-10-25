// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Der Stand für die KI: Staffel, Verfahren, Generierung, nächster Schritt und gesammelte Meldungen.</summary>
/// <param name="Staffel">Name der Staffel (leer ohne geöffnete Staffel).</param>
/// <param name="Info">Kurzinfo zur Staffel.</param>
/// <param name="Einrichtung">Die Einrichtung ist noch offen (erst „einrichtung_abschliessen“).</param>
/// <param name="Verfahren">Kostenoptimierung, Automodus oder Kompaktspieltage.</param>
/// <param name="Laeuft">Die Generierung bzw. Berechnung läuft (auch angehalten).</param>
/// <param name="Angehalten">Sie ist angehalten.</param>
/// <param name="Startplan">Ausgangsplan des nächsten Starts.</param>
/// <param name="Startplaene">Mögliche Ausgangspläne.</param>
/// <param name="Ergebnis">Stand der Generierung (normale Runden) oder <c>null</c>.</param>
/// <param name="Statuszeile">Die Statuszeile der Oberfläche.</param>
/// <param name="NaechsterSchritt">Nächster Schritt der Führung „Schritt für Schritt“.</param>
/// <param name="Meldungen">Meldungen und Fragen seit der letzten Antwort.</param>
public sealed record Stand(
    string Staffel,
    string Info,
    bool Einrichtung,
    string Verfahren,
    bool Laeuft,
    bool Angehalten,
    string Startplan,
    IReadOnlyList<string> Startplaene,
    Generierungsstand? Ergebnis,
    string Statuszeile,
    string NaechsterSchritt,
    IReadOnlyList<string> Meldungen);
