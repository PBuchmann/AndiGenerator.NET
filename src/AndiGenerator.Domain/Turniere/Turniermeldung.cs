// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Turniere;

/// <summary>
/// Meldung einer Mannschaft für eine Turnierrunde (z. B. Jugend-Verbandsoberliga): je Sonntag, ob sie ausrichten
/// und/oder auswärts spielen kann, dazu die Halle und ob dort zwei Spiele gleichzeitig möglich sind.
/// </summary>
/// <param name="Mannschaft">Name der Mannschaft wie in der Meldung.</param>
/// <param name="Tage">Bereitschaft je Sonntag; fehlt ein Tag, kann die Mannschaft dort nicht.</param>
/// <param name="Halle">Halle für Heimspieltage (z. B. „Halle 1“).</param>
/// <param name="ZweiTische">In der Halle können zwei Spiele gleichzeitig laufen (Voraussetzung für ein 4er-Turnier).</param>
/// <param name="Bemerkung">Freitext der Meldung.</param>
public sealed record Turniermeldung(
    string Mannschaft,
    IReadOnlyDictionary<DateOnly, Turnierbereitschaft> Tage,
    string Halle,
    bool ZweiTische,
    string Bemerkung)
{
    /// <summary>Bereitschaft an einem Tag.</summary>
    /// <param name="tag">Der Tag.</param>
    /// <returns>Die Bereitschaft; <see cref="Turnierbereitschaft.None"/>, wenn der Tag nicht gemeldet ist.</returns>
    public Turnierbereitschaft An(DateOnly tag) => Tage.TryGetValue(tag, out Turnierbereitschaft b) ? b : Turnierbereitschaft.None;
}
