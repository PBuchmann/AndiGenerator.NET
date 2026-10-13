// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Meldung einer Mannschaft an einem Tag, wie sie im Raster der Kompaktspieltage erfasst wird (nicht im Original).</summary>
public enum Kompaktfeld
{
    /// <summary>Kann nicht (Sperrtermin).</summary>
    Leer,

    /// <summary>Nur auswärts (kein Heimtermin, nicht gesperrt).</summary>
    Auswaerts,

    /// <summary>Nur Heim (Heimtermin und „Nur Heim“ in der Bemerkung).</summary>
    Heim,

    /// <summary>Heim oder auswärts (Heimtermin).</summary>
    Beides,
}
