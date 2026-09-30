// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Seite „Spiele der Nachbarmannschaften“ (Original <c>TDialogPanelSisterTeams</c>).</summary>
public sealed class NachbarmannschaftenSeiteViewModel : MehrMannschaftenSeiteViewModel
{
    private readonly IOberflaeche oberflaeche;

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche für die Dialoge.</param>
    public NachbarmannschaftenSeiteViewModel(Datenbearbeitung bearbeitung, IOberflaeche oberflaeche)
        : base(bearbeitung, "Spiele der Nachbarmannschaften", string.Empty)
    {
        this.oberflaeche = oberflaeche;
        Laden();
    }

    /// <inheritdoc/>
    protected override DatenSeiteViewModel DetailErzeugen(string name) => new NachbarmannschaftDetailViewModel(Bearbeitung, name, oberflaeche);
}
