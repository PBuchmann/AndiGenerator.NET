// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Was eine Verbesserung des besten Plans bei einem Kriterium geändert hat.</summary>
/// <param name="Text">Kriterium und Änderung, z. B. „Sperrtermine −1.234“ oder „A · Hallenbelegung 3 → 1“.</param>
/// <param name="Besser">Das Kriterium ist besser geworden (sonst schlechter).</param>
public sealed record Planaenderung(string Text, bool Besser);
