// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Text, der bei Bedarf an Wortgrenzen auf mehrere Zeilen umbrochen wird.</summary>
/// <param name="Text">Der Text.</param>
/// <param name="Groesse">Schriftgröße in Punkt.</param>
/// <param name="Farbe">Schriftfarbe.</param>
/// <param name="Fett">Fettschrift.</param>
/// <param name="Einzug">Einzug links in Punkt.</param>
/// <param name="AbstandDavor">Freiraum über dem Text in Punkt.</param>
public sealed record Textzeile(string Text, double Groesse, Farbe Farbe, bool Fett = false, double Einzug = 0, double AbstandDavor = 0) : Baustein(AbstandDavor);
