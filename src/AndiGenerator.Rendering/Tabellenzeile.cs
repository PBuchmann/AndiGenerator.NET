// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Zeile einer Tabelle.</summary>
/// <param name="Zellen">Die Zellen (bei einer Überschrift nur die erste).</param>
/// <param name="Farbe">Schriftfarbe.</param>
/// <param name="Fett">Fettschrift.</param>
/// <param name="Art">Art der Zeile.</param>
public sealed record Tabellenzeile(IReadOnlyList<Tabellenzelle> Zellen, Farbe Farbe, bool Fett = false, Zeilenart Art = Zeilenart.Normal)
{
    /// <summary>Holt eine Abstandszeile.</summary>
    public static Tabellenzeile Leer { get; } = new([], Farbe.Tinte, Art: Zeilenart.Abstand);

    /// <summary>Erzeugt eine Zwischenüberschrift.</summary>
    /// <param name="text">Der Text.</param>
    /// <returns>Die Zeile.</returns>
    public static Tabellenzeile Ueberschrift(string text) => new([new Tabellenzelle(text)], Farbe.AkzentDunkel, true, Zeilenart.Ueberschrift);
}
