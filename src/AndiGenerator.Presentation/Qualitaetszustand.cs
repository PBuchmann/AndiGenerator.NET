// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Zustand eines Qualitätskriteriums für die Anzeige.</summary>
public enum Qualitaetszustand
{
    /// <summary>Erfüllt (grün).</summary>
    Erfuellt,

    /// <summary>Verletzt, aber keine Pflicht (orange).</summary>
    Verletzt,

    /// <summary>Pflicht der Stufe A verletzt (rot).</summary>
    PflichtVerletzt,
}
