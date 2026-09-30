// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Terminwunsch- und Terminplanauswertung für alle Referenzfälle und für gezielt angereicherte Staffeln.</summary>
public class AuswertungenTests
{
    public static TheoryData<string> Eingaben()
    {
        var daten = new TheoryData<string>();
        foreach (string datei in Directory.GetFiles(Path.Combine(Testdaten.Referenz, "eingabe"), "*.xml").Order(StringComparer.Ordinal))
        {
            daten.Add(Path.GetFileName(datei));
        }

        return daten;
    }

    [Theory]
    [MemberData(nameof(Eingaben))]
    public void Auswertungen_aller_Referenzfaelle_lassen_sich_ermitteln(string datei)
    {
        Staffel staffel = Laden(datei);

        Terminwunschuebersicht wuensche = Terminwunschauswertung.Ermitteln(staffel, Berechnungsoptionen.Standard);
        IReadOnlyList<Terminplanzeile> plan = Terminplanauswertung.Zeilen(staffel, Berechnungsoptionen.Standard);

        Assert.Equal(staffel.Mannschaften.Count, wuensche.Mannschaften.Count);
        Assert.All(wuensche.Mannschaften, m => Assert.Contains(m.Zeilen, z => z.Ueberschrift && z.Text == "Auswertung:"));
        Assert.All(plan, z => Assert.True(z.Zeitpunkt is null || z.Kalenderwoche > 0));
    }

    [Fact]
    public void Terminwuensche_zeigen_Heimrecht_Koppelstufen_Spiellokal_und_spielfreie_Tage()
    {
        Staffel staffel = Anreicherung.Erzeugen();
        Terminwunschuebersicht uebersicht = Terminwunschauswertung.Ermitteln(staffel, Berechnungsoptionen.Standard);
        IReadOnlyList<Mannschaft> m = staffel.Mannschaften;

        List<string> erste = Texte(uebersicht, m[0].Name);
        Assert.Contains("Heimrecht:", erste);
        Assert.Contains($"Vorrunde: 2 Rückrunde: {m.Count - 3}", erste);
        Assert.Contains($"Heimspiel gegen {m[1].Name} in der Vorrunde", erste);
        Assert.Contains($"Heimspiel gegen {m[2].Name} in der Rückrunde", erste);

        List<string> vierte = Texte(uebersicht, m[3].Name);
        Assert.Contains(vierte, t => t.Contains("Zwei Heimspiele an diesem Tag möglich", StringComparison.Ordinal));
        Assert.Contains(vierte, t => t.Contains("Zwei Heimspiele an diesem Tag gewünscht", StringComparison.Ordinal) && !t.Contains("(hohe Prio)", StringComparison.Ordinal));
        Assert.Contains(vierte, t => t.Contains("Zwei Heimspiele an diesem Tag gewünscht (hohe Prio)", StringComparison.Ordinal));
        Assert.Contains(vierte, t => t.Contains("Doppelspieltag möglich", StringComparison.Ordinal));
        Assert.Contains(vierte, t => t.Contains("Doppelspieltag gewünscht", StringComparison.Ordinal) && !t.Contains("(hohe Prio)", StringComparison.Ordinal));
        Assert.Contains(vierte, t => t.Contains("Doppelspieltag gewünscht (hohe Prio)", StringComparison.Ordinal));
        Assert.Contains(vierte, t => t.Contains("Spiellokal: 2", StringComparison.Ordinal));

        Assert.Contains(Texte(uebersicht, m[6].Name), t => t.Contains("spielfreier Tag", StringComparison.Ordinal));
        Assert.True(uebersicht.HatKoppeltermine);
    }

    [Fact]
    public void Nicht_erfuellbare_Wuensche_werden_gemeldet()
    {
        Staffel r2 = Anreicherung.R2() with { BestehenderSpielplan = [] };
        List<Mannschaft> m = [.. r2.Mannschaften];
        List<Heimspieltermin> werktags = m[2].Heimspieltermine
            .Where(t => t.Zeitpunkt.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Thursday)
            .ToList();
        Assert.NotEmpty(werktags);
        m[2] = m[2] with { Heimspieltermine = werktags };
        HashSet<DateOnly> tageDerVierten = m[3].Heimspieltermine.Select(t => DateOnly.FromDateTime(t.Zeitpunkt)).ToHashSet();
        m[4] = m[4] with { Heimspieltermine = m[4].Heimspieltermine.Where(t => !tageDerVierten.Contains(DateOnly.FromDateTime(t.Zeitpunkt))).ToList() };
        m[0] = m[0] with
        {
            Sperrtermine = [.. m[0].Sperrtermine, .. m[1].Heimspieltermine.Select(t => DateOnly.FromDateTime(t.Zeitpunkt))],
            KeinWochenspielGegen = [m[2].Name],
            Auswaertskoppeln =
            [
                new Auswaertskoppel(m[3].Name, m[4].Name, Auswaertskoppelart.AmSelbenTag),
                new Auswaertskoppel("Unbekannt A", "Unbekannt B", Auswaertskoppelart.Beliebig),
            ],
        };

        List<string> texte = Texte(Terminwunschauswertung.Ermitteln(r2 with { Mannschaften = m }, Berechnungsoptionen.Standard), m[0].Name);

        Assert.Contains($"Alle Termine beim {m[1].Name} wurden gesperrt", texte);
        Assert.Contains($"60km Regel kann beim {m[2].Name} nicht eingehalten werden", texte);
        Assert.Contains($"Für den Auswärtskoppelwunsch bei {m[3].Name} und {m[4].Name} existieren keine geeigneten Wunschtermine.", texte);
        Assert.Contains("Datenfehler: Unbekanntes Team Unbekannt A in einem Auswärtskoppelwunsch.", texte);
        Assert.Contains("Datenfehler: Unbekanntes Team Unbekannt B in einem Auswärtskoppelwunsch.", texte);
        Assert.Contains($"{m[3].Name} und {m[4].Name} am gleichen Tag", texte);
        Assert.Contains("Unbekannt A und Unbekannt B mit optionaler Übernachtung", texte);
    }

    [Fact]
    public void Nur_Koppeltermine_bei_gerader_Mannschaftszahl_werden_gemeldet()
    {
        Staffel r2 = Anreicherung.R2() with { BestehenderSpielplan = [], Setzliste = null, VorgegebeneSpiele = [] };
        List<Mannschaft> m = r2.Mannschaften.Take(6).ToList();
        HashSet<string> namen = m.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        m = m.Select(x => x with
        {
            KeinWochenspielGegen = x.KeinWochenspielGegen.Where(namen.Contains).ToList(),
            Auswaertskoppeln = x.Auswaertskoppeln.Where(k => namen.Contains(k.MannschaftA) && namen.Contains(k.MannschaftB)).ToList(),
            Heimrechte = x.Heimrechte.Where(h => namen.Contains(h.Gegner)).ToList(),
        }).ToList();
        m[5] = m[5] with { Heimspieltermine = m[5].Heimspieltermine.Select(t => t with { Koppelung = Terminkoppelung.Koppeltermin }).ToList() };

        List<string> texte = Texte(Terminwunschauswertung.Ermitteln(r2 with { Mannschaften = m }, Berechnungsoptionen.Standard), m[5].Name);

        Assert.Contains(texte, t => t.StartsWith("Es wurden nur Koppeltermine angegeben", StringComparison.Ordinal));
    }

    [Fact]
    public void Terminplan_zeigt_festgelegte_Spiele_und_Spiele_der_Vereinsmannschaften()
    {
        Staffel staffel = Anreicherung.Erzeugen();
        List<Mannschaft> verein = staffel.Mannschaften
            .GroupBy(x => Terminplanauswertung.OhneMannschaftsnummer(x.Name))
            .First(g => g.Count() >= 2)
            .Take(2)
            .ToList();
        List<string> andere = staffel.Mannschaften.Select(x => x.Name).Where(n => verein.TrueForAll(v => v.Name != n)).ToList();
        DateTime tag = verein[0].Heimspieltermine[2].Zeitpunkt;
        List<Spiel> plan =
        [
            .. staffel.VorgegebeneSpiele,
            new Spiel(tag, verein[0].Name, andere[0], string.Empty),
            new Spiel(tag.AddHours(-1), andere[1], verein[1].Name, string.Empty),
        ];

        IReadOnlyList<Terminplanzeile> zeilen = Terminplanauswertung.Zeilen(staffel with { BestehenderSpielplan = plan }, Berechnungsoptionen.Standard);

        Assert.Contains(zeilen, z => z.Hinweis.Contains("manuell festgelegter Termin", StringComparison.Ordinal));
        Assert.Contains(zeilen, z => z.Hinweis.Contains("Spiellokal: 2", StringComparison.Ordinal));
        Terminplanzeile heim = Assert.Single(zeilen, z => z.Zeitpunkt == tag && z.Heim == verein[0].Name);
        Assert.Contains(heim.NachbartermineHeim, n => n.Gast == verein[1].Name && n.EchteNachbarmannschaft == (Math.Abs(verein[0].Nummer - verein[1].Nummer) == 1));
    }

    [Fact]
    public void Sonderzeichen_werden_in_Plandateien_maskiert()
    {
        var plan = new DatenKnoten("plan");
        var team = new DatenKnoten("team", plan);
        team.Setzen("teamname", "A&B <C> \"D\"\r\nE\tF");

        byte[] bytes = PlanDatenDatei.ErzeugenBytes(plan);
        string text = Encoding.UTF8.GetString(bytes);
        using var strom = new MemoryStream(bytes);
        DatenKnoten gelesen = PlanDatenDatei.Laden(strom);

        Assert.Contains("A&amp;B &lt;C&gt; &quot;D&quot;&#xD;&#xA;E&#x9;F", text, StringComparison.Ordinal);
        Assert.Equal("A&B <C> \"D\"\r\nE\tF", gelesen.ErstesKind("team")!.Lesen("teamname"));
    }

    private static List<string> Texte(Terminwunschuebersicht uebersicht, string mannschaft) =>
        uebersicht.Mannschaften.Single(m => m.Name == mannschaft).Zeilen.Select(z => z.Text).ToList();

    private static Staffel Laden(string datei)
    {
        string pfad = Path.Combine(Testdaten.Referenz, "eingabe", datei);
        DatenKnoten stand = ClickTtNachPlanDaten.Laden(pfad);
        string modifikationen = Path.ChangeExtension(pfad, ".modifications");
        if (File.Exists(modifikationen))
        {
            stand.Zusammenfuehren(PlanDatenDatei.Laden(modifikationen));
        }

        return StaffelAbbildung.AusPlanDaten(stand);
    }
}
