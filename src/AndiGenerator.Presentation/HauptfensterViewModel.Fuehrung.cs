// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Führung „Schritt für Schritt“: die Begleitleiste rechts, der Knopf links und in der Statuszeile der nächste Schritt der
/// Führung, die zur Lage passt (Kompaktmodus, Automodus oder Kostenoptimierung).
/// </summary>
public sealed partial class HauptfensterViewModel
{
    private readonly Dictionary<Fuehrungsart, int> fuehrungsstand = [];
    private FuehrungViewModel? fuehrung;
    private string fuehrungsHinweis = string.Empty;

    /// <summary>Tritt ein, wenn die Oberfläche ein Element hervorheben soll (Name aus <see cref="Fuehrungsziel"/>).</summary>
    public event EventHandler<string>? ZielHervorheben;

    /// <summary>Holt den Befehl „Schritt für Schritt“ (F1): öffnet die passende Führung bzw. schließt sie.</summary>
    public IRelayCommand FuehrungCommand { get; }

    /// <summary>Holt die laufende Führung oder <c>null</c>.</summary>
    public FuehrungViewModel? Fuehrung
    {
        get => fuehrung;
        private set
        {
            if (SetProperty(ref fuehrung, value))
            {
                OnPropertyChanged(nameof(ZeigtFuehrung));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob die Begleitleiste gezeigt wird.</summary>
    public bool ZeigtFuehrung => Fuehrung is not null;

    /// <summary>
    /// Holt den Hinweis in der Statuszeile, z. B. „Schritt für Schritt – nächster Schritt: Pläne vergleichen“; leer, solange die
    /// Führung offen ist oder keine Staffel bearbeitet wird.
    /// </summary>
    public string FuehrungsHinweis
    {
        get => fuehrungsHinweis;
        private set
        {
            if (SetProperty(ref fuehrungsHinweis, value))
            {
                OnPropertyChanged(nameof(HatFuehrungsHinweis));
            }
        }
    }

    /// <summary>Holt einen Wert, der angibt, ob es einen Hinweis auf die Führung gibt.</summary>
    public bool HatFuehrungsHinweis => FuehrungsHinweis.Length > 0;

    /// <summary>Holt die Führung, die zur Lage passt.</summary>
    public Fuehrungsart PassendeFuehrung => (KompaktAktiv, Automodus) switch
    {
        (true, _) => Fuehrungsart.Kompaktspieltage,
        (_, true) => Fuehrungsart.Automodus,
        _ => Fuehrungsart.Kostenoptimierung,
    };

    /// <summary>Holt einen Wert, der angibt, ob die Generierung einen Plan hat (laufend, pausiert oder gestoppt).</summary>
    internal bool HatLaufendenPlan => laufenderStand is not null;

    /// <summary>Holt einen Wert, der angibt, ob die Ansicht Kompaktspieltage offen ist.</summary>
    internal bool KompaktAnsichtOffen => kompaktAnsicht is not null;

    /// <summary>Öffnet eine Führung, beginnend beim Schritt, an dem man gerade steht.</summary>
    /// <param name="art">Die Führung.</param>
    public void FuehrungStarten(Fuehrungsart art)
    {
        Fuehrung = new FuehrungViewModel(this, art, Einstieg(art));
        Fuehrung.Pruefen();
        FuehrungsHinweis = string.Empty;
        if (Fuehrung.Aktuell.Schritt.Ziel is { } ziel)
        {
            ZielZeigen(ziel);
        }
    }

    /// <summary>Schließt die Führung.</summary>
    internal void FuehrungBeenden()
    {
        Fuehrung = null;
        HinweisBilden();
    }

    /// <summary>Lässt die Oberfläche ein Element hervorheben.</summary>
    /// <param name="ziel">Name aus <see cref="Fuehrungsziel"/>.</param>
    internal void ZielZeigen(string ziel) => ZielHervorheben?.Invoke(this, ziel);

    /// <summary>Merkt sich, wo eine Führung steht (für den Hinweis und das nächste Öffnen).</summary>
    /// <param name="art">Die Führung.</param>
    /// <param name="schritt">Der Schritt ab 0.</param>
    internal void FuehrungsstandMerken(Fuehrungsart art, int schritt) => fuehrungsstand[art] = schritt;

    /// <summary>
    /// Der Schritt, bei dem eine Führung beginnt: nach dem letzten Schritt, dessen Bedingung schon gilt, frühestens dort, wo sie
    /// zuletzt stand.
    /// </summary>
    private int Einstieg(Fuehrungsart art)
    {
        IReadOnlyList<Fuehrungsschritt> schritte = Fuehrungen.Schritte(art);
        int nach = 0;
        for (int i = 0; i < schritte.Count; i++)
        {
            if (schritte[i].Erledigt is { } erledigt && erledigt(this))
            {
                nach = i + 1;
            }
        }

        return Math.Min(schritte.Count - 1, Math.Max(nach, fuehrungsstand.GetValueOrDefault(art)));
    }

    private void FuehrungUmschalten()
    {
        if (Fuehrung is not null)
        {
            FuehrungBeenden();
        }
        else if (ZeigtArbeitsbereich)
        {
            FuehrungStarten(PassendeFuehrung);
        }
    }

    /// <summary>Nach jeder Änderung: Bedingungen der Führung prüfen bzw. den Hinweis in der Statuszeile bilden.</summary>
    private void FuehrungAktualisieren(string? eigenschaft)
    {
        if (eigenschaft is nameof(FuehrungsHinweis) or nameof(HatFuehrungsHinweis) or nameof(Fuehrung) or nameof(ZeigtFuehrung) or nameof(Statuszeile))
        {
            return;
        }

        if (eigenschaft is nameof(ZeigtArbeitsbereich) or nameof(IstGeoeffnet) && !ZeigtArbeitsbereich)
        {
            // Staffel geschlossen: Führung und Stand gehören zur Staffel.
            Fuehrung = null;
            fuehrungsstand.Clear();
        }

        FuehrungCommand?.NotifyCanExecuteChanged();
        Fuehrung?.Pruefen();
        HinweisBilden();
    }

    private void HinweisBilden()
    {
        if (Fuehrung is not null || !ZeigtArbeitsbereich)
        {
            FuehrungsHinweis = string.Empty;
            return;
        }

        Fuehrungsart art = PassendeFuehrung;
        int schritt = Einstieg(art);
        FuehrungsHinweis = string.Create(
            CultureInfo.InvariantCulture,
            $"{Fuehrungen.Titel(art)} – nächster Schritt {schritt + 1}: {Fuehrungen.Schritte(art)[schritt].Titel}");
    }
}
