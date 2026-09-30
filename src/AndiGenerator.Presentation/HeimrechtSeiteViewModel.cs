// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Seite „Heimrecht“ (Original <c>TDialogPanelHomeRight</c>).</summary>
public sealed class HeimrechtSeiteViewModel : MehrMannschaftenSeiteViewModel
{
    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    public HeimrechtSeiteViewModel(Datenbearbeitung bearbeitung)
        : base(bearbeitung, "Heimrecht", string.Empty)
    {
        Laden();
    }

    /// <inheritdoc/>
    protected override DatenSeiteViewModel DetailErzeugen(string name) => new HeimrechtDetailViewModel(Bearbeitung, name);
}
