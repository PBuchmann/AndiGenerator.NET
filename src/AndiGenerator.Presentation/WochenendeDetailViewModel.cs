// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>
/// 60-km-Regel einer Mannschaft (Original <c>TDialogPanel60kmDetail</c>): alle anderen Mannschaften mit Häkchen, gegen die
/// wegen des langen Anfahrtswegs nur am Wochenende gespielt werden soll.
/// </summary>
public sealed class WochenendeDetailViewModel : DatenSeiteViewModel
{
    private readonly string mannschaft;

    /// <summary>Initialisiert die Ansicht.</summary>
    /// <param name="bearbeitung">Arbeitskopie.</param>
    /// <param name="mannschaft">Mannschaftsname.</param>
    public WochenendeDetailViewModel(Datenbearbeitung bearbeitung, string mannschaft)
        : base(bearbeitung, "60km Regel")
    {
        this.mannschaft = mannschaft;
        IReadOnlyList<Hallenentfernung> entfernungen = bearbeitung.Hallenentfernungen(mannschaft);
        Gegner = entfernungen.Select(e => new Gegnerzeile(e, AenderungMelden)).ToList();
        HatEntfernungen = Gegner.Any(g => g.Entfernung.Length > 0);
        List<Hallenentfernung> bekannt = entfernungen.Where(e => e.Luftlinie is not null).ToList();
        Gesamt = bekannt.Sum(e => e.Luftlinie ?? 0).ToString("0", CultureInfo.InvariantCulture) + " km";
        GesamtText = bekannt.Count == Gegner.Count
            ? string.Create(CultureInfo.InvariantCulture, $"Gesamt zu allen {bekannt.Count} Gegnern (je einfache Strecke)")
            : string.Create(CultureInfo.InvariantCulture, $"Gesamt zu {bekannt.Count} von {Gegner.Count} Gegnern (je einfache Strecke; ohne Adresse: {string.Join(", ", Gegner.Where(g => g.Entfernung.Length == 0).Select(g => g.Name))})");
        Laden();
    }

    /// <summary>Holt die Erklärung zu Entfernungen und Hinweisen.</summary>
    public static string Entfernungserklaerung =>
        "Entfernung in Luftlinie zwischen den Hallen. „weit“: über 60 km ohne Häkchen, obwohl der Gegner auch unter der "
        + "Woche Heimspiele hat. „nah“: Häkchen bei höchstens 60 km. Die Wettspielordnung legt kein Maß fest – nur Hinweise.";

    /// <summary>Holt den Hinweis, woher die Entfernungen kommen.</summary>
    public static string OhneEntfernungen =>
        "Entfernungen erscheinen, wenn die Terminwünsche (PDF) aus click-TT übernommen sind – in der Einrichtung unter „Daten aus click-TT ergänzen“.";

    /// <summary>Holt alle anderen Mannschaften mit Häkchen.</summary>
    public IReadOnlyList<Gegnerzeile> Gegner { get; }

    /// <summary>Holt einen Wert, der angibt, ob Entfernungen bekannt sind (Hallenadressen aus den Terminwünschen).</summary>
    public bool HatEntfernungen { get; }

    /// <summary>Holt die Summe der Entfernungen zu allen Gegnern mit bekannter Halle, z. B. „612 km“.</summary>
    public string Gesamt { get; }

    /// <summary>Holt die Beschriftung der Summe (wie viele Gegner, einfache Strecke).</summary>
    public string GesamtText { get; }

    /// <inheritdoc/>
    public override bool IstStandard =>
        Bearbeitung.NurWochenendeStandard(mannschaft) is not IReadOnlyList<string> standard
        || (standard.Count == Gewaehlt().Count && standard.All(Gewaehlt().Contains));

    /// <inheritdoc/>
    public override void Laden() => Anzeigen(Bearbeitung.NurWochenendeGegen(mannschaft));

    /// <inheritdoc/>
    public override void Speichern() => Bearbeitung.NurWochenendeSpeichern(mannschaft, Gewaehlt());

    /// <inheritdoc/>
    public override void Standard()
    {
        // Wie im Original: die Häkchen des click-TT-Stands setzen; gespeichert wird beim Verlassen der Seite.
        Anzeigen(Bearbeitung.NurWochenendeStandard(mannschaft) ?? []);
        AenderungMelden();
    }

    private List<string> Gewaehlt() => Gegner.Where(g => g.Gewaehlt).Select(g => g.Name).ToList();

    private void Anzeigen(IReadOnlyList<string> gewaehlt)
    {
        foreach (Gegnerzeile g in Gegner)
        {
            g.Setzen(gewaehlt.Contains(g.Name, StringComparer.OrdinalIgnoreCase));
        }
    }
}
