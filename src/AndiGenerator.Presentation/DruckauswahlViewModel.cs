// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>
/// Druckauswahl (Original <c>TFormPrintSelection</c>): welche Teile gedruckt werden, das Format und – anders als im
/// Original, das immer den angezeigten Plan druckt – der Plan, aus dem Kosten, Spielplan und Mannschaftspläne stammen.
/// Im Kompaktmodus gibt es nichts zu wählen (Peter, 14.10.2026): Gedruckt wird nur die Übersicht für die Vereine, im
/// Hochformat, mit dem in der Ansicht „Kompaktspieltage“ gewählten Plan – die übrigen Teile sind dort doppelt
/// (Terminwünsche, Spielplan, Mannschaftspläne) oder ohne Bedeutung (Nachbarmannschaften, Diagramme).
/// </summary>
public sealed class DruckauswahlViewModel : ObservableObject
{
    private PlanQuelle? quelle;
    private bool terminwuensche = true;
    private bool nachbartermine = true;
    private bool kosten = true;
    private bool spielplan = true;
    private bool mannschaftsplaene = true;
    private bool mannschaftsplaeneMitNachbarn = true;
    private bool diagramme = true;
    private bool kompaktspieltage;
    private bool querformat;

    /// <summary>Initialisiert die Auswahl für normale Runden; alle Teile sind vorgewählt.</summary>
    /// <param name="quellen">Planquellen, die einen Plan haben.</param>
    public DruckauswahlViewModel(IReadOnlyList<PlanQuelle> quellen)
    {
        ArgumentNullException.ThrowIfNull(quellen);
        Quellen = quellen;
        quelle = quellen.Count > 0 ? quellen[0] : null;
    }

    /// <summary>Holt einen Wert, der angibt, ob im Kompaktmodus gedruckt wird (nur die Übersicht für die Vereine).</summary>
    public bool IstKompakt { get; private init; }

    /// <summary>Holt den Namen des Plans, wie ihn die Ansicht „Kompaktspieltage“ nennt (nur im Kompaktmodus).</summary>
    public string Planname { get; private init; } = string.Empty;

    /// <summary>Holt den Text im Kompaktmodus: welcher Plan gedruckt wird bzw. dass es noch keinen gibt.</summary>
    public string Kompakthinweis => HatPlan
        ? "Gedruckt wird der Plan, der in der Ansicht Kompaktspieltage gewählt ist: " + Planname + "."
        : "Noch kein Plan vorhanden – erst mit ▶ Pläne berechnen oder in der Ansicht Kompaktspieltage einen Plan wählen.";

    /// <summary>Holt die Planquellen, die einen Plan haben.</summary>
    public IReadOnlyList<PlanQuelle> Quellen { get; }

    /// <summary>Holt einen Wert, der angibt, ob ein Plan gedruckt werden kann.</summary>
    public bool HatPlan => Quellen.Count > 0;

    /// <summary>Holt oder setzt den Plan, aus dem Kosten, Spielplan und Mannschaftspläne stammen.</summary>
    public PlanQuelle? Quelle
    {
        get => quelle;
        set
        {
            if (SetProperty(ref quelle, value))
            {
                OnPropertyChanged(nameof(IstGueltig));
            }
        }
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Terminwünsche gedruckt werden.</summary>
    public bool Terminwuensche
    {
        get => terminwuensche;
        set => Setzen(ref terminwuensche, value);
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Termine der Nachbarmannschaften gedruckt werden.</summary>
    public bool Nachbartermine
    {
        get => nachbartermine;
        set => Setzen(ref nachbartermine, value);
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Kosten gedruckt werden.</summary>
    public bool Kosten
    {
        get => kosten;
        set => Setzen(ref kosten, value);
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob der Spielplan gedruckt wird.</summary>
    public bool Spielplan
    {
        get => spielplan;
        set => Setzen(ref spielplan, value);
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Mannschaftspläne gedruckt werden.</summary>
    public bool Mannschaftsplaene
    {
        get => mannschaftsplaene;
        set => Setzen(ref mannschaftsplaene, value);
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Mannschaftspläne mit Nachbarmannschaften gedruckt werden.</summary>
    public bool MannschaftsplaeneMitNachbarn
    {
        get => mannschaftsplaeneMitNachbarn;
        set => Setzen(ref mannschaftsplaeneMitNachbarn, value);
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Diagramme gedruckt werden.</summary>
    public bool Diagramme
    {
        get => diagramme;
        set => Setzen(ref diagramme, value);
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob die Übersicht der Kompaktspieltage für die Vereine gedruckt wird.</summary>
    public bool Kompaktspieltage
    {
        get => kompaktspieltage;
        set => Setzen(ref kompaktspieltage, value);
    }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob im Querformat gedruckt wird.</summary>
    public bool Querformat
    {
        get => querformat;
        set => SetProperty(ref querformat, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob mindestens ein druckbarer Teil gewählt ist.</summary>
    public bool IstGueltig =>
        Terminwuensche || Nachbartermine || (Quelle is not null && (Kosten || Kompaktspieltage || Spielplan || Mannschaftsplaene || MannschaftsplaeneMitNachbarn || Diagramme));

    /// <summary>Erzeugt die Auswahl im Kompaktmodus: nur die Übersicht der Kompaktspieltage für die Vereine, im Hochformat.</summary>
    /// <param name="quelle">Die Quelle des in der Ansicht gewählten Plans oder <c>null</c>, solange es keinen gibt.</param>
    /// <param name="planname">Der Name des Plans, wie ihn die Ansicht nennt.</param>
    /// <returns>Die Auswahl.</returns>
    public static DruckauswahlViewModel Kompakt(PlanQuelle? quelle, string planname)
    {
        ArgumentNullException.ThrowIfNull(planname);
        var auswahl = new DruckauswahlViewModel(quelle is null ? [] : [quelle]) { IstKompakt = true, Planname = planname };
        auswahl.terminwuensche = false;
        auswahl.nachbartermine = false;
        auswahl.kosten = false;
        auswahl.spielplan = false;
        auswahl.mannschaftsplaene = false;
        auswahl.mannschaftsplaeneMitNachbarn = false;
        auswahl.diagramme = false;
        auswahl.kompaktspieltage = true;
        return auswahl;
    }

    private void Setzen(ref bool feld, bool wert, [CallerMemberName] string? name = null)
    {
        if (SetProperty(ref feld, wert, name))
        {
            OnPropertyChanged(nameof(IstGueltig));
        }
    }
}
