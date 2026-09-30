// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Presentation;

/// <summary>
/// Meldungen je Mannschaft (Original: Text unter der Kostentabelle in <c>PaintKosten</c>, <c>tmNormalMessage</c>): was
/// genau die Kosten einer Mannschaft verursacht, z. B. welche Sperrtermine oder Wünsche nicht eingehalten sind. Hier als
/// eigene Ansicht, wahlweise für eine einzelne Mannschaft.
/// </summary>
public sealed class MeldungenAnsichtViewModel : AnsichtViewModel
{
    /// <summary>Eintrag der Mannschaftsauswahl für alle Mannschaften.</summary>
    public const string AlleMannschaften = "Alle Mannschaften";

    private IReadOnlyList<Mannschaftsbewertung> bewertungen = [];
    private IReadOnlyList<string> mannschaften = [AlleMannschaften];
    private string mannschaft = AlleMannschaften;
    private IReadOnlyList<Meldungszeile> zeilen = [];
    private IReadOnlyList<Meldungsgruppe> gruppen = [];
    private string zusammenfassung = string.Empty;

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="hauptfenster">Hauptfenster.</param>
    /// <param name="id">Eindeutige Kennung im Layout.</param>
    public MeldungenAnsichtViewModel(HauptfensterViewModel hauptfenster, string id)
        : base(hauptfenster, "Meldungen", id)
    {
    }

    /// <summary>Holt die Auswahl: alle Mannschaften oder eine einzelne.</summary>
    public IReadOnlyList<string> Mannschaften
    {
        get => mannschaften;
        private set => SetProperty(ref mannschaften, value);
    }

    /// <summary>Holt oder setzt die gewählte Mannschaft bzw. <see cref="AlleMannschaften"/>.</summary>
    public string? Mannschaft
    {
        get => mannschaft;
        set
        {
            if (SetProperty(ref mannschaft, value ?? AlleMannschaften))
            {
                ZeilenBilden();
            }
        }
    }

    /// <summary>Holt die Zeilen: je Mannschaft mit Meldungen ihr Name und darunter die Meldungen.</summary>
    public IReadOnlyList<Meldungszeile> Zeilen
    {
        get => zeilen;
        private set => SetProperty(ref zeilen, value);
    }

    /// <summary>Holt die Meldungen je Mannschaft (Kacheln), Mannschaften mit den meisten Meldungen zuerst.</summary>
    public IReadOnlyList<Meldungsgruppe> Gruppen
    {
        get => gruppen;
        private set
        {
            if (SetProperty(ref gruppen, value))
            {
                OnPropertyChanged(nameof(KeineMeldungen));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob der Plan keine Meldungen hat (für die Auswahl).</summary>
    public bool KeineMeldungen => Gruppen.Count == 0 && bewertungen.Count > 0;

    /// <summary>Holt die Zusammenfassung, z. B. „12 Meldungen bei 4 von 7 Mannschaften“.</summary>
    public string Zusammenfassung
    {
        get => zusammenfassung;
        private set => SetProperty(ref zusammenfassung, value);
    }

    /// <inheritdoc/>
    protected override void Anzeigen(Planstand? stand)
    {
        bewertungen = stand?.Bewertung.Mannschaften ?? [];
        List<string> auswahl = [AlleMannschaften, .. bewertungen.Select(m => m.Name)];
        Mannschaften = auswahl;
        if (!auswahl.Contains(mannschaft, StringComparer.Ordinal))
        {
            mannschaft = AlleMannschaften;
            OnPropertyChanged(nameof(Mannschaft));
        }

        int meldungen = bewertungen.Sum(m => m.Meldungen.Count);
        int betroffen = bewertungen.Count(m => m.Meldungen.Count > 0);
        Zusammenfassung = stand is null
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"{meldungen} Meldungen bei {betroffen} von {bewertungen.Count} Mannschaften");
        ZeilenBilden();
    }

    private void ZeilenBilden()
    {
        var liste = new List<Meldungszeile>();
        foreach (Mannschaftsbewertung m in bewertungen.Where(m => m.Meldungen.Count > 0 && (mannschaft == AlleMannschaften || m.Name == mannschaft)))
        {
            liste.Add(new Meldungszeile(m.Name + ":", Ueberschrift: true));
            liste.AddRange(m.Meldungen.Select(t => new Meldungszeile(t, Ueberschrift: false)));
        }

        if (liste.Count == 0 && bewertungen.Count > 0)
        {
            liste.Add(new Meldungszeile("Keine Meldungen.", Ueberschrift: false));
        }

        Zeilen = liste;
        Gruppen = bewertungen
            .Where(m => m.Meldungen.Count > 0 && (mannschaft == AlleMannschaften || m.Name == mannschaft))
            .OrderByDescending(m => m.Meldungen.Count)
            .Select(m => new Meldungsgruppe(m.Name, m.Meldungen.Count == 1 ? "1 Meldung" : string.Create(CultureInfo.InvariantCulture, $"{m.Meldungen.Count} Meldungen"), m.Meldungen))
            .ToList();
        OnPropertyChanged(nameof(KeineMeldungen));
    }
}
