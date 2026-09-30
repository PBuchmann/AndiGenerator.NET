// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AndiGenerator.Presentation;

/// <summary>
/// Einstellungen der Staffel (Original <c>TFormOptions</c>, nicht modal): Rundenplanung, Doppelrunde mit Viertelrunden,
/// Freitag bei der 60-km-Regel und alle Gewichtungen. Jede Änderung wird wie im Original sofort gespeichert und von der
/// laufenden Generierung übernommen. Als Dokument im Andock-Layout kann der Dialog neben den Ansichten offen bleiben.
/// </summary>
public sealed class OptionenAnsichtViewModel : Document
{
    private readonly HauptfensterViewModel hauptfenster;
    private bool laden;
    private bool freitag;
    private int rundenplanung;
    private bool doppelrunde;
    private bool automatischeMitte;
    private DateTime? mitte1;
    private DateTime? mitte2;
    private IReadOnlyList<GewichtungsEintrag> planGewichte = [];
    private IReadOnlyList<GewichtungsEintrag> kostenartGewichte = [];
    private IReadOnlyList<GewichtungsEintrag> mannschaftsGewichte = [];

    /// <summary>Initialisiert die Einstellungen.</summary>
    /// <param name="hauptfenster">Hauptfenster.</param>
    public OptionenAnsichtViewModel(HauptfensterViewModel hauptfenster)
    {
        ArgumentNullException.ThrowIfNull(hauptfenster);
        this.hauptfenster = hauptfenster;
        Id = "Einstellungen";
        Title = "Einstellungen";
        CanClose = true;
        CanFloat = true;
        StandardCommand = new AsyncRelayCommand(StandardAsync);
        Laden();
    }

    /// <summary>Holt die Namen der Rundenplanungen (Reihenfolge wie <see cref="Domain.Optionen.Rundenplanung"/>).</summary>
    public static IReadOnlyList<string> Rundenplanungen { get; } =
    [
        "Vor- und Rückrunde planen", "Halbrunde planen", "Nur Vorrunde planen", "Nur Rückrunde planen",
        "Coronarunde planen (nur die nicht gespielten Spiele der Rückrunde)",
    ];

    /// <summary>Holt oder setzt einen Wert, der angibt, ob Freitagsspiele bei der 60-km-Regel zulässig sind.</summary>
    public bool FreitagZaehltZumWochenende
    {
        get => freitag;
        set
        {
            if (SetProperty(ref freitag, value))
            {
                Aendern(o => o with { FreitagZaehltZumWochenende = value });
            }
        }
    }

    /// <summary>Holt oder setzt die Rundenplanung als Index in <see cref="Rundenplanungen"/>.</summary>
    public int RundenplanungIndex
    {
        get => rundenplanung;
        set
        {
            if (value >= 0 && SetProperty(ref rundenplanung, value))
            {
                Aendern(o => o with { Rundenplanung = (Rundenplanung)value });
            }
        }
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob eine Doppelrunde (vier Viertelrunden) geplant wird.</summary>
    public bool Doppelrunde
    {
        get => doppelrunde;
        set
        {
            if (SetProperty(ref doppelrunde, value))
            {
                OnPropertyChanged(nameof(MittenBearbeitbar));
                Aendern(o => o with { Doppelrunde = value });
            }
        }
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Enddaten der Viertelrunden automatisch ermittelt werden.</summary>
    public bool AutomatischeMitte
    {
        get => automatischeMitte;
        set
        {
            if (!SetProperty(ref automatischeMitte, value))
            {
                return;
            }

            OnPropertyChanged(nameof(MittenBearbeitbar));

            // Wie im Original: Beim Umschalten stehen die automatisch ermittelten Daten als Vorschlag in den Feldern.
            Aendern(o => o with
            {
                AutomatischeMitte = value,
                Mitte1 = mitte1 is DateTime a ? DateOnly.FromDateTime(a) : o.Mitte1,
                Mitte2 = mitte2 is DateTime b ? DateOnly.FromDateTime(b) : o.Mitte2,
            });
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob die Enddaten der Viertelrunden von Hand eingegeben werden können.</summary>
    public bool MittenBearbeitbar => Doppelrunde && !AutomatischeMitte;

    /// <summary>Holt oder setzt das Enddatum der 1. Viertelrunde.</summary>
    public DateTime? Mitte1
    {
        get => mitte1;
        set
        {
            if (SetProperty(ref mitte1, value) && value is DateTime datum)
            {
                Aendern(o => o with { Mitte1 = DateOnly.FromDateTime(datum) });
            }
        }
    }

    /// <summary>Holt oder setzt das Enddatum der 3. Viertelrunde.</summary>
    public DateTime? Mitte2
    {
        get => mitte2;
        set
        {
            if (SetProperty(ref mitte2, value) && value is DateTime datum)
            {
                Aendern(o => o with { Mitte2 = DateOnly.FromDateTime(datum) });
            }
        }
    }

    /// <summary>Holt die Gewichtungen der Plan-Kostenarten.</summary>
    public IReadOnlyList<GewichtungsEintrag> PlanGewichte
    {
        get => planGewichte;
        private set => SetProperty(ref planGewichte, value);
    }

    /// <summary>Holt die Gewichtungen der Kostenarten für alle Mannschaften.</summary>
    public IReadOnlyList<GewichtungsEintrag> KostenartGewichte
    {
        get => kostenartGewichte;
        private set => SetProperty(ref kostenartGewichte, value);
    }

    /// <summary>Holt die Gewichtungen aller Kosten je Mannschaft.</summary>
    public IReadOnlyList<GewichtungsEintrag> MannschaftsGewichte
    {
        get => mannschaftsGewichte;
        private set => SetProperty(ref mannschaftsGewichte, value);
    }

    /// <summary>Holt den Befehl „Standard wiederherstellen“.</summary>
    public IAsyncRelayCommand StandardCommand { get; }

    /// <summary>Liest die Werte aus den aktuellen Optionen der Staffel (nach Öffnen oder Änderungen von außen).</summary>
    internal void Laden()
    {
        Berechnungsoptionen? o = hauptfenster.Optionen;
        Staffel? staffel = hauptfenster.Staffel;
        if (o is null || staffel is null)
        {
            return;
        }

        laden = true;
        try
        {
            FreitagZaehltZumWochenende = o.FreitagZaehltZumWochenende;
            RundenplanungIndex = (int)o.Rundenplanung;
            Doppelrunde = o.Doppelrunde;
            AutomatischeMitte = o.AutomatischeMitte;
            (DateOnly auto1, DateOnly auto2) = hauptfenster.AutomatischeRundenmitten() ?? (default, default);
            DateOnly? m1 = o.AutomatischeMitte ? auto1 : o.Mitte1 ?? auto1;
            DateOnly? m2 = o.AutomatischeMitte ? auto2 : o.Mitte2 ?? auto2;
            Mitte1 = m1?.ToDateTime(TimeOnly.MinValue);
            Mitte2 = m2?.ToDateTime(TimeOnly.MinValue);
            PlanGewichte = Enum.GetValues<PlanKostenart>().Select(a => Eintrag(new PlanGewichtungsziel(a), o)).ToList();
            KostenartGewichte = Enum.GetValues<MannschaftsKostenart>().Select(a => Eintrag(new KostenartGewichtungsziel(a), o)).ToList();
            MannschaftsGewichte = staffel.Mannschaften.Select(m => Eintrag(new MannschaftsGewichtungsziel(m.Id, m.Name), o)).ToList();
        }
        finally
        {
            laden = false;
        }
    }

    private GewichtungsEintrag Eintrag(Gewichtungsziel ziel, Berechnungsoptionen o) =>
        new(ziel, ziel.Lesen(o), (z, wert) => Aendern(opt => z.Setzen(opt, wert)));

    private void Aendern(Func<Berechnungsoptionen, Berechnungsoptionen> aenderung)
    {
        if (laden || hauptfenster.Optionen is not Berechnungsoptionen bisher)
        {
            return;
        }

        if (hauptfenster.OptionenAnwenden(aenderung(bisher)))
        {
            // Automatisch ermittelte Daten hängen z. B. von der Rundenplanung ab.
            Laden();
        }
    }

    private async Task StandardAsync()
    {
        if (hauptfenster.Optionen is null
            || !await hauptfenster.Oberflaeche.FragenAsync("Standard wiederherstellen", "Alle Einstellungen und Gewichtungen dieser Staffel auf den Standard zurücksetzen?"))
        {
            return;
        }

        if (hauptfenster.OptionenAnwenden(Berechnungsoptionen.Standard))
        {
            Laden();
        }
    }
}
