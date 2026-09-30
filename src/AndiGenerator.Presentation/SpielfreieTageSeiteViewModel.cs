// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>
/// Seite „Spielfreie Tage“ (Original <c>TDialogPanelFreeDays</c>): alle Tage mit Heimspielwünschen mit Häkchen; an den
/// gewählten Tagen sollen keine Spiele stattfinden.
/// </summary>
public sealed class SpielfreieTageSeiteViewModel : DatenSeiteViewModel
{
    private IReadOnlyList<DateOnly> tage = [];
    private IReadOnlyList<AuswahlZeile> zeilen = [];

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    public SpielfreieTageSeiteViewModel(Datenbearbeitung bearbeitung)
        : base(bearbeitung, "Spielfreie Tage")
    {
        Laden();
    }

    /// <summary>Holt die möglichen Tage mit Häkchen.</summary>
    public IReadOnlyList<AuswahlZeile> Zeilen
    {
        get => zeilen;
        private set => SetProperty(ref zeilen, value);
    }

    /// <inheritdoc/>
    public override bool IstStandard =>
        Bearbeitung.SpielfreieTageStandard() is not IReadOnlyList<DateOnly> standard
        || (standard.Count == Gewaehlt().Count && standard.All(Gewaehlt().Contains));

    /// <inheritdoc/>
    public override void Laden()
    {
        // Die möglichen Tage hängen von den Wunschterminen ab und werden bei jedem Betreten neu bestimmt.
        tage = Bearbeitung.MoeglicheSpieltage();
        Zeilen = tage.Select(t => new AuswahlZeile(Datumsanzeige.Tag(t), false, AenderungMelden)).ToList();
        Anzeigen(Bearbeitung.SpielfreieTage());
    }

    /// <inheritdoc/>
    public override void Speichern() => Bearbeitung.SpielfreieTageSpeichern(Gewaehlt());

    /// <inheritdoc/>
    public override void Standard()
    {
        // Wie im Original: die Häkchen des click-TT-Stands setzen; gespeichert wird beim Verlassen der Seite.
        Anzeigen(Bearbeitung.SpielfreieTageStandard() ?? []);
        AenderungMelden();
    }

    private List<DateOnly> Gewaehlt() => tage.Where((_, i) => Zeilen[i].Gewaehlt).ToList();

    private void Anzeigen(IReadOnlyList<DateOnly> gewaehlt)
    {
        for (int i = 0; i < tage.Count; i++)
        {
            Zeilen[i].Setzen(gewaehlt.Contains(tage[i]));
        }
    }
}
