// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Eine Zeile der Terminübersicht (Original <c>TDialogPanelHomeDaysDetail</c>): Montag und die sieben Tagestexte.</summary>
/// <param name="Montag">Montag der Woche.</param>
/// <param name="Texte">Kurztexte Montag bis Sonntag (siehe <see cref="Heimtagtext"/>).</param>
public sealed record Wunschterminwoche(DateOnly Montag, IReadOnlyList<string> Texte);
