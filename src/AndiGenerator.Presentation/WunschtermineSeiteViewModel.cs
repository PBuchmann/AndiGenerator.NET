// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Seite „Wunschtermine“ (Original <c>TDialogPanelHomeDays</c>).</summary>
public sealed class WunschtermineSeiteViewModel : MehrMannschaftenSeiteViewModel
{
    private readonly IOberflaeche oberflaeche;

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche für den Dialog.</param>
    public WunschtermineSeiteViewModel(Datenbearbeitung bearbeitung, IOberflaeche oberflaeche)
        : base(bearbeitung, "Wunschtermine", string.Empty)
    {
        this.oberflaeche = oberflaeche;
        Laden();
    }

    /// <inheritdoc/>
    protected override DatenSeiteViewModel DetailErzeugen(string name) => new WunschterminDetailViewModel(Bearbeitung, name, oberflaeche);
}
