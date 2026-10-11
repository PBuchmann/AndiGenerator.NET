// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Dock.Model.Mvvm.Controls;

namespace AndiGenerator.Presentation;

/// <summary>Verlauf der Suche seit dem Start als eigene, formatfüllende Ansicht.</summary>
public sealed class VerlaufAnsichtViewModel : Document
{
    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="hauptfenster">Hauptfenster mit dem Verlauf.</param>
    public VerlaufAnsichtViewModel(HauptfensterViewModel hauptfenster)
    {
        ArgumentNullException.ThrowIfNull(hauptfenster);
        Id = "Verlauf";
        Title = "Verlauf";
        CanClose = true;
        CanFloat = true;
        Verlauf = hauptfenster.Verlauf;
    }

    /// <summary>Holt den Verlauf der Suche.</summary>
    public Suchverlauf Verlauf { get; }
}
