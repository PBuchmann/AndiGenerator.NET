// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Stand eines Laufs des Kompaktplaners.</summary>
/// <param name="Text">Was gerade geschieht, z. B. „Schritt 1 von 4 – Nur 4er-Spieltage: …“.</param>
/// <param name="Anteil">Erledigter Anteil des ganzen Laufs von 0 bis 1.</param>
public sealed record Kompaktfortschritt(string Text, double Anteil);
