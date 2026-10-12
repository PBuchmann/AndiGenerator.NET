// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Ergebnis der Prüfung „Spiele unter der Woche“ (60-km-Regel) anhand der Hallenadressen.</summary>
/// <param name="Grenze">Grenze in Kilometern Luftlinie.</param>
/// <param name="Befunde">Die Hinweise, zuerst fehlende Sperren, jeweils die weitesten zuerst.</param>
/// <param name="PauschalGesperrt">Mannschaften, die alle Gegner sperren (Vereinswunsch, kein Entfernungsfehler).</param>
/// <param name="OhneLage">Mannschaften, deren Halle nicht gefunden wurde (keine Adresse oder unbekannte Postleitzahl).</param>
/// <param name="Wochentagsgastgeber">Mannschaften mit Heimterminen unter der Woche und am Wochenende (nur bei ihnen wird geprüft).</param>
/// <param name="GeprueftePaare">Zahl der geprüften Paare (Gastgeber mit Heimterminen unter der Woche).</param>
public sealed record Wochenspielpruefung(
    double Grenze,
    IReadOnlyList<Wochenspielbefund> Befunde,
    IReadOnlyList<string> PauschalGesperrt,
    IReadOnlyList<string> OhneLage,
    IReadOnlyList<string> Wochentagsgastgeber,
    int GeprueftePaare);
