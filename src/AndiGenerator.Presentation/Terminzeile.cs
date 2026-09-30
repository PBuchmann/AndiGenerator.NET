// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Zeile der Terminplanansicht.</summary>
/// <param name="Art">Art der Zeile.</param>
/// <param name="Datum">Datum mit Wochentag, <c>------</c> für nicht terminierte Spiele.</param>
/// <param name="Zeit">Uhrzeit.</param>
/// <param name="Heim">Heimmannschaft bzw. Überschrift.</param>
/// <param name="Gast">Gastmannschaft.</param>
/// <param name="Hinweis">Hinweise wie im Original, z. B. „Ausweichtermin“ oder „kein Wunschtermin“.</param>
public sealed record Terminzeile(Planzeilenart Art, string Datum, string Zeit, string Heim, string Gast, string Hinweis)
{
    /// <summary>Holt eine Leerzeile.</summary>
    public static Terminzeile Leer { get; } = new(Planzeilenart.Abstand, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    /// <summary>Holt den Bindestrich zwischen Heim und Gast (nur bei Spielen).</summary>
    public string Trenner => Art is Planzeilenart.Spiel or Planzeilenart.Nachbarspiel or Planzeilenart.EchtesNachbarspiel ? "-" : string.Empty;
}
