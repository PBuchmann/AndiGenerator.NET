// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Nachbarmannschaften einer Mannschaft (Original <c>TDialogPanelSisterTeamsDetail</c>): Liste mit Neu, Bearbeiten und
/// Löschen; bearbeitet wird im Dialog „Nachbarmannschaft“.
/// </summary>
public sealed class NachbarmannschaftDetailViewModel : DatenSeiteViewModel
{
    private readonly string mannschaft;
    private readonly IOberflaeche oberflaeche;
    private IReadOnlyList<Nachbarmannschaftseintrag> eintraege = [];
    private Nachbarmannschaftseintrag? auswahl;

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche für die Dialoge.</param>
    public NachbarmannschaftDetailViewModel(Datenbearbeitung bearbeitung, string mannschaft, IOberflaeche oberflaeche)
        : base(bearbeitung, "Nachbarmannschaften")
    {
        ArgumentNullException.ThrowIfNull(oberflaeche);
        this.mannschaft = mannschaft;
        this.oberflaeche = oberflaeche;
        NeuCommand = new AsyncRelayCommand(NeuAsync);
        BearbeitenCommand = new AsyncRelayCommand(BearbeitenAsync, () => Auswahl is not null);
        LoeschenCommand = new AsyncRelayCommand(LoeschenAsync, () => Auswahl is not null);
        Laden();
    }

    /// <summary>Holt die Nachbarmannschaften.</summary>
    public IReadOnlyList<Nachbarmannschaftseintrag> Eintraege
    {
        get => eintraege;
        private set => SetProperty(ref eintraege, value);
    }

    /// <summary>Holt oder setzt die gewählte Nachbarmannschaft.</summary>
    public Nachbarmannschaftseintrag? Auswahl
    {
        get => auswahl;
        set
        {
            if (SetProperty(ref auswahl, value))
            {
                BearbeitenCommand.NotifyCanExecuteChanged();
                LoeschenCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Holt den Befehl „Neue Mannschaft…“.</summary>
    public IAsyncRelayCommand NeuCommand { get; }

    /// <summary>Holt den Befehl „Mannschaft bearbeiten…“ (auch per Doppelklick).</summary>
    public IAsyncRelayCommand BearbeitenCommand { get; }

    /// <summary>Holt den Befehl „Mannschaft löschen…“.</summary>
    public IAsyncRelayCommand LoeschenCommand { get; }

    /// <inheritdoc/>
    public override bool IstStandard => Bearbeitung.NachbarmannschaftenSindStandard(mannschaft);

    /// <inheritdoc/>
    public override void Laden()
    {
        // Wie im Original bleibt die Position in der Liste erhalten.
        int position = Auswahl is null ? -1 : Eintraege.ToList().IndexOf(Auswahl);
        Eintraege = Bearbeitung.Nachbarmannschaften(mannschaft);
        Auswahl = position >= 0 && position < Eintraege.Count ? Eintraege[position] : null;
    }

    /// <inheritdoc/>
    public override void Speichern()
    {
        // Änderungen stehen sofort in der Arbeitskopie (Original: FormToData leer).
    }

    /// <inheritdoc/>
    public override void Standard()
    {
        Bearbeitung.NachbarmannschaftenZuruecksetzen(mannschaft);
        Laden();
        AenderungMelden();
    }

    private Task NeuAsync() => BearbeitenAsync(null);

    private Task BearbeitenAsync() => Auswahl is Nachbarmannschaftseintrag e ? BearbeitenAsync(e.Index) : Task.CompletedTask;

    private async Task BearbeitenAsync(int? index)
    {
        if (await Nachbarmannschaftsbearbeitung.BearbeitenAsync(Bearbeitung, mannschaft, index, oberflaeche))
        {
            Laden();
            AenderungMelden();
        }
    }

    private async Task LoeschenAsync()
    {
        if (Auswahl is Nachbarmannschaftseintrag e
            && await oberflaeche.FragenAsync("Nachbarmannschaft löschen", $"Möchten Sie die Mannschaft {e.Bezeichnung} wirklich löschen?"))
        {
            Bearbeitung.NachbarmannschaftLoeschen(mannschaft, e.Index);
            Laden();
            AenderungMelden();
        }
    }
}
