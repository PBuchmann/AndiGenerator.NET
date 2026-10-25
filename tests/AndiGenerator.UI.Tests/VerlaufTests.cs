// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Media.Imaging;

namespace AndiGenerator.UI.Tests;

[Collection(OberflaechenSammlung.Name)]
public sealed class VerlaufTests
{
    private readonly Oberflaeche oberflaeche;

    public VerlaufTests(Oberflaeche oberflaeche)
    {
        this.oberflaeche = oberflaeche;
    }

    [Fact]
    public Task Verlauf_mit_Phasenwechsel_in_derselben_Sekunde_wird_gezeichnet() => oberflaeche.Ausfuehren(() =>
    {
        // Der erste Punkt nach der Basisoptimierung liegt in derselben Sekunde wie ihr letzter (früher: Ausnahme beim Zeichnen).
        var verlauf = new Suchverlauf();
        verlauf.Hinzufuegen(Stand("Basisoptimierung"), 3);
        verlauf.Hinzufuegen(Stand("Basisoptimierung"), 5);
        verlauf.Hinzufuegen(Stand("Optimierung Stufe A"), 5);

        using var bild = new RenderTargetBitmap(new PixelSize(900, 500));
        using (var context = bild.CreateDrawingContext())
        {
            new Verlaufszeichnung(context, verlauf, new Rect(0, 0, 900, 500)).Zeichnen(null);
        }

        Assert.Equal(3, verlauf.Punkte.Count);
        return Task.CompletedTask;
    });

    private static Optimierungsstand Stand(string phase)
    {
        var lenkung = new Lenkungsstand(null, new Stufenwert(0, 12, 30, 0, 300, 0), null, string.Empty, phase, 0, 0, Berechnungsoptionen.Standard, []);
        var raum = new Maschinenraum([], [], 0, null, 0, [new(Kostenkriterium.ParalleleSpiele, 12, Stufe.A)], 1);
        return new Optimierungsstand(true, false, 100, 10, 1000, 1, TimeSpan.Zero, null, [], lenkung, raum);
    }
}
