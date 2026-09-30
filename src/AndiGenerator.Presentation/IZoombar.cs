// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ansicht, deren Inhalt vergrößert und verkleinert werden kann.</summary>
public interface IZoombar
{
    /// <summary>Holt die Vergrößerung der Ansicht.</summary>
    Zoom Zoom { get; }
}
