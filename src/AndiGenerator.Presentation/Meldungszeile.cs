// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Zeile der Meldungsansicht.</summary>
/// <param name="Text">Der Text.</param>
/// <param name="Ueberschrift"><c>true</c> für den Namen der Mannschaft, sonst eine Meldung.</param>
public sealed record Meldungszeile(string Text, bool Ueberschrift);
