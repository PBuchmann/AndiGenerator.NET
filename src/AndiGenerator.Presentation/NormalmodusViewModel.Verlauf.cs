// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Einfach;
using AndiGenerator.Engine.Inseln;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Steuerung der Planung (Pause, Weiter, Neu starten) und der Güteverlauf für die Anzeige „Suchraum“: Treppenkurve der besten
/// Lösung, bewiesene Grenze und Zähler. Der beste Plan gilt über alle Läufe; ein Lauf nach „Weiter“ beginnt die Suche neu und
/// ersetzt ihn nur, wenn er besser wird.
/// </summary>
public sealed partial class NormalmodusViewModel
{
    private static readonly Schnellergebnis LeeresErgebnis = new([], null, null, [], [], null, null, [], [], Massstab.Standard);

    private readonly List<Gueteschritt> verlaufListe = [];
    private (IReadOnlyList<Spiel> Plan, Planguete Guete)? gesamtBester;
    private Schnellstand? gesamtStand;
    private int gezaehlteVerbesserungen;
    private bool pausiert;
    private IReadOnlyList<Gueteschritt> verlauf = [];
    private int angezeigteVerbesserungen;
    private double verlaufSekunden;
    private IAsyncRelayCommand? pauseCommand;
    private IAsyncRelayCommand? neuStartenCommand;

    /// <summary>Holt den Güteverlauf: Schönheitsfehler des besten Plans über die Zeit (Treppe).</summary>
    public IReadOnlyList<Gueteschritt> Verlauf
    {
        get => verlauf;
        private set
        {
            if (SetProperty(ref verlauf, value))
            {
                OnPropertyChanged(nameof(ZeigtVerlauf));
                OnPropertyChanged(nameof(BesterWert));
                OnPropertyChanged(nameof(GrenzeText));
                OnPropertyChanged(nameof(NochText));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob schon ein Verlauf zu zeigen ist.</summary>
    public bool ZeigtVerlauf => Verlauf.Count > 0;

    /// <summary>Holt die Zahl der Verbesserungen des besten Plans seit dem Start.</summary>
    public int Verbesserungen
    {
        get => angezeigteVerbesserungen;
        private set => SetProperty(ref angezeigteVerbesserungen, value);
    }

    /// <summary>Holt die Rechenzeit (Sekunden) am Ende der Zeitachse des Verlaufs.</summary>
    public double VerlaufSekunden
    {
        get => verlaufSekunden;
        private set => SetProperty(ref verlaufSekunden, value);
    }

    /// <summary>Holt die Schönheitsfehler des besten Plans.</summary>
    public string BesterWert => Verlauf.Count > 0 ? Verlauf[^1].Wert.ToString(Deutsch) : "–";

    /// <summary>Holt die bewiesene Grenze als Text („mindestens 38“ oder „unbekannt“).</summary>
    public string GrenzeText => Verlauf.Count > 0 && Verlauf[^1].Grenze is int g ? string.Create(Deutsch, $"mindestens {g}") : "noch unbekannt";

    /// <summary>Holt, wie viel höchstens noch zu holen ist („höchstens 129“ oder „offen“).</summary>
    public string NochText => Verlauf.Count > 0 && Verlauf[^1].Grenze is int g ? string.Create(Deutsch, $"höchstens {Math.Max(0, Verlauf[^1].Wert - g)}") : "offen";

    /// <summary>Holt einen Wert, der angibt, ob gerade gerechnet wird (nicht pausiert).</summary>
    public bool Rechnet => planung is not null;

    /// <summary>Holt einen Wert, der angibt, ob die Planung pausiert ist.</summary>
    public bool Pausiert => pausiert;

    /// <summary>Holt den Text des Befehls „Pause“ bzw. „Weiter“.</summary>
    public string PauseText => pausiert ? "Weiter" : "Pause";

    /// <summary>Holt den Befehl „Pause“, der nach dem Anhalten zu „Weiter“ wird.</summary>
    public IAsyncRelayCommand PauseCommand
    {
        get => pauseCommand ??= new AsyncRelayCommand(PauseWeiterAsync, () => Laeuft);
        private set => pauseCommand = value;
    }

    /// <summary>Holt den Befehl „Neu starten“: die Planung beginnt von vorn, der beste Plan bleibt erhalten.</summary>
    public IAsyncRelayCommand NeuStartenCommand
    {
        get => neuStartenCommand ??= new AsyncRelayCommand(NeuStartenAsync, () => Laeuft);
        private set => neuStartenCommand = value;
    }

    /// <summary>Die Schönheitsfehler (Feinschliff) eines Plans und ihre bewiesene Grenze.</summary>
    /// <param name="guete">Die Verstöße des Plans.</param>
    /// <param name="status">Was je Kriterium bewiesen ist, oder <c>null</c>.</param>
    /// <returns>Die Zahl und die Summe der bewiesenen Untergrenzen (<c>null</c>, wenn keine bekannt ist).</returns>
    internal static (int Wert, int? Grenze) Feinwerte(Planguete guete, IReadOnlyList<Kriteriumsstatus>? status)
    {
        ArgumentNullException.ThrowIfNull(guete);
        Dictionary<Kostenkriterium, Kriteriumsstatus> je = (status ?? []).ToDictionary(s => s.Kriterium);
        int wert = 0;
        int grenze = 0;
        foreach (Kostenkriterium k in Massstab.Gruppen.SelectMany(g => g.Kriterien))
        {
            wert += guete.Anzahl[(int)k];
            if (je.TryGetValue(k, out Kriteriumsstatus? st) && st.Untergrenze is int u)
            {
                grenze += Math.Min(u, guete.Anzahl[(int)k]);
            }
        }

        return (wert, grenze > 0 ? grenze : null);
    }

    /// <summary>Nimmt einen Punkt in den Verlauf auf, wenn sich Wert oder Grenze geändert haben (unter der Sperre aufrufen).</summary>
    private void VerlaufPunktMerken(Planguete guete, IReadOnlyList<Kriteriumsstatus>? status)
    {
        (int wert, int? grenze) = Feinwerte(guete, status);
        if (verlaufListe.Count > 0 && verlaufListe[^1].Grenze is int alt && (grenze is null || grenze < alt))
        {
            grenze = alt;
        }

        if (verlaufListe.Count > 0 && verlaufListe[^1].Wert == wert && verlaufListe[^1].Grenze == grenze)
        {
            return;
        }

        verlaufListe.Add(new Gueteschritt(laufuhr.Elapsed.TotalSeconds, wert, grenze));
    }

    /// <summary>Der Verlauf zum Anzeigen (unter der Sperre aufrufen).</summary>
    private VerlaufStand VerlaufStandHolen() => new([.. verlaufListe], gezaehlteVerbesserungen, laufuhr.Elapsed.TotalSeconds);

    /// <summary>Übernimmt den Verlauf in die Anzeige (Oberflächen-Thread).</summary>
    private void VerlaufAnzeigen(VerlaufStand stand)
    {
        if (stand.Punkte.Count != Verlauf.Count || (stand.Punkte.Count > 0 && stand.Punkte[^1] != Verlauf[^1]))
        {
            Verlauf = stand.Punkte;
        }

        Verbesserungen = stand.Verbesserungen;
        VerlaufSekunden = stand.Sekunden;
    }

    private void VerlaufZuruecksetzen()
    {
        lock (sperre)
        {
            verlaufListe.Clear();
            gesamtBester = null;
            gesamtStand = null;
            gezaehlteVerbesserungen = 0;
            letzterStand = null;
        }

        letzteGuete = null;
        Verlauf = [];
        Verbesserungen = 0;
        VerlaufSekunden = 0;
    }

    /// <summary>Hält die laufende Planung an und wartet auf ihr Ende.</summary>
    /// <returns>Ihr Ergebnis (leer, wenn nichts lief oder sie abgebrochen wurde).</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Die Planung läuft auf dem Threadpool und wartet nie auf die Oberfläche.")]
    private async Task<Schnellergebnis> LaufAnhaltenAsync()
    {
        Normalplanung? p;
        lock (sperre)
        {
            p = planung;
        }

        if (p is null)
        {
            return LeeresErgebnis;
        }

        p.Beenden();
        Schnellergebnis ergebnis = LeeresErgebnis;
        try
        {
            ergebnis = await p.Lauf.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Abgebrochen ohne Ergebnis: es zählt der beste Plan aus den Meldungen.
        }

        PlanungBeenden(p);
        return ergebnis;
    }

    /// <summary>Sichert den besten Plan (Ergebnis des Laufs und bester Plan der Meldungen); ein bisher besserer bleibt.</summary>
    private async Task BestenPlanSichernAsync(Schnellergebnis ergebnis)
    {
        if (hauptfenster.Sitzung is not Plansitzung s)
        {
            return;
        }

        IReadOnlyList<Spiel>? aus;
        lock (sperre)
        {
            aus = gesamtBester?.Plan;
        }

        foreach (IReadOnlyList<Spiel> plan in new[] { ergebnis.Plan, aus }.OfType<IReadOnlyList<Spiel>>().Where(x => x.Count > 0))
        {
            try
            {
                _ = Normalplanung.Uebernehmen(s, plan, steuerung.Massstab);
            }
            catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
            {
                await hauptfenster.Oberflaeche.MeldenAsync("Plan speichern", "Der Plan ließ sich nicht speichern: " + fehler.Message).ConfigureAwait(true);
            }
        }
    }

    private async Task PauseWeiterAsync()
    {
        if (pausiert)
        {
            await StartenAsync(neu: false).ConfigureAwait(true);
            return;
        }

        if (planung is null)
        {
            return;
        }

        Schnellergebnis ergebnis = await LaufAnhaltenAsync().ConfigureAwait(true);
        await BestenPlanSichernAsync(ergebnis).ConfigureAwait(true);
        pausiert = true;
        Hinweis = "Pausiert. „Weiter“ startet eine neue Suche; der beste Plan bleibt erhalten und zählt weiter.";
        Laufzeit = string.Create(Deutsch, $"pausiert nach {Dauer(laufuhr.Elapsed)}");
        LaufGeaendert();
    }

    private async Task NeuStartenAsync()
    {
        Schnellergebnis ergebnis = await LaufAnhaltenAsync().ConfigureAwait(true);
        await BestenPlanSichernAsync(ergebnis).ConfigureAwait(true);
        pausiert = false;
        await StartenAsync(neu: true).ConfigureAwait(true);
    }

    /// <summary>Der Verlauf zu einem Zeitpunkt.</summary>
    /// <param name="Punkte">Die Punkte.</param>
    /// <param name="Verbesserungen">Zahl der Verbesserungen.</param>
    /// <param name="Sekunden">Rechenzeit.</param>
    private readonly record struct VerlaufStand(IReadOnlyList<Gueteschritt> Punkte, int Verbesserungen, double Sekunden);
}
