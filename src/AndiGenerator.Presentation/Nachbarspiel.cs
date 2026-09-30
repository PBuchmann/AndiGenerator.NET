// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Spiel einer Nachbar- oder Vereinsmannschaft am selben Tag.</summary>
/// <param name="Zeit">Uhrzeit.</param>
/// <param name="Text">Begegnung, z. B. „(H) TTC Musterstadt II – SV Nordhafen“.</param>
/// <param name="Lokal">Spiellokal oder leer.</param>
/// <param name="Echt">Echte Nachbarmannschaft (sonst Vereinsmannschaft).</param>
public sealed record Nachbarspiel(string Zeit, string Text, string Lokal, bool Echt);
