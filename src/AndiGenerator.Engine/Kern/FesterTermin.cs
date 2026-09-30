// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Kern;

/// <summary>Vorgegebenes Spiel oder Spiel des bestehenden Spielplans.</summary>
internal readonly record struct FesterTermin(int Heim, int Gast, double Datum);
