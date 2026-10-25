// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Schranke;
using AndiGenerator.Persistence.Tests;
using AndiGenerator.Rendering;
using Dock.Model.Core;

namespace AndiGenerator.Presentation.Tests;

/// <summary>
/// Hauptfenster im Automodus mit HiGHS: Schranke im Leitstand und in der Qualitätsansicht, Kriterien ohne Modell. Laufen
/// allein (<see cref="AlleinSammlung"/>), weil HiGHS unter der Last der parallel laufenden Testklassen sonst nicht
/// rechtzeitig fertig wird; auf win-arm64 übersprungen (kein HiGHS).
/// </summary>
[Collection(AlleinSammlung.Name)]
public sealed class HauptfensterHighsTests : IDisposable
{
    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-hf-highs-" + Guid.NewGuid().ToString("N"));
    private readonly TestOberflaeche o = new();
    private readonly HauptfensterViewModel hf;

    public HauptfensterHighsTests()
    {
        Directory.CreateDirectory(Path.Combine(ordner, "daten"));
        string datei = Path.Combine(ordner, "daten", "staffel.xml");
        File.Copy(Path.Combine(Testdaten.Referenz, "datendialoge", "D01_Allgemein.xml"), datei);
        o.DateiZumOeffnen = datei;
        hf = new HauptfensterViewModel(o, () => throw new InvalidOperationException("Keine Fenster im Test"), Path.Combine(ordner, "basis"), null);
    }

    public void Dispose()
    {
        hf.Dispose();
        try
        {
            Directory.Delete(ordner, recursive: true);
        }
        catch (IOException)
        {
            // Aufräumen ist nicht Teil des Tests.
        }
    }

    [HighsFact]
    public async Task Automodus_uebernimmt_die_Schranke_aus_HiGHS_in_die_laufende_Generierung()
    {
        // HiGHS rechnet nebenher; seine Schranke landet im laufenden Automodus und steht im Leitstand.
        await OeffnenAsync();
        hf.AutomodusCommand.Execute(null);
        await hf.GenerierungCommand.ExecuteAsync(null);
        try
        {
            hf.LeitstandCommand.Execute(null);
            LeitstandAnsichtViewModel raum = Assert.Single(Ansichten<LeitstandAnsichtViewModel>());
            for (int i = 0; i < 600 && !raum.Verfahren.Contains("Untere Schranke", StringComparison.Ordinal); i++)
            {
                await Task.Delay(100);
            }

            Assert.Contains("Untere Schranke aus HiGHS (Summe je Stufe): A ≥", raum.Verfahren, StringComparison.Ordinal);
            Assert.Contains(raum.Protokoll, z => z.Contains("Schranke aus HiGHS", StringComparison.Ordinal));
        }
        finally
        {
            hf.StoppenCommand.Execute(null);
        }
    }

    [HighsFact]
    public async Task Automodus_zeigt_die_Schranke_der_Stufe_A()
    {
        // Im Automodus zeigt die Qualitätsansicht die untere Schranke der Stufe A (HiGHS rechnet im Hintergrund).
        await OeffnenAsync();
        hf.QualitaetsansichtCommand.Execute(null);
        QualitaetAnsichtViewModel qualitaet = Assert.Single(Ansichten<QualitaetAnsichtViewModel>());
        qualitaet.Quelle = PlanQuelle.ClickTt;
        Assert.False(qualitaet.ZeigtSchranke);
        Assert.All(qualitaet.Zeilen, z => Assert.Empty(z.Bestmoeglich));

        // Die Meldungen von HiGHS laufen wie auf dem UI-Thread nur hier, nie gleichzeitig mit den Prüfungen (sonst sah der Test
        // gelegentlich eine Ansicht mitten im Erneuern).
        o.Sammeln = true;
        hf.AutomodusCommand.Execute(null);
        Assert.True(qualitaet.ZeigtSchranke);
        for (int i = 0; i < 600 && qualitaet.Zeilen.Any(z => z.BestmoeglichRechnet); i++)
        {
            await Task.Delay(100);
            o.Abarbeiten();
        }

        Assert.True(qualitaet.ZeigtSchranke);
        Assert.DoesNotContain(qualitaet.Zeilen, z => z.BestmoeglichRechnet);

        // Eine Untergrenze 0: Kein Verstoß ist zwingend, kein Haken.
        Assert.All(qualitaet.Zeilen.Where(z => z.Stufe == "A1"), z => Assert.Equal("kein Verstoß zwingend", z.Bestmoeglich));
        Assert.All(qualitaet.Zeilen.Where(z => z.Stufe == "A1"), z => Assert.False(z.BestmoeglichErreicht));
        Assert.All(qualitaet.Zeilen.Where(z => (z.IstA || z.IstB) && z.Kriterium is not null), z => Assert.NotEmpty(z.Bestmoeglich));
        Assert.All(qualitaet.Zeilen.Where(z => z.IstC), z => Assert.Empty(z.Bestmoeglich));

        // Je Stufe eine Kopfzeile über ihrer ersten Zeile: Plan, Grenze und in Klartext, ob das Optimum erreicht ist.
        Assert.Equal(["Stufe A · muss", "Stufe B · soll", "Stufe C · wird geglättet"], qualitaet.Zeilen.Where(z => z.HatKopf).Select(z => z.Kopf!.Titel));
        Stufenkopf a = qualitaet.Zeilen.First(z => z.HatKopf).Kopf!;
        Assert.StartsWith("Grenze ≥ ", a.Grenze, StringComparison.Ordinal);
        Assert.Contains("Optimum", a.Spielraum, StringComparison.Ordinal);

        hf.KostenoptimierungCommand.Execute(null);
        Assert.False(qualitaet.ZeigtSchranke);
        Assert.DoesNotContain(qualitaet.Zeilen, z => z.HatKopf);
    }

    [HighsFact]
    public async Task Kriterien_ohne_Modell_bleiben_in_C()
    {
        // Bei der Doppelrunde kennt HiGHS den Abstand H/A nicht: Die Zeile lässt sich weder mit den Knöpfen noch durch Ziehen
        // nach A oder B bringen – beim Ziehen steht der Grund an der schwebenden Zeile, die Lücke bleibt.
        await OeffnenAsync();
        hf.EinstellungenCommand.Execute(null);
        Assert.Single(Ansichten<OptionenAnsichtViewModel>()).Doppelrunde = true;
        hf.QualitaetsansichtCommand.Execute(null);
        QualitaetAnsichtViewModel qualitaet = Assert.Single(Ansichten<QualitaetAnsichtViewModel>());
        qualitaet.Quelle = PlanQuelle.ClickTt;
        hf.AutomodusCommand.Execute(null);
        for (int i = 0; i < 600 && !qualitaet.Zeilen.Any(z => z.NurC); i++)
        {
            await Task.Delay(100);
        }

        Qualitaetszeile abstand = Assert.Single(qualitaet.Zeilen, z => z.Kriterium == Kostenkriterium.AbstandHeimAuswaerts);
        Assert.True(abstand.NurC);
        Assert.False(abstand.KannAB);
        Assert.False(abstand.EinstufenBCommand.CanExecute(null));
        Assert.Contains("HiGHS kennt", abstand.ErklaerungB, StringComparison.Ordinal);
        Assert.True(Assert.Single(qualitaet.Zeilen, z => z.Kriterium == Kostenkriterium.Sperrtermine).KannAB);

        string vorher = string.Join(",", qualitaet.Zeilen.Select(z => z.Stufe + z.Name));
        Qualitaetszeile b = qualitaet.Zeilen.First(z => z.IstB && z.Kriterium is not null);
        Assert.True(qualitaet.ZiehenBeginnen(abstand));
        qualitaet.ZiehenUeber(b, davor: true);
        Assert.NotNull(qualitaet.Schwebend);
        Assert.True(qualitaet.Schwebend.HatAblehnung);
        Assert.Equal("C", Assert.Single(qualitaet.Zeilen, z => z.Kriterium == Kostenkriterium.AbstandHeimAuswaerts).Klasse);

        // Innerhalb von C geht es weiter: Vorschau ohne Ablehnung.
        Qualitaetszeile c = qualitaet.Zeilen.Last(z => z.IstC && z.Kriterium is not null && z.Kriterium != Kostenkriterium.AbstandHeimAuswaerts);
        qualitaet.ZiehenUeber(c, davor: false);
        Assert.False(qualitaet.Schwebend.HatAblehnung);
        qualitaet.ZiehenBeenden(uebernehmen: false);
        Assert.Equal(vorher, string.Join(",", qualitaet.Zeilen.Select(z => z.Stufe + z.Name)));
    }

    private static void Sammeln<T>(IDockable dockable, List<T> liste)
        where T : class
    {
        if (dockable is T gesucht)
        {
            liste.Add(gesucht);
        }

        if (dockable is IDock dock && dock.VisibleDockables is { } kinder)
        {
            foreach (IDockable kind in kinder)
            {
                Sammeln(kind, liste);
            }
        }
    }

    private async Task OeffnenAsync()
    {
        await hf.OeffnenCommand.ExecuteAsync(null);
        Assert.True(hf.IstGeoeffnet);
        if (hf.Einrichtung is EinrichtungViewModel einrichtung)
        {
            await einrichtung.AbschliessenCommand.ExecuteAsync(null);
        }

        Assert.True(hf.ZeigtArbeitsbereich);
    }

    private List<T> Ansichten<T>()
        where T : class
    {
        var liste = new List<T>();
        Sammeln(hf.Layout, liste);
        return liste;
    }
}
