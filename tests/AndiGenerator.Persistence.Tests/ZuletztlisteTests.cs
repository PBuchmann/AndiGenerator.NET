// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Liste der zuletzt geöffneten Staffeln für die Startseite.</summary>
public sealed class ZuletztlisteTests : IDisposable
{
    private readonly string basis = Path.Combine(Path.GetTempPath(), "andigen-zuletzt-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(basis))
        {
            Directory.Delete(basis, recursive: true);
        }
    }

    [Fact]
    public void Neueste_zuerst_ohne_Doppelte_und_hoechstens_acht()
    {
        Assert.Empty(Zuletztliste.Laden(basis));
        var zeit = new DateTime(2026, 9, 29, 14, 32, 0, DateTimeKind.Local);
        for (int i = 1; i <= 10; i++)
        {
            Zuletztliste.Hinzufuegen(basis, new ZuletztGeoeffnet($@"C:\Staffeln\s{i}.xml", "Staffel " + i, zeit.AddMinutes(i)));
        }

        IReadOnlyList<ZuletztGeoeffnet> liste = Zuletztliste.Hinzufuegen(basis, new ZuletztGeoeffnet(@"c:\staffeln\S5.XML", "Staffel\t5\nneu", zeit.AddHours(1)));

        Assert.Equal(Zuletztliste.Hoechstzahl, liste.Count);
        Assert.Equal("Staffel\t5\nneu", liste[0].Name);
        Assert.Equal(["Staffel 5 neu", "Staffel 10", "Staffel 9"], Zuletztliste.Laden(basis).Take(3).Select(e => e.Name));
        Assert.Equal(zeit.AddHours(1), Zuletztliste.Laden(basis)[0].Zeitpunkt);
        Assert.DoesNotContain(Zuletztliste.Laden(basis), e => e.Name == "Staffel 1");
    }

    [Fact]
    public void Entfernen_nimmt_nur_den_Eintrag_aus_der_Liste()
    {
        var zeit = new DateTime(2026, 9, 29, 14, 32, 0, DateTimeKind.Local);
        Zuletztliste.Hinzufuegen(basis, new ZuletztGeoeffnet(@"C:\Staffeln\a.xml", "A", zeit));
        Zuletztliste.Hinzufuegen(basis, new ZuletztGeoeffnet(@"C:\Staffeln\b.xml", "B", zeit.AddMinutes(1)));

        IReadOnlyList<ZuletztGeoeffnet> liste = Zuletztliste.Entfernen(basis, @"c:\staffeln\A.XML");

        Assert.Equal(["B"], liste.Select(e => e.Name));
        Assert.Equal(["B"], Zuletztliste.Laden(basis).Select(e => e.Name));
        Assert.Equal(["B"], Zuletztliste.Entfernen(basis, @"C:\gibt\es\nicht.xml").Select(e => e.Name));
    }

    [Fact]
    public void Ungueltige_Zeilen_werden_uebergangen()
    {
        Directory.CreateDirectory(basis);
        File.WriteAllLines(Path.Combine(basis, Zuletztliste.Dateiname), ["kaputt", "2026-09-29T10:00:00\tA\t", "kein Datum\tB\tC:\\b.xml", "2026-09-29T10:00:00.0000000\tC\tC:\\c.xml"]);

        ZuletztGeoeffnet eintrag = Assert.Single(Zuletztliste.Laden(basis));
        Assert.Equal("C", eintrag.Name);
        Assert.Equal(@"C:\c.xml", eintrag.Pfad);
    }
}
