// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Seite „Heimkoppel/Doppelspieltage (Kompakt)“ (Original <c>TDialogPanelHomeCoupleCompact</c>).</summary>
public sealed class HeimkoppelnSeiteViewModel : MehrMannschaftenSeiteViewModel
{
    private const string Hinweis =
        "Koppelspieltag: zwei Heimspiele am selben Tag (mit zweiter Anfangszeit). Doppelspieltag: zwei Heimspiele an zwei "
        + "aufeinanderfolgenden Tagen, meist Samstag und Sonntag. Je Termin bzw. Terminpaar wählen Sie, ob das möglich, "
        + "gewünscht oder mit hoher Priorität gewünscht ist.\n"
        + "Neue Koppeltermine und die zweite Anfangszeit legen Sie in den Wunschterminen an.";

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    public HeimkoppelnSeiteViewModel(Datenbearbeitung bearbeitung)
        : base(bearbeitung, "Heimkoppel und Doppelspieltage", Hinweis)
    {
        Laden();
    }

    /// <inheritdoc/>
    protected override DatenSeiteViewModel DetailErzeugen(string name) => new HeimkoppelDetailViewModel(Bearbeitung, name);
}
