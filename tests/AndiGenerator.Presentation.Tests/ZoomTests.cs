// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation.Tests;

/// <summary>Vergrößerung der Ansichten.</summary>
public sealed class ZoomTests
{
    [Fact]
    public void Stufen_wechseln_und_bleiben_in_den_Grenzen()
    {
        var zoom = new Zoom();
        var geaendert = new List<string?>();
        zoom.PropertyChanged += (_, e) => geaendert.Add(e.PropertyName);
        Assert.Equal(100, zoom.Prozent);
        Assert.Equal(1.0, zoom.Faktor);
        Assert.Equal("100 %", zoom.Text);

        zoom.Groesser();
        Assert.Equal(110, zoom.Prozent);
        Assert.Equal(1.1, zoom.Faktor, 6);
        Assert.Equal(["Prozent", "Faktor", "Schieber", "Text"], geaendert);

        zoom.Prozent = 115;
        zoom.Groesser();
        Assert.Equal(125, zoom.Prozent);
        zoom.Prozent = 105;
        zoom.Kleiner();
        Assert.Equal(100, zoom.Prozent);

        zoom.Prozent = 1000;
        Assert.Equal(Zoom.Maximum, zoom.Prozent);
        zoom.Groesser();
        Assert.Equal(Zoom.Maximum, zoom.Prozent);
        zoom.Prozent = 1;
        Assert.Equal(Zoom.Minimum, zoom.Prozent);
        zoom.Kleiner();
        Assert.Equal(Zoom.Minimum, zoom.Prozent);

        zoom.Zuruecksetzen();
        Assert.Equal(100, zoom.Prozent);
    }

    [Fact]
    public void Schieberegler_stellt_in_5_Prozent_Schritten_ein()
    {
        var zoom = new Zoom { Schieber = 117.4 };
        Assert.Equal(115, zoom.Prozent);
        Assert.Equal(115.0, zoom.Schieber);
        zoom.Schieber = 117.5;
        Assert.Equal(120, zoom.Prozent);
        Assert.Equal("120 %", zoom.Text);
        zoom.Schieber = 10;
        Assert.Equal(Zoom.Minimum, zoom.Prozent);
    }

    [Fact]
    public void Startzoom_passt_sich_dem_Platz_an()
    {
        Assert.Equal(100, Zoom.Vorschlag(Zoom.Bezugsbreite, Zoom.Bezugshoehe));
        Assert.Equal(120, Zoom.Vorschlag(1590, 720));
        Assert.Equal(80, Zoom.Vorschlag(960, 600));
        Assert.Equal(75, Zoom.Vorschlag(400, 300));
        Assert.Equal(175, Zoom.Vorschlag(3500, 2000));
        Assert.Equal(100, Zoom.Vorschlag(0, 500));

        var o = new TestOberflaeche();
        string basis = Path.Combine(Path.GetTempPath(), "andigen-zoom-" + Guid.NewGuid().ToString("N"));
        using var hf = new HauptfensterViewModel(o, () => throw new InvalidOperationException("Keine Fenster im Test"), basis, null);
        var dokumente = (Dock.Model.Core.IDock)hf.Layout.ActiveDockable!;
        IZoombar kosten = Assert.IsAssignableFrom<IZoombar>(dokumente.ActiveDockable);

        hf.StartzoomFestlegen(130);

        Assert.Equal(130, hf.Startzoom);
        Assert.Equal(130, kosten.Zoom.Prozent);
        hf.StartzoomFestlegen(1000);
        Assert.Equal(Zoom.Maximum, hf.Startzoom);
    }

    [Fact]
    public void Ausgabeansichten_haben_je_eine_eigene_Vergroesserung()
    {
        var o = new TestOberflaeche();
        string basis = Path.Combine(Path.GetTempPath(), "andigen-zoom-" + Guid.NewGuid().ToString("N"));
        using var hf = new HauptfensterViewModel(o, () => throw new InvalidOperationException("Keine Fenster im Test"), basis, null);
        IZoombar kosten = new KostenAnsichtViewModel(hf, "k");
        IZoombar plan = new TerminplanAnsichtViewModel(hf, "t");
        IZoombar wuensche = new TerminwunschAnsichtViewModel(hf);
        IZoombar nachbarn = new NachbarterminAnsichtViewModel(hf);

        kosten.Zoom.Groesser();

        Assert.Equal(110, kosten.Zoom.Prozent);
        Assert.All(new[] { plan, wuensche, nachbarn }, a => Assert.Equal(100, a.Zoom.Prozent));
    }

    [Fact]
    public void Tastenbefehle_wirken_auf_die_aktive_Ansicht()
    {
        var o = new TestOberflaeche();
        string basis = Path.Combine(Path.GetTempPath(), "andigen-zoom-" + Guid.NewGuid().ToString("N"));
        using var hf = new HauptfensterViewModel(o, () => throw new InvalidOperationException("Keine Fenster im Test"), basis, null);
        var dokumente = (Dock.Model.Core.IDock)hf.Layout.ActiveDockable!;
        IZoombar aktiv = Assert.IsAssignableFrom<IZoombar>(dokumente.ActiveDockable);

        hf.ZoomGroesserCommand.Execute(null);
        hf.ZoomGroesserCommand.Execute(null);
        Assert.Equal(125, aktiv.Zoom.Prozent);
        hf.ZoomKleinerCommand.Execute(null);
        Assert.Equal(110, aktiv.Zoom.Prozent);
        hf.ZoomZuruecksetzenCommand.Execute(null);
        Assert.Equal(100, aktiv.Zoom.Prozent);
    }
}
