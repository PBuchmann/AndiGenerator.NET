// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>Seite „Auswärtskoppeltermine“ (Original <c>TDialogPanelAuswaertsKoppelDetail</c>).</summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>Mindestabstand zweier Spiele am selben Tag (Original <c>MinGameDistance</c> = 3:30 h).</summary>
    private static readonly TimeSpan MindestAbstand = new(3, 30, 0);

    /// <summary>Liest die Auswärtskoppelwünsche einer Mannschaft.</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Die Wünsche in der Reihenfolge der Daten.</returns>
    public IReadOnlyList<Auswaertskoppeldaten> Auswaertskoppeln(string mannschaft) =>
        Team(mannschaft).KinderMitNamen("roadcouple").Select(AuswaertskoppelLesen).ToList();

    /// <summary>Liefert die möglichen Partner eines Auswärtskoppelwunschs: alle anderen Mannschaften.</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Die Namen, sortiert.</returns>
    public IReadOnlyList<string> Auswaertskoppelpartner(string mannschaft) =>
        Mannschaftsnamen().Where(n => n != mannschaft).ToList();

    /// <summary>
    /// Prüft einen Auswärtskoppelwunsch wie das Original (<c>ButtonOKClick</c>). Zusätzlich darf es dasselbe Mannschaftspaar
    /// nicht zweimal geben, sonst ließe sich die Änderungsdatei nicht eindeutig schreiben.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="bisher">Bearbeiteter Wunsch oder <c>null</c> für einen neuen.</param>
    /// <param name="neu">Die neuen Werte.</param>
    /// <returns>Die Fehlermeldung oder <c>null</c>.</returns>
    public string? AuswaertskoppelPruefen(string mannschaft, Auswaertskoppeldaten? bisher, Auswaertskoppeldaten neu)
    {
        ArgumentNullException.ThrowIfNull(neu);
        if (neu.MannschaftA.Length == 0)
        {
            return "Bitte Mannschaft 1 angegeben";
        }

        if (neu.MannschaftB.Length == 0)
        {
            return "Bitte Mannschaft 2 angegeben";
        }

        if (neu.MannschaftA == neu.MannschaftB)
        {
            return "Mannschaft 1 und 2 müssen unterschiedlich sein";
        }

        DatenKnoten? eigener = bisher is null ? null : AuswaertskoppelKnoten(mannschaft, bisher);
        bool doppelt = Team(mannschaft).KinderMitNamen("roadcouple")
            .Any(k => !ReferenceEquals(k, eigener) && k.Lesen("teamnamea") == neu.MannschaftA && k.Lesen("teamnameb") == neu.MannschaftB);
        return doppelt ? "Diesen Auswärtskoppelwunsch gibt es schon" : null;
    }

    /// <summary>Legt einen Auswärtskoppelwunsch an oder ändert ihn (Original <c>GetValue</c>).</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="bisher">Bearbeiteter Wunsch oder <c>null</c> für einen neuen.</param>
    /// <param name="neu">Die neuen Werte.</param>
    public void AuswaertskoppelSpeichern(string mannschaft, Auswaertskoppeldaten? bisher, Auswaertskoppeldaten neu)
    {
        ArgumentNullException.ThrowIfNull(neu);
        DatenKnoten knoten = bisher is null
            ? new DatenKnoten("roadcouple", Team(mannschaft))
            : AuswaertskoppelKnoten(mannschaft, bisher) ?? throw new ArgumentException("Unbekannter Auswärtskoppelwunsch.", nameof(bisher));
        knoten.Setzen("teamnamea", neu.MannschaftA);
        knoten.Setzen("teamnameb", neu.MannschaftB);
        knoten.Setzen("sameday", neu.Art);
    }

    /// <summary>Löscht einen Auswärtskoppelwunsch.</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="daten">Der Wunsch.</param>
    public void AuswaertskoppelLoeschen(string mannschaft, Auswaertskoppeldaten daten)
    {
        if (AuswaertskoppelKnoten(mannschaft, daten) is DatenKnoten knoten)
        {
            Team(mannschaft).Kinder.Remove(knoten);
        }
    }

    /// <summary>
    /// Liefert die Heimspieltermine, an denen eine andere Anfangszeit eine Auswärtskoppel ermöglichen würde (Original
    /// <c>WunschTermineNeedsTimeshiftForAuswaertskoppel</c>): Eine andere Mannschaft wünscht eine Auswärtskoppel am selben Tag
    /// mit dieser und dem Partner, und der Partner hat am selben Tag weniger als 3:30 Stunden versetzt ein Heimspiel.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Die Termine mit ihrer alternativen Zeit.</returns>
    public IReadOnlyList<Zweitzeit> AuswaertsZweitzeiten(string mannschaft)
    {
        List<string> partner = Teams(Arbeitsstand)
            .Where(t => t.Lesen("teamname") != mannschaft)
            .SelectMany(t => t.KinderMitNamen("roadcouple"))
            .Where(k => k.LesenZahl("sameday") is 0 or 2)
            .Select(k => Partner(k, mannschaft))
            .Where(p => p.Length > 0 && p != mannschaft)
            .ToList();
        List<Heimtag> partnerTage = partner.SelectMany(Heimtage).Where(h => h.IstHeimtag).ToList();
        return Heimtage(mannschaft)
            .Where(h => h.IstHeimtag && !h.Koppeltermin && partnerTage.Exists(p => p.Datum.Date == h.Datum.Date && (p.Datum - h.Datum).Duration() < MindestAbstand))
            .Select(h => new Zweitzeit(h.Datum, h.AuswaertsKoppelZweitzeit ? h.KoppelZweitzeit : null))
            .ToList();
    }

    /// <summary>
    /// Schreibt die alternativen Anfangszeiten (Original <c>FormToData</c>) und speichert alle Heimspieltermine mit
    /// <c>SetHomeDays</c>. Termine, die nicht übergeben werden, bleiben unverändert.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="zeiten">Die Termine mit ihrer alternativen Zeit.</param>
    public void AuswaertsZweitzeitenSpeichern(string mannschaft, IReadOnlyList<Zweitzeit> zeiten)
    {
        ArgumentNullException.ThrowIfNull(zeiten);
        List<Heimtag> tage = Heimtage(mannschaft).Select(h => ZweitzeitSetzen(h, zeiten)).ToList();
        HeimtageSpeichern(mannschaft, tage);
    }

    /// <summary>
    /// Holt einen Wert, der angibt, ob die Seite dem Standard entspricht (Original <c>isDefault</c>): gleiche
    /// Auswärtskoppelwünsche wie in der click-TT-Datei und keine alternativen Zeiten.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="zeiten">Die angezeigten alternativen Zeiten.</param>
    /// <returns><c>true</c>, wenn alles dem Standard entspricht.</returns>
    public bool AuswaertskoppelnSindStandard(string mannschaft, IEnumerable<Zweitzeit> zeiten)
    {
        ArgumentNullException.ThrowIfNull(zeiten);
        if (zeiten.Any(z => z.Zeit is not null))
        {
            return false;
        }

        DatenKnoten? vorlage = standard is null ? null : Teams(standard).FirstOrDefault(t => t.Lesen("teamname") == mannschaft);
        return GleicheKinder(Team(mannschaft), vorlage, "roadcouple");
    }

    /// <summary>
    /// Original <c>toDefault</c>: Auswärtskoppelwünsche aus der click-TT-Datei übernehmen und alle alternativen Zeiten löschen.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    public void AuswaertskoppelnZuruecksetzen(string mannschaft)
    {
        DatenKnoten team = Team(mannschaft);
        team.KinderEntfernen("roadcouple");
        DatenKnoten? vorlage = standard is null ? null : Teams(standard).FirstOrDefault(t => t.Lesen("teamname") == mannschaft);
        foreach (DatenKnoten k in vorlage?.KinderMitNamen("roadcouple") ?? [])
        {
            new DatenKnoten("roadcouple", team).Zuweisen(k);
        }

        AuswaertsZweitzeitenSpeichern(mannschaft, AuswaertsZweitzeiten(mannschaft).Select(z => z with { Zeit = null }).ToList());
    }

    private static Auswaertskoppeldaten AuswaertskoppelLesen(DatenKnoten k) =>
        new(k.Lesen("teamnamea"), k.Lesen("teamnameb"), k.LesenZahl("sameday"));

    private static Heimtag ZweitzeitSetzen(Heimtag h, IReadOnlyList<Zweitzeit> zeiten)
    {
        if (!h.IstHeimtag || zeiten.FirstOrDefault(z => z.Termin == h.Datum) is not Zweitzeit z)
        {
            return h;
        }

        return z.Zeit is TimeOnly zeit
            ? h with { AuswaertsKoppelZweitzeit = true, KoppelZweitzeit = zeit }
            : h with { AuswaertsKoppelZweitzeit = false };
    }

    private static string Partner(DatenKnoten koppel, string mannschaft)
    {
        string a = koppel.Lesen("teamnamea");
        string b = koppel.Lesen("teamnameb");
        string partner = a == mannschaft ? b : string.Empty;
        return b == mannschaft ? a : partner;
    }

    /// <summary>Original <c>ChildNodesAreTheSame</c>: gleiche Kinder dieses Namens, zugeordnet über den Schlüssel.</summary>
    private static bool GleicheKinder(DatenKnoten? a, DatenKnoten? b, string name)
    {
        if (a is null || b is null)
        {
            return ReferenceEquals(a, b);
        }

        return a.KinderMitNamen(name).All(k => GleichesKindIn(k, b)) && b.KinderMitNamen(name).All(k => GleichesKindIn(k, a));
    }

    private static bool GleichesKindIn(DatenKnoten kind, DatenKnoten eltern)
    {
        int index = eltern.KindIndex(kind.Schluessel());
        return index >= 0 && kind.IstGleich(eltern.Kinder[index]);
    }

    private DatenKnoten? AuswaertskoppelKnoten(string mannschaft, Auswaertskoppeldaten daten) =>
        Team(mannschaft).KinderMitNamen("roadcouple").LastOrDefault(k => AuswaertskoppelLesen(k) == daten);
}
