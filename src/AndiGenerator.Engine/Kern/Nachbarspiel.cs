// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Kern;

/// <summary>Spiel einer Nachbarmannschaft in anderer Staffel (Original <c>TSisterGame</c>), kompakt.</summary>
internal readonly record struct Nachbarspiel(
    double Datum,
    int Tag,
    bool IstHeimspiel,
    int Lokal,
    int Nummer,
    bool GleichesGeschlecht,
    bool KeineParallelenSpiele,
    bool ParalleleHeimspiele,
    int Schluessel);
