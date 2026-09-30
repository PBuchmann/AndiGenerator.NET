// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Seite „60km Regel“ (Original <c>TDialogPanel60km</c>).</summary>
public sealed class WochenendeSeiteViewModel : MehrMannschaftenSeiteViewModel
{
    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    public WochenendeSeiteViewModel(Datenbearbeitung bearbeitung)
        : base(bearbeitung, "60km Regel", "Wegen des langen Anfahrtsweges dürfen Spiele gegen diese Mannschaften nur am Wochenende stattfinden")
    {
        Laden();
    }

    /// <inheritdoc/>
    protected override DatenSeiteViewModel DetailErzeugen(string name) => new WochenendeDetailViewModel(Bearbeitung, name);
}
