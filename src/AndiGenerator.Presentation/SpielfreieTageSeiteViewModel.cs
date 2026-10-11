// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>
/// Seite „Spielfreie Tage“ (Original <c>TDialogPanelFreeDays</c>): alle Tage mit Heimspielwünschen, wählbar in einem
/// Monatskalender der Saison; an den gewählten Tagen sollen keine Spiele stattfinden.
/// </summary>
public sealed class SpielfreieTageSeiteViewModel : DatenSeiteViewModel
{
    private IReadOnlyList<DateOnly> tage = [];
    private IReadOnlyList<AuswahlZeile> zeilen = [];
    private IReadOnlyList<Kalendermonat> monate = [];

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    public SpielfreieTageSeiteViewModel(Datenbearbeitung bearbeitung)
        : base(bearbeitung, "Spielfreie Tage")
    {
        Laden();
    }

    /// <summary>Holt die Wochentage für die Kopfzeile eines Monats.</summary>
    public static IReadOnlyList<string> Wochentage => WunschterminDetailViewModel.Wochentage;

    /// <summary>Holt die möglichen Tage mit Häkchen.</summary>
    public IReadOnlyList<AuswahlZeile> Zeilen
    {
        get => zeilen;
        private set => SetProperty(ref zeilen, value);
    }

    /// <summary>Holt die Monate der Saison als Kalender; wählbar sind die Tage mit Heimspielwünschen.</summary>
    public IReadOnlyList<Kalendermonat> Monate
    {
        get => monate;
        private set
        {
            if (SetProperty(ref monate, value))
            {
                OnPropertyChanged(nameof(KeineTage));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es keine wählbaren Tage gibt.</summary>
    public bool KeineTage => Monate.Count == 0;

    /// <inheritdoc/>
    public override string Marke => Zeilen.Count(z => z.Gewaehlt) is int n and > 0 ? n.ToString(CultureInfo.InvariantCulture) : string.Empty;

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
        Monate = Kalender();
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

    /// <summary>Warum ein Tag nicht wählbar ist (für den Tooltip).</summary>
    private static string Grund(DateOnly tag, Ligadaten liga, bool eingetragen)
    {
        if (tag < liga.Beginn || tag > liga.Ende)
        {
            return "außerhalb der Saison";
        }

        return eingetragen
            ? "als spielfreier Tag eingetragen (z. B. aus click-TT); keine Mannschaft hat hier einen Heimspielwunsch"
            : "nicht gesperrt: keine Mannschaft hat an diesem Tag einen Heimspielwunsch, deshalb wird hier ohnehin nicht gespielt";
    }

    /// <summary>Die Monate vom ersten bis zum letzten möglichen Tag, je Monat ab dem Montag der ersten Woche.</summary>
    private List<Kalendermonat> Kalender()
    {
        if (tage.Count == 0)
        {
            return [];
        }

        var deutsch = CultureInfo.GetCultureInfo("de-DE");
        HashSet<DateOnly> eingetragen = [.. Bearbeitung.EingetrageneSpielfreieTage()];
        Ligadaten liga = Bearbeitung.Ligadaten;
        Dictionary<DateOnly, AuswahlZeile> waehlbar = tage.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => Zeilen[x.i]);
        var liste = new List<Kalendermonat>();
        for (var erster = new DateOnly(tage.Min().Year, tage.Min().Month, 1); erster <= tage.Max(); erster = erster.AddMonths(1))
        {
            int leer = ((int)erster.DayOfWeek + 6) % 7;
            var felder = Enumerable.Range(0, leer).Select(_ => new Kalendertag(string.Empty, null, false, string.Empty)).ToList();
            for (DateOnly tag = erster; tag.Month == erster.Month; tag = tag.AddDays(1))
            {
                AuswahlZeile? zeile = waehlbar.GetValueOrDefault(tag);
                string datum = tag.ToString("ddd, dd.MM.yyyy", deutsch);
                string hinweis = datum + " – " + (zeile is null ? Grund(tag, liga, eingetragen.Contains(tag)) : "Klick: spielfrei an/aus");
                bool wochenende = tag.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                felder.Add(new Kalendertag(tag.Day.ToString(CultureInfo.InvariantCulture), zeile, wochenende, hinweis, zeile is null && eingetragen.Contains(tag)));
            }

            liste.Add(new Kalendermonat(erster.ToString("MMMM yyyy", deutsch), felder));
        }

        return liste;
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
