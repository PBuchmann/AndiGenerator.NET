// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Einrichtungsseite nach dem Öffnen einer Staffel: alle Rückfragen des Originals (gespeicherte Einstellungen,
/// Rundenplanung, Erststart-Assistent <c>TDialogFirstStart</c>) auf einer Seite statt in einzelnen Fenstern. Links die
/// Punkte als Checkliste in beliebiger Reihenfolge, rechts der gewählte Punkt. Übersprungene oder beim Abschließen
/// offene Punkte ändern nichts – wie „Nein“ bzw. das Schließen des Assistenten im Original.
/// </summary>
public sealed class EinrichtungViewModel : ObservableObject
{
    private readonly Func<Einrichtungsschritt, Task<bool>> uebernehmen;
    private readonly Func<Einrichtungsschritt, DatenDialogViewModel?> seiteErzeugen;
    private readonly Func<Task> abschliessen;
    private Einrichtungsschritt aktiv;
    private string meldung = string.Empty;

    /// <summary>Initialisiert die Seite.</summary>
    /// <param name="titel">Überschrift, z. B. „4. Kreisklasse Gruppe A einrichten“.</param>
    /// <param name="schritte">Die Punkte in ihrer Reihenfolge (mindestens einer).</param>
    /// <param name="uebernehmen">Übernimmt einen Punkt; <c>false</c>, wenn er nicht übernommen werden konnte.</param>
    /// <param name="seiteErzeugen">Erzeugt die eingebettete Seite eines Datenpunkts mit dem aktuellen Stand der Daten.</param>
    /// <param name="abschliessen">Schließt die Seite; generiert wird danach erst mit „Generierung starten“.</param>
    public EinrichtungViewModel(
        string titel,
        IReadOnlyList<Einrichtungsschritt> schritte,
        Func<Einrichtungsschritt, Task<bool>> uebernehmen,
        Func<Einrichtungsschritt, DatenDialogViewModel?> seiteErzeugen,
        Func<Task> abschliessen)
    {
        ArgumentNullException.ThrowIfNull(schritte);
        ArgumentOutOfRangeException.ThrowIfZero(schritte.Count);
        ArgumentNullException.ThrowIfNull(uebernehmen);
        ArgumentNullException.ThrowIfNull(seiteErzeugen);
        ArgumentNullException.ThrowIfNull(abschliessen);
        Titel = titel;
        Schritte = schritte;
        this.uebernehmen = uebernehmen;
        this.seiteErzeugen = seiteErzeugen;
        this.abschliessen = abschliessen;
        foreach (Einrichtungsschritt schritt in schritte)
        {
            schritt.Gewaehlt += (_, _) => Aktiv = schritt;
        }

        aktiv = schritte[0];
        Zeigen(aktiv);
        UebernehmenCommand = new AsyncRelayCommand(UebernehmenAsync);
        UeberspringenCommand = new RelayCommand(Ueberspringen);
        AbschliessenCommand = new AsyncRelayCommand(AbschliessenAsync);
    }

    /// <summary>Holt die Überschrift.</summary>
    public string Titel { get; }

    /// <summary>Holt die Punkte.</summary>
    public IReadOnlyList<Einrichtungsschritt> Schritte { get; }

    /// <summary>Holt oder setzt den gezeigten Punkt.</summary>
    public Einrichtungsschritt Aktiv
    {
        get => aktiv;
        set
        {
            if (ReferenceEquals(value, aktiv))
            {
                return;
            }

            aktiv.IstAktiv = false;
            aktiv.Seite = null;
            SetProperty(ref aktiv, value);
            Zeigen(value);
            Meldung = string.Empty;
            OnPropertyChanged(nameof(UebernehmenText));
        }
    }

    /// <summary>Holt die Beschriftung des Übernehmen-Knopfs.</summary>
    public string UebernehmenText => Aktiv.IstEntscheidung ? "Auswahl übernehmen" : "Übernehmen und weiter";

    /// <summary>Holt den Fortschritt als Text, z. B. „2 von 6 Punkten erledigt“.</summary>
    public string FortschrittText => string.Create(CultureInfo.InvariantCulture, $"{Bearbeitet} von {Schritte.Count} Punkten erledigt");

    /// <summary>Holt den Fortschritt als Anteil (0 … 100) für den Balken.</summary>
    public double Fortschritt => 100.0 * Bearbeitet / Schritte.Count;

    /// <summary>Holt eine Meldung, wenn ein Punkt nicht übernommen werden konnte (z. B. ungültige Eingaben).</summary>
    public string Meldung
    {
        get => meldung;
        private set
        {
            if (SetProperty(ref meldung, value))
            {
                OnPropertyChanged(nameof(HatMeldung));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob eine Meldung angezeigt wird.</summary>
    public bool HatMeldung => Meldung.Length > 0;

    /// <summary>Holt den Befehl „Übernehmen und weiter“.</summary>
    public IAsyncRelayCommand UebernehmenCommand { get; }

    /// <summary>Holt den Befehl „Überspringen“.</summary>
    public IRelayCommand UeberspringenCommand { get; }

    /// <summary>
    /// Holt den Befehl „Abschließen“: Bei offenen Entscheidungen gilt die angeklickte Wahl, alle übrigen offenen Punkte
    /// bleiben unverändert.
    /// </summary>
    public IAsyncRelayCommand AbschliessenCommand { get; }

    private int Bearbeitet => Schritte.Count(s => s.Zustand != Schrittzustand.Offen);

    private void Zeigen(Einrichtungsschritt schritt)
    {
        schritt.IstAktiv = true;
        if (!schritt.IstEntscheidung)
        {
            schritt.Seite = seiteErzeugen(schritt);
        }
    }

    private async Task UebernehmenAsync()
    {
        Einrichtungsschritt schritt = Aktiv;
        if (!await uebernehmen(schritt))
        {
            Meldung = schritt.Seite is { HatMeldung: true } seite ? seite.Meldung : "Der Punkt konnte nicht übernommen werden.";
            return;
        }

        Abhaken(schritt, Schrittzustand.Erledigt);
    }

    private async Task AbschliessenAsync()
    {
        foreach (Einrichtungsschritt schritt in Schritte.Where(s => s.IstEntscheidung && s.IstGewaehlt && s.Zustand == Schrittzustand.Offen).ToList())
        {
            if (!await uebernehmen(schritt))
            {
                Aktiv = schritt;
                Meldung = "Der Punkt konnte nicht übernommen werden.";
                return;
            }

            Abhaken(schritt, Schrittzustand.Erledigt);
        }

        await abschliessen();
    }

    private void Ueberspringen() => Abhaken(Aktiv, Schrittzustand.Uebersprungen);

    private void Abhaken(Einrichtungsschritt schritt, Schrittzustand zustand)
    {
        schritt.Zustand = zustand;
        OnPropertyChanged(nameof(FortschrittText));
        OnPropertyChanged(nameof(Fortschritt));

        // Weiter zum nächsten offenen Punkt, zuerst nach dem aktuellen, dann von vorn.
        int index = Schritte.ToList().IndexOf(schritt);
        Einrichtungsschritt? naechster = Schritte.Skip(index + 1).Concat(Schritte.Take(index)).FirstOrDefault(s => s.Zustand == Schrittzustand.Offen);
        if (naechster is not null)
        {
            Aktiv = naechster;
        }
        else
        {
            // Alles bearbeitet: die eingebettete Seite zeigt den neuen Stand.
            Meldung = string.Empty;
            Zeigen(schritt);
        }
    }
}
