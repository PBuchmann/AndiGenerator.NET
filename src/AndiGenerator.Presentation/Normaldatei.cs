// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eine Datei aus click-TT auf der ersten Seite des normalen Modus.</summary>
/// <param name="Titel">Z. B. „Terminwünsche (XML)“.</param>
/// <param name="Rolle">„notwendig“, „empfohlen“ oder „freiwillig“.</param>
/// <param name="Beschreibung">Was darin steht bzw. wozu sie dient.</param>
/// <param name="Geladen">Ob sie geladen ist.</param>
public sealed record Normaldatei(string Titel, string Rolle, string Beschreibung, bool Geladen)
{
    /// <summary>Gets den Zustand als Text („geladen“ bzw. „fehlt“).</summary>
    public string Zustand => Geladen ? "geladen" : "fehlt";
}
