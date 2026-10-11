// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using CommunityToolkit.Mvvm.ComponentModel;

namespace AndiGenerator.Presentation;

/// <summary>Eine Insel im Maschinenraum: eine eigenständige Suche mit eigenem besten Plan.</summary>
public sealed class Inselanzeige : ObservableObject
{
    private string kosten = string.Empty;
    private bool istBeste;
    private bool laeuft;
    private double fortschritt;
    private string fortschrittText = string.Empty;
    private string neustarts = string.Empty;
    private string strategien = string.Empty;
    private IReadOnlyList<Kostentreiberzeile> kostentreiber = [];
    private string hinweis = string.Empty;
    private bool zeigtVerstoesse;
    private string verstossA = string.Empty;
    private string verstossB = string.Empty;
    private string verstossC = string.Empty;

    /// <summary>Initialisiert die Insel.</summary>
    /// <param name="titel">Überschrift, z. B. „Insel 2“ oder „Spezial-Insel“.</param>
    /// <param name="spezial">Ist die Spezial-Insel.</param>
    public Inselanzeige(string titel, bool spezial)
    {
        Titel = titel;
        IstSpezial = spezial;
    }

    /// <summary>Holt die Überschrift.</summary>
    public string Titel { get; }

    /// <summary>Holt einen Wert, der angibt, ob es die Spezial-Insel ist.</summary>
    public bool IstSpezial { get; }

    /// <summary>Holt die Kosten ihres besten Plans.</summary>
    public string Kosten
    {
        get => kosten;
        internal set => SetProperty(ref kosten, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob sie gerade den besten Plan aller Inseln hat.</summary>
    public bool IstBeste
    {
        get => istBeste;
        internal set => SetProperty(ref istBeste, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob sie rechnet.</summary>
    public bool Laeuft
    {
        get => laeuft;
        internal set => SetProperty(ref laeuft, value);
    }

    /// <summary>Holt den Weg bis zum Neustart in Prozent: Pläne ohne Verbesserung im Verhältnis zur Grenze.</summary>
    public double Fortschritt
    {
        get => fortschritt;
        internal set => SetProperty(ref fortschritt, value);
    }

    /// <summary>Holt den Text zum Fortschritt.</summary>
    public string FortschrittText
    {
        get => fortschrittText;
        internal set => SetProperty(ref fortschrittText, value);
    }

    /// <summary>Holt die Zahl der Neustarts als Text.</summary>
    public string Neustarts
    {
        get => neustarts;
        internal set => SetProperty(ref neustarts, value);
    }

    /// <summary>Holt die größten Kostenverursacher ihres besten Plans.</summary>
    public IReadOnlyList<Kostentreiberzeile> Kostentreiber
    {
        get => kostentreiber;
        internal set => SetProperty(ref kostentreiber, value);
    }

    /// <summary>Holt die Suchstrategien ihrer Suchplätze (als Hinweis für Neugierige).</summary>
    public string Strategien
    {
        get => strategien;
        internal set => SetProperty(ref strategien, value);
    }

    /// <summary>Holt einen Wert, der angibt, ob statt der Kosten die Verstöße A, B und C gezeigt werden (Automodus).</summary>
    public bool ZeigtVerstoesse
    {
        get => zeigtVerstoesse;
        internal set => SetProperty(ref zeigtVerstoesse, value);
    }

    /// <summary>Holt die Verstöße der Stufe A ihres besten Plans.</summary>
    public string VerstossA
    {
        get => verstossA;
        internal set => SetProperty(ref verstossA, value);
    }

    /// <summary>Holt die Verstöße der Stufe B ihres besten Plans.</summary>
    public string VerstossB
    {
        get => verstossB;
        internal set => SetProperty(ref verstossB, value);
    }

    /// <summary>Holt die Verstöße der Stufe C ihres besten Plans.</summary>
    public string VerstossC
    {
        get => verstossC;
        internal set => SetProperty(ref verstossC, value);
    }

    /// <summary>Holt einen erklärenden Hinweis (Spezial-Insel).</summary>
    public string Hinweis
    {
        get => hinweis;
        internal set => SetProperty(ref hinweis, value);
    }
}
