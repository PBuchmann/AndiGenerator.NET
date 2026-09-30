// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Seite „Auswärtskoppeltermine“ (Original <c>TDialogPanelAuswaertskoppel</c>).</summary>
public sealed class AuswaertskoppelnSeiteViewModel : MehrMannschaftenSeiteViewModel
{
    private readonly IOberflaeche oberflaeche;

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche für die Dialoge.</param>
    public AuswaertskoppelnSeiteViewModel(Datenbearbeitung bearbeitung, IOberflaeche oberflaeche)
        : base(bearbeitung, "Auswärtskoppeltermine", string.Empty)
    {
        this.oberflaeche = oberflaeche;
        Laden();
    }

    /// <inheritdoc/>
    protected override DatenSeiteViewModel DetailErzeugen(string name) => new AuswaertskoppelDetailViewModel(Bearbeitung, name, oberflaeche);
}
