// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Engine.Einfach;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Turniere;
using AndiGenerator.Persistence.Tests;

namespace AndiGenerator.Presentation.Tests;

/// <summary>
/// Normaler Modus: Wahl des Modus bleibt erhalten; Dateien → Planung → Ergebnis mit kurzer Rechenzeit. Die Planung läuft
/// allein (<see cref="AlleinSammlung"/>), weil HiGHS unter Last sonst kaum zum Zug kommt.
/// </summary>
[Collection(AlleinSammlung.Name)]
public sealed class NormalmodusTests : IDisposable
{
    private const string R2 = "R2_4__Kreisklasse_Gruppe_A (2)";

    private static readonly Schnellplanereinstellungen Kurz = Schnellplanereinstellungen.Standard with
    {
        Kerne = 2,
        PhaseEinsHoechstens = TimeSpan.FromSeconds(10),
        Vorglaetten = TimeSpan.FromSeconds(2),
        ErsteRunde = TimeSpan.FromSeconds(2),
        LaengsteRunde = TimeSpan.FromSeconds(4),
        Laufzeit = new Laufzeitregel(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(1)),
    };

    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-normalmodus-" + Guid.NewGuid().ToString("N"));
    private readonly TestOberflaeche o = new();

    public NormalmodusTests()
    {
        Directory.CreateDirectory(Path.Combine(ordner, "daten"));
        string datei = Path.Combine(ordner, "daten", "staffel.xml");
        File.Copy(Path.Combine(Testdaten.Referenz, "eingabe", R2 + ".xml"), datei);
        o.DateiZumOeffnen = datei;
    }

    private string Basis => Path.Combine(ordner, "basis");

    public void Dispose()
    {
        try
        {
            Directory.Delete(ordner, recursive: true);
        }
        catch (IOException)
        {
            // Aufräumen ist nicht Teil des Tests.
        }
    }

    [Fact]
    public void Gewaehlter_Modus_bleibt_beim_naechsten_Start()
    {
        using (HauptfensterViewModel hf = Neu())
        {
            Assert.True(hf.IstNormal);
            Assert.True(hf.ZeigtNormalAnsicht);
            Assert.False(hf.NormalCommand.CanExecute(null));
            hf.ExperteCommand.Execute(null);
            Assert.True(hf.IstExperte);
            Assert.False(hf.ZeigtNormalAnsicht);
        }

        Assert.True(Programmeinstellungen.Laden(Basis).Expertenmodus);
        using (HauptfensterViewModel hf = Neu())
        {
            Assert.True(hf.IstExperte);
            hf.NormalCommand.Execute(null);
            Assert.True(hf.IstNormal);
            Assert.Equal(Normalseite.Start, hf.Normal.Seite);
        }

        Assert.False(Programmeinstellungen.Laden(Basis).Expertenmodus);
    }

    [HighsFact]
    public async Task Dateien_Planung_Ergebnis_und_der_beste_Plan_bleibt()
    {
        using HauptfensterViewModel hf = Neu();
        NormalmodusViewModel n = hf.Normal;
        n.Planeinstellungen = Kurz;
        Assert.False(n.PlanErstellenCommand.CanExecute(null));

        await n.OeffnenCommand.ExecuteAsync(null);
        Assert.True(n.IstGeoeffnet);
        Assert.True(n.Dateien[0].Geladen);
        Assert.False(n.HatHinderung);
        Assert.Equal("gesamte Runde", hf.Rundentext);

        await n.PlanErstellenCommand.ExecuteAsync(null);
        Assert.True(n.Laeuft);
        Assert.Equal(Normalseite.Planung, n.Seite);
        Assert.False(hf.ExperteCommand.CanExecute(null));
        for (int i = 0; i < 300 && n.Gruppen.Count == 0; i++)
        {
            await Task.Delay(100);
        }

        Assert.NotEmpty(n.Gruppen);
        Assert.NotEmpty(n.Urteil);

        // Wichtigkeit je Kriterium: nur im Feinschliff einstellbar; ein Klick wirkt sofort auf die Balken.
        Normalbalken einstellbar = n.Gruppen.SelectMany(g => g.Zeilen).First(z => z.Einstellbar);
        Assert.Equal(Stufe.C, einstellbar.Stufe);
        Assert.True(einstellbar.NormalAktiv);
        Assert.DoesNotContain(n.Gruppen.Where(g => g.Stufe != Stufe.C).SelectMany(g => g.Zeilen), z => z.Einstellbar);
        Kostenkriterium gewaehlt = einstellbar.Kriterium!.Value;
        n.WichtigkeitCommand.Execute(einstellbar.HochParameter);
        Assert.Equal(Kriterienwichtigkeit.Hoch, n.WichtigkeitVon(gewaehlt));
        for (int i = 0; i < 100 && !n.Gruppen.SelectMany(g => g.Zeilen).Any(z => z.Kriterium == gewaehlt && z.HochAktiv); i++)
        {
            await Task.Delay(100);
        }

        Assert.Contains(n.Gruppen.SelectMany(g => g.Zeilen), z => z.Kriterium == gewaehlt && z.HochAktiv);
        n.WichtigkeitCommand.Execute(einstellbar.NormalParameter);
        Assert.Equal(Kriterienwichtigkeit.Normal, n.WichtigkeitVon(gewaehlt));

        // Terminplan und Diagramme zeigen schon während der Planung den besten Plan.
        n.ReiterCommand.Execute("1");
        for (int i = 0; i < 300 && (n.Terminplan is null || n.Diagramme is null); i++)
        {
            await Task.Delay(100);
        }

        Assert.NotNull(n.Terminplan);
        Assert.NotNull(n.Diagramme);
        Assert.True(n.ZeigtTerminplan);
        n.ReiterCommand.Execute("0");

        // Pause hält die Suche an und sichert den besten Plan; der Verlauf bleibt, „Weiter“ rechnet wieder.
        await n.PauseCommand.ExecuteAsync(null);
        Assert.True(n.Pausiert);
        Assert.False(n.Rechnet);
        Assert.True(n.Laeuft);
        Assert.Equal("Weiter", n.PauseText);
        Assert.Equal(Normalseite.Planung, n.Seite);
        Assert.False(hf.ExperteCommand.CanExecute(null));
        Assert.NotEmpty(Directory.GetFiles(Basis, Normalplanung.BesterPlanName + ".*", SearchOption.AllDirectories));
        int schritte = n.Verlauf.Count;
        await n.PauseCommand.ExecuteAsync(null);
        Assert.True(n.Rechnet);
        Assert.False(n.Pausiert);
        Assert.True(n.Verlauf.Count >= schritte);

        // Neu starten beginnt von vorn: der Verlauf ist leer, die Planung läuft.
        await n.NeuStartenCommand.ExecuteAsync(null);
        Assert.True(n.Rechnet);
        Assert.Empty(n.Verlauf);
        for (int i = 0; i < 300 && n.Gruppen.Count == 0; i++)
        {
            await Task.Delay(100);
        }

        await n.FertigCommand.ExecuteAsync(null);
        Assert.False(n.Laeuft);
        Assert.Equal(Normalseite.Ergebnis, n.Seite);
        Assert.NotEmpty(n.FazitTitel);
        Assert.NotNull(n.Terminplan);
        Assert.NotNull(n.Diagramme);
        Assert.True(n.ExportierenCommand.CanExecute(null));
        string bester = Assert.Single(Directory.GetFiles(Basis, Normalplanung.BesterPlanName + ".*", SearchOption.AllDirectories));

        // Nochmal planen: der beste Plan bleibt (er wird nur durch einen besseren ersetzt).
        await n.PlanErstellenCommand.ExecuteAsync(null);
        await Task.Delay(1500);
        await n.FertigCommand.ExecuteAsync(null);
        Assert.Equal(Normalseite.Ergebnis, n.Seite);
        Assert.True(File.Exists(bester));
    }

    private HauptfensterViewModel Neu() => new(o, () => throw new InvalidOperationException("Keine Fenster im Test"), Basis, null);
}
