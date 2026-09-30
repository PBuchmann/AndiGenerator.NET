// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>
/// 60-km-Regel einer Mannschaft (Original <c>TDialogPanel60kmDetail</c>): alle anderen Mannschaften mit Häkchen, gegen die
/// wegen des langen Anfahrtswegs nur am Wochenende gespielt werden soll.
/// </summary>
public sealed class WochenendeDetailViewModel : DatenSeiteViewModel
{
    private readonly string mannschaft;

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="mannschaft">Mannschaftsname.</param>
    public WochenendeDetailViewModel(Datenbearbeitung bearbeitung, string mannschaft)
        : base(bearbeitung, "60km Regel")
    {
        this.mannschaft = mannschaft;
        Gegner = bearbeitung.Mannschaftsnamen().Where(n => n != mannschaft).Select(n => new AuswahlZeile(n, false, AenderungMelden)).ToList();
        Laden();
    }

    /// <summary>Holt alle anderen Mannschaften mit Häkchen.</summary>
    public IReadOnlyList<AuswahlZeile> Gegner { get; }

    /// <inheritdoc/>
    public override bool IstStandard =>
        Bearbeitung.NurWochenendeStandard(mannschaft) is not IReadOnlyList<string> standard
        || (standard.Count == Gewaehlt().Count && standard.All(Gewaehlt().Contains));

    /// <inheritdoc/>
    public override void Laden() => Anzeigen(Bearbeitung.NurWochenendeGegen(mannschaft));

    /// <inheritdoc/>
    public override void Speichern() => Bearbeitung.NurWochenendeSpeichern(mannschaft, Gewaehlt());

    /// <inheritdoc/>
    public override void Standard()
    {
        // Wie im Original: die Häkchen des click-TT-Stands setzen; gespeichert wird beim Verlassen der Seite.
        Anzeigen(Bearbeitung.NurWochenendeStandard(mannschaft) ?? []);
        AenderungMelden();
    }

    private List<string> Gewaehlt() => Gegner.Where(g => g.Gewaehlt).Select(g => g.Name).ToList();

    private void Anzeigen(IReadOnlyList<string> gewaehlt)
    {
        foreach (AuswahlZeile g in Gegner)
        {
            g.Setzen(gewaehlt.Contains(g.Name, StringComparer.OrdinalIgnoreCase));
        }
    }
}
