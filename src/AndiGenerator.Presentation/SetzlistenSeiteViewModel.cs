// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Globalization;
using AndiGenerator.Application;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Seite „Setzliste“ (Original <c>TDialogPanelRanking</c>): Setzliste aktivieren und die erwartete Reihenfolge mit
/// „Nach oben“/„Nach unten“ festlegen. Standard ist wie im Original „nicht aktiv“; die Reihenfolge bleibt dabei erhalten.
/// </summary>
public sealed class SetzlistenSeiteViewModel : DatenSeiteViewModel
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private IReadOnlyDictionary<string, Mannschaftsstaerke> staerken = new Dictionary<string, Mannschaftsstaerke>();
    private bool aktiv;
    private int auswahl = -1;
    private int gezogen = -1;
    private int ablage = -1;
    private Setzlistenzeile? schwebend;

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    public SetzlistenSeiteViewModel(Datenbearbeitung bearbeitung)
        : base(bearbeitung, "Setzliste")
    {
        HochCommand = new RelayCommand(() => Verschieben(-1), () => Auswahl > 0);
        RunterCommand = new RelayCommand(() => Verschieben(1), () => Auswahl >= 0 && Auswahl < Reihenfolge.Count - 1);
        NachStaerkeCommand = new RelayCommand(NachStaerkeOrdnen, () => HatStaerken);
        Reihenfolge.CollectionChanged += (_, _) => ZeilenAufbauen();
        Laden();
    }

    /// <summary>Holt den Erklärungstext des Originals.</summary>
    public static string Erklaerung =>
        "Mit der Setzliste können Sie die erwartete Tabelle am Ende der Saison vorgeben.\n"
        + "Das Ziel ist, dass am Ende der Saison die stärksten und schwächsten Mannschaften gegeneinander spielen sollen. "
        + "Damit soll verhindert werden, dass in den wichtigen Spielen um den Auf- und Abstieg Mannschaften beteiligt sind, "
        + "bei denen es um nichts mehr geht.\n"
        + "Mit der Setzliste soll die Spannung bis zum Schluss erhalten bleiben und, falls es so etwas überhaupt geben sollte, "
        + "Mauscheleien verhindert werden.";

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Setzliste aktiv ist.</summary>
    public bool Aktiv
    {
        get => aktiv;
        set
        {
            if (SetProperty(ref aktiv, value))
            {
                AenderungMelden();
            }
        }
    }

    /// <summary>Holt die erwartete Reihenfolge (Platz 1 zuerst).</summary>
    public ObservableCollection<string> Reihenfolge { get; } = [];

    /// <summary>
    /// Holt die Zeilen der Anzeige mit Platznummer; beim Ziehen schon in der Reihenfolge, die beim Ablegen entstünde, mit
    /// einer Lücke an der Ablagestelle.
    /// </summary>
    public ObservableCollection<Setzlistenzeile> Zeilen { get; } = [];

    /// <summary>Holt beim Ziehen die Zeile, die am Mauszeiger hängt; sonst <c>null</c>.</summary>
    public Setzlistenzeile? Schwebend
    {
        get => schwebend;
        private set => SetProperty(ref schwebend, value);
    }

    /// <summary>Holt oder setzt den Index der gewählten Mannschaft.</summary>
    public int Auswahl
    {
        get => auswahl;
        set
        {
            if (SetProperty(ref auswahl, value))
            {
                HochCommand.NotifyCanExecuteChanged();
                RunterCommand.NotifyCanExecuteChanged();
                ZeilenAufbauen();
            }
        }
    }

    /// <summary>Holt den Befehl „Nach oben“.</summary>
    public IRelayCommand HochCommand { get; }

    /// <summary>Holt den Befehl „Nach unten“.</summary>
    public IRelayCommand RunterCommand { get; }

    /// <summary>Holt den Befehl „Nach TTR ordnen“: stärkste Mannschaft zuerst, Mannschaften ohne Wert am Ende.</summary>
    public IRelayCommand NachStaerkeCommand { get; }

    /// <summary>Holt einen Wert, der angibt, ob TTR-Werte aus einer Mannschaftsmeldung gespeichert sind.</summary>
    public bool HatStaerken => staerken.Count > 0;

    /// <summary>Holt die Erklärung der TTR-Werte mit Herkunft oder einen leeren Text.</summary>
    public string StaerkenHinweis
    {
        get
        {
            if (!HatStaerken)
            {
                return string.Empty;
            }

            string vermerk = Bearbeitung.MannschaftsmeldungVermerk;
            string herkunft = vermerk.Length > 0 ? " (" + vermerk + ")" : string.Empty;
            return "Ø = Schnitt der TTR-Werte der Stammspieler aus der Mannschaftsmeldung" + herkunft + ".";
        }
    }

    /// <inheritdoc/>
    public override string Marke => Aktiv ? "an" : "aus";

    /// <inheritdoc/>
    public override bool IstStandard => !Aktiv;

    /// <inheritdoc/>
    public override void Laden()
    {
        Setzlistendaten daten = Bearbeitung.Setzliste();
        staerken = Bearbeitung.Mannschaftsstaerken();
        OnPropertyChanged(nameof(HatStaerken));
        OnPropertyChanged(nameof(StaerkenHinweis));
        NachStaerkeCommand.NotifyCanExecuteChanged();
        aktiv = daten.Aktiv;
        OnPropertyChanged(nameof(Aktiv));
        Reihenfolge.Clear();
        foreach (string name in daten.Reihenfolge)
        {
            Reihenfolge.Add(name);
        }

        Auswahl = Reihenfolge.Count > 0 ? 0 : -1;
    }

    /// <inheritdoc/>
    public override void Speichern() => Bearbeitung.SetzlisteSpeichern(new Setzlistendaten(Aktiv, Reihenfolge.ToList()));

    /// <inheritdoc/>
    public override void Standard() => Aktiv = false;

    /// <summary>
    /// Zieht eine Mannschaft auf eine andere (Original <c>ListRankingDragDrop</c>): Sie landet vor der Zielmannschaft.
    /// </summary>
    /// <param name="von">Index der gezogenen Mannschaft.</param>
    /// <param name="ziel">Index der Mannschaft, auf die sie gezogen wurde.</param>
    public void Ziehen(int von, int ziel)
    {
        if (von < 0 || ziel < 0 || von >= Reihenfolge.Count || ziel >= Reihenfolge.Count || von == ziel)
        {
            return;
        }

        int nach = ziel > von ? ziel - 1 : ziel;
        Reihenfolge.Move(von, nach);
        Auswahl = nach;
    }

    /// <summary>Beginnt das Ziehen einer Mannschaft (am Griff ihrer Zeile).</summary>
    /// <param name="index">Index der Mannschaft.</param>
    /// <returns><c>true</c>, wenn gezogen wird.</returns>
    public bool ZiehenBeginnen(int index)
    {
        if (index < 0 || index >= Reihenfolge.Count)
        {
            return false;
        }

        gezogen = index;
        ablage = index;
        ZeilenAufbauen();
        return true;
    }

    /// <summary>
    /// Die gezogene Mannschaft ist über der Zeile <paramref name="ziel"/> (Index in der angezeigten Vorschau): über deren
    /// oberer Hälfte kommt sie davor, sonst dahinter. Die Anzeige zeigt die neue Reihenfolge schon mit der Lücke.
    /// </summary>
    /// <param name="ziel">Index der Zeile unter dem Zeiger in <see cref="Zeilen"/>.</param>
    /// <param name="davor"><c>true</c> = obere Hälfte.</param>
    public void ZiehenUeber(int ziel, bool davor)
    {
        if (gezogen < 0 || ziel < 0 || ziel >= Zeilen.Count || ziel == ablage)
        {
            return;
        }

        // In der Vorschau ohne die gezogene Mannschaft: vor bzw. hinter die Zeile unter dem Zeiger.
        int ohne = ziel > ablage ? ziel - 1 : ziel;
        int neu = davor ? ohne : ohne + 1;
        if (neu != ablage)
        {
            ablage = neu;
            ZeilenAufbauen();
        }
    }

    /// <summary>Beendet das Ziehen: legt die Mannschaft an der Ablagestelle ab oder bricht ab (Escape).</summary>
    /// <param name="uebernehmen"><c>true</c> = ablegen.</param>
    public void ZiehenBeenden(bool uebernehmen)
    {
        int von = gezogen;
        int nach = ablage;
        gezogen = -1;
        ablage = -1;
        if (uebernehmen && von >= 0 && nach >= 0 && von != nach)
        {
            Reihenfolge.Move(von, nach);
            Auswahl = nach;
        }

        ZeilenAufbauen();
    }

    /// <summary>Wählt eine Mannschaft (Klick auf ihre Zeile).</summary>
    /// <param name="index">Index in <see cref="Zeilen"/>.</param>
    public void Waehlen(int index)
    {
        if (gezogen < 0 && index >= 0 && index < Reihenfolge.Count)
        {
            Auswahl = index;
        }
    }

    /// <summary>Verschiebt die gewählte Mannschaft (Original <c>ButtonUpClick</c>/<c>ButtonDownClick</c>).</summary>
    /// <param name="richtung">-1 nach oben, 1 nach unten.</param>
    internal void Verschieben(int richtung)
    {
        int von = Auswahl;
        int nach = von + richtung;
        if (von < 0 || nach < 0 || nach >= Reihenfolge.Count)
        {
            return;
        }

        Reihenfolge.Move(von, nach);
        Auswahl = nach;
    }

    /// <summary>Ordnet nach dem TTR-Schnitt, die stärkste Mannschaft zuerst; ohne Wert bleibt die bisherige Folge am Ende.</summary>
    private void NachStaerkeOrdnen()
    {
        List<string> neu =
        [
            .. Reihenfolge.Where(staerken.ContainsKey).OrderByDescending(n => staerken[n].Schnitt),
            .. Reihenfolge.Where(n => !staerken.ContainsKey(n)),
        ];
        for (int ziel = 0; ziel < neu.Count; ziel++)
        {
            int von = Reihenfolge.IndexOf(neu[ziel]);
            if (von != ziel)
            {
                Reihenfolge.Move(von, ziel);
            }
        }

        Auswahl = Reihenfolge.Count > 0 ? 0 : -1;
        AenderungMelden();
    }

    /// <summary>Stärke einer Mannschaft als Text, z. B. „Ø 1.623“ (TTR-Schnitt der Stammspieler), sonst leer.</summary>
    private string StaerkeText(string name) =>
        staerken.TryGetValue(name, out Mannschaftsstaerke? s) ? "Ø " + s.Schnitt.ToString("N0", Deutsch) : string.Empty;

    /// <summary>Baut die angezeigten Zeilen auf; beim Ziehen in der Reihenfolge nach dem Ablegen, mit Lücke.</summary>
    private void ZeilenAufbauen()
    {
        List<string> namen = [.. Reihenfolge];
        string? gezogenerName = null;
        if (gezogen >= 0 && gezogen < namen.Count)
        {
            gezogenerName = namen[gezogen];
            namen.RemoveAt(gezogen);
            namen.Insert(Math.Clamp(ablage, 0, namen.Count), gezogenerName);
        }

        Zeilen.Clear();
        for (int i = 0; i < namen.Count; i++)
        {
            bool luecke = gezogenerName is not null && i == ablage;
            Zeilen.Add(new Setzlistenzeile(i + 1, namen[i], luecke, gezogenerName is null && i == Auswahl) { Staerke = StaerkeText(namen[i]) });
        }

        Schwebend = gezogenerName is null ? null : new Setzlistenzeile(ablage + 1, gezogenerName, false, false) { Staerke = StaerkeText(gezogenerName) };
    }
}
