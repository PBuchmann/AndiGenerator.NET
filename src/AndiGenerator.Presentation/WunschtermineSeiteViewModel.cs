// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Seite „Wunschtermine“ (Original <c>TDialogPanelHomeDays</c>).</summary>
public sealed class WunschtermineSeiteViewModel : MehrMannschaftenSeiteViewModel
{
    private const string Hinweis =
        "Beispiele für die Termineingabe:\n"
        + "18:00: Mannschaft kann um 18:00 Uhr ein Heimspiel austragen.\n"
        + "FREI: Eingabe des Textes 'FREI' bedeutet, dass die Mannschaft an diesem Tag kein Spiel machen will.\n"
        + "Keine Eingabe: Die Mannschaft kann an diesem Tag ein Auswärtsspiel wahrnehmen.\n\n"
        + "Weitere Optionen (maximale Heimspiele, Koppeloptionen, Ausweichtermin etc.) mit Doppelklick oder Maus rechts.";

    private readonly IOberflaeche oberflaeche;

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche für den Dialog.</param>
    public WunschtermineSeiteViewModel(Datenbearbeitung bearbeitung, IOberflaeche oberflaeche)
        : base(bearbeitung, "Wunschtermine", Hinweis)
    {
        this.oberflaeche = oberflaeche;
        Laden();
    }

    /// <inheritdoc/>
    protected override DatenSeiteViewModel DetailErzeugen(string name) => new WunschterminDetailViewModel(Bearbeitung, name, oberflaeche);
}
