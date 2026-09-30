// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Eintrag der Liste der Nachbarmannschaften einer Mannschaft.</summary>
/// <param name="Index">Position unter den Nachbarmannschaften der Mannschaft (für Bearbeiten und Löschen).</param>
/// <param name="Bezeichnung">Anzeige wie im Original, z. B. <c>(Damen) TTC Beispiel 18, Spiele, Spiellokal: 2</c>.</param>
public sealed record Nachbarmannschaftseintrag(int Index, string Bezeichnung);
