// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eine Wahl bei einer Entscheidung der Einrichtung (große Kachel).</summary>
/// <param name="Titel">Die Wahl, z. B. „Nur die Rückrunde“.</param>
/// <param name="Beschreibung">Erläuterung darunter.</param>
public sealed record Einrichtungsoption(string Titel, string Beschreibung);
