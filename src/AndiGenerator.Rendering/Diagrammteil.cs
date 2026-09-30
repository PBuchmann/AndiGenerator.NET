// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Ein Diagramm der Diagrammansicht (Original <c>PaintVerteilung</c>).</summary>
public enum Diagrammteil
{
    /// <summary>Spieltage je Mannschaft mit Überlappungen (<c>PaintSpielPlanVerteilung</c>).</summary>
    Spieltage,

    /// <summary>Wechsel Heim/Auswärts (<c>PaintSpielWechselHeimAuswaerts</c>).</summary>
    WechselHeimAuswaerts,

    /// <summary>Spielverteilung (<c>PaintSpielAbstaende</c>).</summary>
    Spielverteilung,

    /// <summary>Abstand Heimspiel zu Auswärtsspiel (<c>PaintSpielAbstandHinRueck</c>), nur mit Rückrunde.</summary>
    AbstandHeimAuswaerts,

    /// <summary>Anzahl Spiele pro Woche (<c>PaintGamesPerWeek</c>).</summary>
    SpieleProWoche,

    /// <summary>Setzliste (<c>PaintSpielRanking</c>), nur mit Setzliste.</summary>
    Setzliste,
}
