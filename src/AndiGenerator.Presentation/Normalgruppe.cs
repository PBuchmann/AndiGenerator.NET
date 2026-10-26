// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Presentation;

/// <summary>Eine Gruppe der Seite „Planung“: Muss, Soll oder Feinschliff mit Summe, Grenze und ihren Balken.</summary>
/// <param name="Marke">„MUSS“, „SOLL“ oder „FEINSCHLIFF“.</param>
/// <param name="Stufe">Die Stufe (Farbe).</param>
/// <param name="Summe">Z. B. „9 Verstöße“.</param>
/// <param name="Grenze">Z. B. „bewiesen: mindestens 7 · gespeichert 11“.</param>
/// <param name="Zeilen">Die Kriterien.</param>
/// <param name="Leer">Text statt der Zeilen, wenn es keine gibt (z. B. „Alle Pflichtregeln erfüllt.“).</param>
public sealed record Normalgruppe(string Marke, Stufe Stufe, string Summe, string Grenze, IReadOnlyList<Normalbalken> Zeilen, string Leer)
{
    /// <summary>Gets a value indicating whether die Gruppe Zeilen hat.</summary>
    public bool HatZeilen => Zeilen.Count > 0;
}
