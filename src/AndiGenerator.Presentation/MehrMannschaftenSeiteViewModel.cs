// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>
/// Seite mit Mannschaftsliste links und den Daten der gewählten Mannschaft rechts (Original <c>TDialogPanelMultiTeams</c>).
/// Wie im Original merkt sich der Dialog die zuletzt gewählte Mannschaft über alle solchen Seiten hinweg; ein Wechsel
/// der Mannschaft ist nur mit gültigen Eingaben möglich, „Standard wiederherstellen“ wirkt auf die gewählte Mannschaft.
/// </summary>
public abstract class MehrMannschaftenSeiteViewModel : DatenSeiteViewModel
{
    private static string? letzteMannschaft;

    private IReadOnlyList<string> namen = [];
    private string? mannschaft;
    private DatenSeiteViewModel? detail;
    private string meldung = string.Empty;

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="titel">Seitentitel (Original <c>getCaption</c>).</param>
    /// <param name="kopftext">Erklärung über der Liste (Original <c>getHeaderText</c>); leer = keine.</param>
    protected MehrMannschaftenSeiteViewModel(Datenbearbeitung bearbeitung, string titel, string kopftext)
        : base(bearbeitung, titel)
    {
        Kopftext = kopftext;
    }

    /// <summary>Holt die Erklärung über der Liste.</summary>
    public string Kopftext { get; }

    /// <summary>Holt einen Wert, der angibt, ob es eine Erklärung gibt.</summary>
    public bool HatKopftext => Kopftext.Length > 0;

    /// <summary>Holt die Mannschaftsnamen.</summary>
    public IReadOnlyList<string> Namen
    {
        get => namen;
        private set => SetProperty(ref namen, value);
    }

    /// <summary>Holt oder setzt die gewählte Mannschaft; der Wechsel unterbleibt bei ungültigen Eingaben.</summary>
    public string? Mannschaft
    {
        get => mannschaft;
        set
        {
            if (value is null || value == mannschaft)
            {
                return;
            }

            if (detail?.Pruefen() is string fehler)
            {
                Meldung = fehler;
                OnPropertyChanged(nameof(Mannschaft));
                return;
            }

            detail?.Speichern();
            mannschaft = value;
            Anzeigen(value);
        }
    }

    /// <summary>Holt die Daten der gewählten Mannschaft.</summary>
    public DatenSeiteViewModel? Detail
    {
        get => detail;
        private set => SetProperty(ref detail, value);
    }

    /// <summary>Holt die Überschrift der Detailansicht, z. B. „TTC Beispiel / Spiellokale“.</summary>
    public string Ueberschrift => mannschaft is null ? string.Empty : $"{mannschaft} / {Titel}";

    /// <summary>Holt die Meldung der letzten Prüfung.</summary>
    public string Meldung
    {
        get => meldung;
        private set => SetProperty(ref meldung, value);
    }

    /// <inheritdoc/>
    public override bool IstStandard => Detail?.IstStandard ?? true;

    /// <inheritdoc/>
    public override void Laden()
    {
        Namen = Bearbeitung.Mannschaftsnamen();
        string? wahl = letzteMannschaft is string letzte && Namen.Contains(letzte) ? letzte : Namen.FirstOrDefault();
        mannschaft = null;
        detail = null;
        if (wahl is not null)
        {
            Anzeigen(wahl);
        }
    }

    /// <inheritdoc/>
    public override void Speichern() => Detail?.Speichern();

    /// <inheritdoc/>
    public override void Standard() => Detail?.Standard();

    /// <inheritdoc/>
    public override string? Pruefen() => Detail?.Pruefen();

    /// <summary>Erzeugt die Detailansicht einer Mannschaft (Original <c>getDialogClass</c>).</summary>
    /// <param name="name">Mannschaftsname.</param>
    /// <returns>Die Detailansicht, bereits geladen.</returns>
    protected abstract DatenSeiteViewModel DetailErzeugen(string name);

    private static void Merken(string name) => letzteMannschaft = name;

    private void Anzeigen(string name)
    {
        Merken(name);
        if (detail is not null)
        {
            detail.Geaendert -= DetailGeaendert;
        }

        DatenSeiteViewModel neu = DetailErzeugen(name);
        neu.Geaendert += DetailGeaendert;
        mannschaft = name;
        Meldung = string.Empty;
        Detail = neu;
        OnPropertyChanged(nameof(Mannschaft));
        OnPropertyChanged(nameof(Ueberschrift));
        AenderungMelden();
    }

    private void DetailGeaendert(object? sender, EventArgs e) => AenderungMelden();
}
