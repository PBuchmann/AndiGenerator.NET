// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Die Begleitleiste „Schritt für Schritt“ (Form C, Peter 24.10.2026): rechts die Schritte einer Führung, der aktuelle mit
/// Text, „Zeigen“ (öffnet bei Bedarf die Ansicht und hebt das Element hervor) und „Weiter“. Schritte mit Bedingung hakt die
/// Führung selbst ab, sobald der Zustand eintritt (z. B. „Generierung starten“), und zeigt dann den nächsten.
/// </summary>
public sealed class FuehrungViewModel : ObservableObject
{
    private readonly HauptfensterViewModel hauptfenster;
    private Fuehrungszeile aktuell;

    /// <summary>Der Schritt wurde von Hand gewählt (Zurück, Klick): Er bleibt stehen, auch wenn seine Bedingung schon gilt.</summary>
    private bool vonHand;

    /// <summary>Initialisiert die Führung.</summary>
    /// <param name="hauptfenster">Das Hauptfenster.</param>
    /// <param name="art">Die Führung.</param>
    /// <param name="start">Der Schritt (ab 0), mit dem sie beginnt; die davor gelten als erledigt.</param>
    internal FuehrungViewModel(HauptfensterViewModel hauptfenster, Fuehrungsart art, int start)
    {
        this.hauptfenster = hauptfenster;
        Art = art;
        Schritte = Fuehrungen.Schritte(art).Select((s, i) => new Fuehrungszeile(i + 1, s, Waehlen)).ToList();
        start = Math.Clamp(start, 0, Schritte.Count - 1);
        foreach (Fuehrungszeile zeile in Schritte.Take(start))
        {
            zeile.IstErledigt = true;
        }

        aktuell = Schritte[start];
        aktuell.IstAktuell = true;
        WeiterCommand = new RelayCommand(Weiter);
        ZurueckCommand = new RelayCommand(Zurueck, () => aktuell.Nummer > 1);
        ZeigenCommand = new RelayCommand(Zeigen, () => aktuell.Schritt.Ziel is not null);
        SchliessenCommand = new RelayCommand(hauptfenster.FuehrungBeenden);
        AnleitungCommand = hauptfenster.AnleitungCommand;
    }

    /// <summary>Holt die Art der Führung.</summary>
    public Fuehrungsart Art { get; }

    /// <summary>Holt den Titel, z. B. „Schritt für Schritt: Automodus“.</summary>
    public string Titel => Fuehrungen.Titel(Art);

    /// <summary>Holt die Schritte.</summary>
    public IReadOnlyList<Fuehrungszeile> Schritte { get; }

    /// <summary>Holt den aktuellen Schritt.</summary>
    public Fuehrungszeile Aktuell => aktuell;

    /// <summary>Holt den Fortschritt, z. B. „Schritt 3 von 10“.</summary>
    public string FortschrittText => string.Create(CultureInfo.InvariantCulture, $"Schritt {aktuell.Nummer} von {Schritte.Count}");

    /// <summary>Holt den Text des Knopfs „Weiter“ (beim letzten Schritt „Fertig“).</summary>
    public string WeiterText => aktuell.Nummer == Schritte.Count ? "Fertig" : "Weiter ›";

    /// <summary>Holt den Befehl „Weiter“: hakt den Schritt ab und zeigt den nächsten (beim letzten: schließt).</summary>
    public IRelayCommand WeiterCommand { get; }

    /// <summary>Holt den Befehl „Zurück“.</summary>
    public IRelayCommand ZurueckCommand { get; }

    /// <summary>Holt den Befehl „Zeigen“: öffnet bei Bedarf die Ansicht und hebt das Element hervor.</summary>
    public IRelayCommand ZeigenCommand { get; }

    /// <summary>Holt den Befehl „Schließen“.</summary>
    public IRelayCommand SchliessenCommand { get; }

    /// <summary>Holt den Befehl „Anleitung öffnen“.</summary>
    public IAsyncRelayCommand AnleitungCommand { get; }

    /// <summary>
    /// Prüft die Bedingungen: Ist der aktuelle Schritt erledigt, geht es zum nächsten (und das Element wird gezeigt).
    /// </summary>
    internal void Pruefen()
    {
        if (vonHand)
        {
            return;
        }

        bool weiter = false;
        while (aktuell.Schritt.Erledigt is { } erledigt && erledigt(hauptfenster) && aktuell.Nummer < Schritte.Count)
        {
            aktuell.IstErledigt = true;
            Setzen(Schritte[aktuell.Nummer]);
            weiter = true;
        }

        if (aktuell.Nummer == Schritte.Count && aktuell.Schritt.Erledigt is { } letzter && letzter(hauptfenster))
        {
            aktuell.IstErledigt = true;
        }

        if (weiter)
        {
            Hervorheben();
        }
    }

    /// <summary>Zeigt das Element des aktuellen Schritts (ohne vorher eine Ansicht zu öffnen).</summary>
    private void Hervorheben()
    {
        if (aktuell.Schritt.Ziel is { } ziel)
        {
            hauptfenster.ZielZeigen(ziel);
        }
    }

    private void Zeigen()
    {
        aktuell.Schritt.Vorbereiten?.Invoke(hauptfenster);
        Hervorheben();
    }

    private void Weiter()
    {
        aktuell.IstErledigt = true;
        if (aktuell.Nummer == Schritte.Count)
        {
            hauptfenster.FuehrungBeenden();
            return;
        }

        Setzen(Schritte[aktuell.Nummer]);
        Pruefen();
        Zeigen();
    }

    private void Zurueck()
    {
        if (aktuell.Nummer > 1)
        {
            Setzen(Schritte[aktuell.Nummer - 2]);
            aktuell.IstErledigt = false;
            vonHand = true;
            Zeigen();
        }
    }

    private void Waehlen(Fuehrungszeile zeile)
    {
        Setzen(zeile);
        vonHand = true;
        Zeigen();
    }

    private void Setzen(Fuehrungszeile neu)
    {
        aktuell.IstAktuell = false;
        aktuell = neu;
        vonHand = false;
        aktuell.IstAktuell = true;
        hauptfenster.FuehrungsstandMerken(Art, aktuell.Nummer - 1);
        OnPropertyChanged(nameof(Aktuell));
        OnPropertyChanged(nameof(FortschrittText));
        OnPropertyChanged(nameof(WeiterText));
        ZurueckCommand.NotifyCanExecuteChanged();
        ZeigenCommand.NotifyCanExecuteChanged();
    }
}
