// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Seite „Heimkoppel/Doppelspieltage (Kompakt)“ (Original <c>TDialogPanelHomeCoupleCompact</c>).</summary>
public sealed class HeimkoppelnSeiteViewModel : MehrMannschaftenSeiteViewModel
{
    private const string Hinweis =
        "Neue Heimkoppeltermine können nur in den Wunschterminen angelegt werden.\n"
        + "Die zweite Anfangszeit kann ebenfalls nur in den Wunschterminen angegeben werden";

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    public HeimkoppelnSeiteViewModel(Datenbearbeitung bearbeitung)
        : base(bearbeitung, "Heimkoppel/Doppelspieltage (Kompakt)", Hinweis)
    {
        Laden();
    }

    /// <inheritdoc/>
    protected override DatenSeiteViewModel DetailErzeugen(string name) => new HeimkoppelDetailViewModel(Bearbeitung, name);
}
