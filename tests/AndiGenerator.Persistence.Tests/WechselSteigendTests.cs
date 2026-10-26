// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Persistence.ClickTt;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

/// <summary>
/// Der Wechsel H/A zählt steigend (Peter, 29.10.2026): Heim–Heim 1, drei in Folge 3, vier 6 – als Zuschlag zur Anzahl des
/// Originals (Automodus, einfacher Modus, Qualitätsansicht); die Kostentabelle bleibt wie im Original. Die Heim/Auswärts-Bilanz
/// ist entfernt; gespeicherte Einteilungen, die sie nennen, bleiben lesbar.
/// </summary>
public sealed class WechselSteigendTests
{
    [Theory]
    [InlineData("R3_Damen_Bezirksoberliga_Rheinhessen (1).xml", false)]
    [InlineData("R04_1__Kreisklasse_Gruppe_A (1).xml", true)]
    [InlineData("R07_2__Kreisklasse_Gruppe_A (1).xml", false)]
    [InlineData("R10_3__Kreisklasse (11).xml", true)]
    [InlineData("R11_4__Kreisklasse (5).xml", true)]
    [InlineData("R14_Bezirksliga_Rheinhessen_Nord (1).xml", true)]
    public void Der_Zuschlag_macht_aus_jeder_Folge_die_steigende_Zaehlung(string datei, bool mitFolgenAbDrei)
    {
        // Je Mannschaft die Spiele ihres Plans nach Datum, Folgen gleicher Art.
        Staffel staffel = Laden(datei);
        Planbewertung b = Referenzbewertung.Bewerten(staffel, Berechnungsoptionen.Standard);
        int einfach = b.Mannschaften.Sum(m => m.JeKostenart[(int)MannschaftsKostenart.WechselHeimAuswaerts].Anzahl);
        int steigend = 0;
        int ohneZuschlag = 0;
        foreach (Mannschaft m in staffel.Mannschaften)
        {
            List<bool> folge = [.. staffel.BestehenderSpielplan
                .Where(s => s.Zeitpunkt is not null && (s.Heim == m.Name || s.Gast == m.Name))
                .OrderBy(s => s.Zeitpunkt)
                .Select(s => s.Heim == m.Name)];
            int laenge = 1;
            for (int i = 1; i < folge.Count; i++)
            {
                laenge = folge[i] == folge[i - 1] ? laenge + 1 : 1;
                steigend += laenge - 1;
                ohneZuschlag += laenge > 1 ? 1 : 0;
            }
        }

        Assert.Equal(ohneZuschlag, einfach);
        Assert.Equal(mitFolgenAbDrei, steigend > einfach);
        Assert.Equal(steigend - einfach, b.Zusatz!.Wechselzuschlag!.Sum());
        (double[] anzahl, _) = Stufeneinteilung.Anzahlen(b);
        Assert.Equal(steigend, anzahl[(int)Kostenkriterium.WechselHeimAuswaerts]);
    }

    [Fact]
    public void Gespeicherte_Einteilungen_mit_der_Bilanz_bleiben_lesbar()
    {
        Stufeneinteilung gelesen = Stufeneinteilung.AusText("A:Hallenbelegung;B:Sperrtermine,HeimAuswaertsBilanz;C:WechselHeimAuswaerts");

        Assert.Equal([Kostenkriterium.Hallenbelegung], gelesen.A);
        Assert.Equal([Kostenkriterium.Sperrtermine], gelesen.B);
        Assert.Equal(Kostenkriterium.WechselHeimAuswaerts, gelesen.C[0]);
        Assert.DoesNotContain("Bilanz", Stufeneinteilung.Standard.Text(), StringComparison.Ordinal);
    }

    private static Staffel Laden(string datei)
    {
        string pfad = Path.Combine(Testdaten.Referenz, "eingabe", datei);
        DatenKnoten stand = ClickTtNachPlanDaten.Laden(pfad);
        return StaffelAbbildung.AusPlanDaten(stand);
    }
}
