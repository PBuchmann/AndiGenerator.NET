// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>
/// Heimkoppel- und Doppelspieltagswünsche einer Mannschaft (Original <c>TDialogPanelHomeCoupleCompactDetail</c>).
/// Standard ist der Stand der click-TT-Datei.
/// </summary>
public sealed class HeimkoppelDetailViewModel : DatenSeiteViewModel
{
    private readonly string mannschaft;
    private IReadOnlyList<HeimkoppelZeile> zeilen = [];

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="mannschaft">Mannschaftsname.</param>
    public HeimkoppelDetailViewModel(Datenbearbeitung bearbeitung, string mannschaft)
        : base(bearbeitung, "Heimkoppel/Doppelspieltage")
    {
        this.mannschaft = mannschaft;
        Laden();
    }

    /// <summary>Holt die Wünsche.</summary>
    public IReadOnlyList<HeimkoppelZeile> Zeilen
    {
        get => zeilen;
        private set
        {
            if (SetProperty(ref zeilen, value))
            {
                OnPropertyChanged(nameof(Keine));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es keine möglichen Koppeltermine gibt.</summary>
    public bool Keine => Zeilen.Count == 0;

    /// <inheritdoc/>
    public override bool IstStandard =>
        Bearbeitung.HeimkoppelnStandard(mannschaft) is not IReadOnlyList<Heimkoppel> standard
        || standard.SequenceEqual(Zeilen.Select(z => z.Wert));

    /// <inheritdoc/>
    public override void Laden() =>
        Zeilen = Bearbeitung.Heimkoppeln(mannschaft).Select(k => new HeimkoppelZeile(k, AenderungMelden)).ToList();

    /// <inheritdoc/>
    public override void Speichern() => Bearbeitung.HeimkoppelnSpeichern(mannschaft, Zeilen.Select(z => z.Wert));

    /// <inheritdoc/>
    public override void Standard()
    {
        // Wie im Original: die Wünsche des click-TT-Stands übernehmen und neu lesen.
        if (Bearbeitung.HeimkoppelnStandard(mannschaft) is IReadOnlyList<Heimkoppel> standard)
        {
            Bearbeitung.HeimkoppelnSpeichern(mannschaft, standard);
            Laden();
            AenderungMelden();
        }
    }
}
