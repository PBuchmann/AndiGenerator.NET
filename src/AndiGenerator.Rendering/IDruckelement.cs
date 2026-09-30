// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Fertig platziertes Zeichenelement einer Druckseite.</summary>
public interface IDruckelement
{
    /// <summary>Zeichnet das Element.</summary>
    /// <param name="flaeche">Die Zeichenfläche.</param>
    void Zeichnen(IZeichenflaeche flaeche);
}
