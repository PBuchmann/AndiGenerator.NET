// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Plandaten;

/// <summary>Status eines Knotens in einer Differenz (<c>.modifications</c>). Persistiert als Delphi-Enumname.</summary>
public enum KnotenStatus
{
    /// <summary><c>dosNormal</c> (wird nicht geschrieben).</summary>
    Normal,

    /// <summary><c>dosNew</c>: Knoten ersetzt beim Zusammenführen den Basisknoten vollständig.</summary>
    Neu,

    /// <summary><c>dosModified</c>: Attribute des Knotens werden beim Zusammenführen übernommen.</summary>
    Geaendert,

    /// <summary><c>dosDelete</c>: Knoten wird beim Zusammenführen entfernt.</summary>
    Geloescht,
}
