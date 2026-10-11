// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Wunschtermine einer Mannschaft (Original <c>TDialogPanelHomeDaysDetail</c>): Wochenübersicht mit einem Kärtchen je Tag
/// (Heimspiel, Ausweichtermin, Sperrtermin); Klick oder Kontextmenü öffnet den Dialog „Wunschtermin“, Ziehen verschiebt
/// einen Termin auf einen anderen Tag.
/// </summary>
public sealed class WunschterminDetailViewModel : DatenSeiteViewModel
{
    private readonly string mannschaft;
    private readonly IOberflaeche oberflaeche;
    private IReadOnlyList<WunschterminZeile> wochen = [];

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="oberflaeche">Dienste der Oberfläche für den Dialog.</param>
    public WunschterminDetailViewModel(Datenbearbeitung bearbeitung, string mannschaft, IOberflaeche oberflaeche)
        : base(bearbeitung, "Wunschtermine")
    {
        ArgumentNullException.ThrowIfNull(oberflaeche);
        this.mannschaft = mannschaft;
        this.oberflaeche = oberflaeche;
        BearbeitenCommand = new AsyncRelayCommand<WunschterminZelle>(BearbeitenAsync);
        LoeschenCommand = new RelayCommand<WunschterminZelle>(Loeschen, z => z?.HatTermin == true);
        Laden();
    }

    /// <summary>Holt die Wochentage für die Kopfzeile.</summary>
    public static IReadOnlyList<string> Wochentage { get; } = ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];

    /// <summary>Holt die Wochen.</summary>
    public IReadOnlyList<WunschterminZeile> Wochen
    {
        get => wochen;
        private set => SetProperty(ref wochen, value);
    }

    /// <summary>Holt den Befehl „Bearbeiten…“ für einen Tag (auch per Doppelklick).</summary>
    public IAsyncRelayCommand<WunschterminZelle> BearbeitenCommand { get; }

    /// <summary>Holt den Befehl „Löschen“ für einen Tag.</summary>
    public IRelayCommand<WunschterminZelle> LoeschenCommand { get; }

    /// <inheritdoc/>
    public override bool IstStandard => Bearbeitung.WunschtermineSindStandard(mannschaft, Werte());

    /// <summary>
    /// Verschiebt den Termin eines Tages auf einen anderen (Ziehen im Wochenraster). Steht dort schon ein Termin, tauschen
    /// die beiden; mit <paramref name="kopieren"/> bleibt der Termin am alten Tag stehen und ersetzt den am neuen.
    /// </summary>
    /// <param name="von">Der Tag, dessen Termin gezogen wird.</param>
    /// <param name="nach">Der Tag, auf den er gelegt wird.</param>
    /// <param name="kopieren"><c>true</c> = kopieren statt verschieben (Strg).</param>
    public static void Verschieben(WunschterminZelle von, WunschterminZelle nach, bool kopieren)
    {
        ArgumentNullException.ThrowIfNull(von);
        ArgumentNullException.ThrowIfNull(nach);
        if (ReferenceEquals(von, nach) || !von.HatTermin)
        {
            return;
        }

        string bisher = nach.Text;
        nach.Text = von.Text;
        if (!kopieren)
        {
            von.Text = bisher;
        }
    }

    /// <inheritdoc/>
    public override void Laden() => Anzeigen(Bearbeitung.Wunschtermine(mannschaft));

    /// <inheritdoc/>
    public override void Speichern() => Bearbeitung.WunschtermineSpeichern(mannschaft, Werte());

    /// <inheritdoc/>
    public override string? Pruefen() => Datenbearbeitung.WunschterminePruefen(Werte());

    /// <inheritdoc/>
    public override void Standard()
    {
        // Wie im Original: die Termine des click-TT-Stands anzeigen; gespeichert wird beim Verlassen der Seite.
        if (Bearbeitung.WunschtermineStandard(mannschaft) is IReadOnlyList<Wunschterminwoche> standard)
        {
            Anzeigen(standard);
            AenderungMelden();
        }
    }

    private static void Loeschen(WunschterminZelle? zelle)
    {
        if (zelle is not null)
        {
            zelle.Text = string.Empty;
        }
    }

    /// <summary>
    /// Die übliche Uhrzeit der Mannschaft: die häufigste ihrer Heimspielzeiten am selben Wochentag, sonst an allen Tagen,
    /// sonst 18:00.
    /// </summary>
    private TimeSpan UeblicheUhrzeit(DayOfWeek wochentag)
    {
        List<(DayOfWeek Tag, TimeSpan Zeit)> zeiten = [.. Wochen.SelectMany(w => w.Tage)
            .Where(z => z.IstHeimspiel || z.IstAusweich)
            .Select(z => (z.Tag.DayOfWeek, TimeSpan.Parse(z.Hauptzeile, System.Globalization.CultureInfo.InvariantCulture)))];
        List<(DayOfWeek Tag, TimeSpan Zeit)> amTag = [.. zeiten.Where(z => z.Tag == wochentag)];
        List<(DayOfWeek Tag, TimeSpan Zeit)> basis = amTag.Count > 0 ? amTag : zeiten;
        return basis.Count == 0 ? new TimeSpan(18, 0, 0) : basis.GroupBy(z => z.Zeit).OrderByDescending(g => g.Count()).First().Key;
    }

    private List<Wunschterminwoche> Werte() => Wochen.Select(w => w.Wert()).ToList();

    private void Anzeigen(IReadOnlyList<Wunschterminwoche> daten) =>
        Wochen = daten.Select(w => new WunschterminZeile(w, ZelleGeaendert)).ToList();

    private void ZelleGeaendert()
    {
        LoeschenCommand.NotifyCanExecuteChanged();
        AenderungMelden();
    }

    private async Task BearbeitenAsync(WunschterminZelle? zelle)
    {
        if (zelle is null)
        {
            return;
        }

        var dialog = new WunschterminDialogViewModel(zelle.Tag, zelle.Text);
        if (!zelle.HatTermin)
        {
            // Ein leerer Tag wird angeklickt, um einen Termin einzutragen: gleich als Heimspiel zur üblichen Uhrzeit vorschlagen.
            dialog.Art = 1;
            dialog.Uhrzeit = UeblicheUhrzeit(zelle.Tag.DayOfWeek);
        }

        if (await oberflaeche.WunschterminBearbeitenAsync(dialog))
        {
            zelle.Text = dialog.Ergebnis;
        }
    }
}
