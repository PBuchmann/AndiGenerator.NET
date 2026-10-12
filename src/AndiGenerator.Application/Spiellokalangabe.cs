// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Spiellokal eines Vereins aus dem Bericht „Terminwünsche“.</summary>
/// <param name="Nummer">Nummer 1 bis 3.</param>
/// <param name="Name">Name der Halle.</param>
/// <param name="Adresse">Straße, Postleitzahl und Ort.</param>
public sealed record Spiellokalangabe(int Nummer, string Name, string Adresse);
