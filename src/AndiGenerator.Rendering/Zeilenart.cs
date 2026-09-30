// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Art einer Tabellenzeile.</summary>
public enum Zeilenart
{
    /// <summary>Normale Zeile mit Zellen.</summary>
    Normal,

    /// <summary>Zwischenüberschrift über die ganze Breite (erste Zelle); bleibt mit der nächsten Zeile zusammen.</summary>
    Ueberschrift,

    /// <summary>Kleiner Abstand (neue Woche, neue Runde); am Seitenanfang entfällt er.</summary>
    Abstand,
}
