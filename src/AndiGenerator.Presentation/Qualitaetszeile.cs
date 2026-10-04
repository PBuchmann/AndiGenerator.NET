// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Klassenstufe = AndiGenerator.Engine.Inseln.Stufe;

namespace AndiGenerator.Presentation;

/// <summary>
/// Zeile der Qualitätsansicht. Im Automodus lassen sich Kriterien (außer den harten Fehlern A1) einer anderen Stufe
/// zuordnen und innerhalb ihrer Stufe verschieben – mit Knöpfen oder durch Ziehen; die Werte werden bei jedem besseren
/// Plan erneuert.
/// </summary>
public sealed class Qualitaetszeile : ObservableObject
{
    private readonly Zeilenbearbeitung? bearbeitung;
    private string verstoesse = string.Empty;
    private string kosten = string.Empty;
    private Qualitaetszustand zustand;
    private bool istGewaehlt;

    /// <summary>Initialisiert die Zeile; Verstöße, Kosten und Zustand werden danach gesetzt.</summary>
    /// <param name="stufe">Stufe mit Nummer, z. B. <c>A2</c>.</param>
    /// <param name="name">Kriterium.</param>
    /// <param name="kriterium">Das einstufbare Kriterium; <c>null</c> bei den harten Fehlern.</param>
    /// <param name="bearbeitung">Einstufen und Verschieben; <c>null</c> = nicht bearbeitbar (z. B. außerhalb des Automodus).</param>
    /// <param name="gezogen">Die Zeile wird gerade gezogen (Vorschau).</param>
    internal Qualitaetszeile(string stufe, string name, Kostenkriterium? kriterium = null, Zeilenbearbeitung? bearbeitung = null, bool gezogen = false)
    {
        Stufe = stufe;
        Name = name;
        Kriterium = kriterium;
        IstGezogen = gezogen;
        this.bearbeitung = kriterium is null ? null : bearbeitung;
        EinstufenACommand = new RelayCommand(() => Einstufen(Klassenstufe.A), () => KannAB && Klasse != "A");
        EinstufenBCommand = new RelayCommand(() => Einstufen(Klassenstufe.B), () => KannAB && Klasse != "B");
        EinstufenCCommand = new RelayCommand(() => Einstufen(Klassenstufe.C), () => IstEinstufbar && Klasse != "C");
        HochCommand = new RelayCommand(() => this.bearbeitung?.Hoch?.Invoke(), () => this.bearbeitung?.Hoch is not null);
        RunterCommand = new RelayCommand(() => this.bearbeitung?.Runter?.Invoke(), () => this.bearbeitung?.Runter is not null);
    }

    /// <summary>Holt die Stufe mit Nummer, z. B. <c>A2</c>.</summary>
    public string Stufe { get; }

    /// <summary>Holt das Kriterium.</summary>
    public string Name { get; }

    /// <summary>Holt das einstufbare Kriterium; <c>null</c> bei den harten Fehlern.</summary>
    public Kostenkriterium? Kriterium { get; }

    /// <summary>Holt einen Wert, der angibt, ob die Zeile eingestuft und verschoben werden kann (im Automodus, alle außer A1).</summary>
    public bool IstEinstufbar => bearbeitung is not null;

    /// <summary>Holt einen Wert, der angibt, ob die Zeile gerade gezogen wird (Vorschau beim Ablegen).</summary>
    public bool IstGezogen { get; }

    /// <summary>Holt die Stufe ohne Nummer (<c>A</c>, <c>B</c> oder <c>C</c>).</summary>
    public string Klasse => Stufe[..1];

    /// <summary>Holt einen Wert, der angibt, ob die Zeile in Stufe A steht.</summary>
    public bool IstA => Klasse == "A";

    /// <summary>Holt einen Wert, der angibt, ob die Zeile in Stufe B steht.</summary>
    public bool IstB => Klasse == "B";

    /// <summary>Holt einen Wert, der angibt, ob die Zeile in Stufe C steht.</summary>
    public bool IstC => Klasse == "C";

    /// <summary>Holt einen Wert, der angibt, ob das Kriterium in A oder B stehen darf (nicht bei den Spieltagen ohne Anzahl).</summary>
    public bool KannAB => IstEinstufbar && Kriterium is Kostenkriterium k && Stufeneinteilung.HatAnzahl(k);

    /// <summary>Holt einen Wert, der angibt, ob die Zeile gewählt ist (ihre Verstöße stehen rechts daneben).</summary>
    public bool IstGewaehlt
    {
        get => istGewaehlt;
        internal set => SetProperty(ref istGewaehlt, value);
    }

    /// <summary>Holt die Verstöße, z. B. <c>3 von 15</c> oder <c>–</c>, wenn nur Kosten bekannt sind.</summary>
    public string Verstoesse
    {
        get => verstoesse;
        internal set => SetProperty(ref verstoesse, value);
    }

    /// <summary>Holt die Kosten.</summary>
    public string Kosten
    {
        get => kosten;
        internal set => SetProperty(ref kosten, value);
    }

    /// <summary>Holt den Zustand für die Farbe.</summary>
    public Qualitaetszustand Zustand
    {
        get => zustand;
        internal set => SetProperty(ref zustand, value);
    }

    /// <summary>Holt den Befehl „in Stufe A“.</summary>
    public IRelayCommand EinstufenACommand { get; }

    /// <summary>Holt den Befehl „in Stufe B“.</summary>
    public IRelayCommand EinstufenBCommand { get; }

    /// <summary>Holt den Befehl „in Stufe C“.</summary>
    public IRelayCommand EinstufenCCommand { get; }

    /// <summary>Holt den Befehl „nach oben“ (wichtiger innerhalb der Stufe).</summary>
    public IRelayCommand HochCommand { get; }

    /// <summary>Holt den Befehl „nach unten“ (weniger wichtig innerhalb der Stufe).</summary>
    public IRelayCommand RunterCommand { get; }

    private void Einstufen(Klassenstufe neu)
    {
        if (Kriterium is Kostenkriterium k)
        {
            bearbeitung?.Einstufen(k, neu);
        }
    }
}
