// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation.Tests;

/// <summary>Dialog „Über“: Urheber, Rechtshinweise nach GPL-3.0 Abschnitt 5d und Version.</summary>
public sealed class UeberTests
{
    [Fact]
    public async Task Ueber_nennt_Urheber_Lizenz_und_oeffnet_den_Lizenztext()
    {
        var o = new TestOberflaeche();
        var ueber = new UeberViewModel(o, "1.2.3");

        Assert.Equal("Version 1.2.3", ueber.Version);
        Assert.Contains("Andreas Hofmann", UeberViewModel.Original, StringComparison.Ordinal);
        Assert.Contains("Peter Buchmann", UeberViewModel.Portierung, StringComparison.Ordinal);
        Assert.Contains("Claude", UeberViewModel.Entstehung, StringComparison.Ordinal);
        Assert.Contains("GNU General Public License, Version 3", UeberViewModel.Lizenzhinweis, StringComparison.Ordinal);
        Assert.Contains("OHNE JEDE GEWÄHRLEISTUNG", UeberViewModel.Lizenzhinweis, StringComparison.Ordinal);
        Assert.NotEmpty(UeberViewModel.Programm);
        Assert.NotEmpty(UeberViewModel.Beschreibung);
        Assert.Contains("SkiaSharp", UeberViewModel.Drittbibliotheken, StringComparison.Ordinal);

        await ueber.LizenzAnzeigenCommand.ExecuteAsync(null);

        Assert.Equal(new Uri(UeberViewModel.Lizenzadresse), Assert.Single(o.Adressen));
    }

    [Fact]
    public void Version_ohne_Quelltextstand()
    {
        string version = UeberViewModel.VersionVon(typeof(UeberViewModel).Assembly);
        Assert.DoesNotContain('+', version);
        Assert.Matches(@"^\d+\.\d+", version);
    }

    [Fact]
    public async Task Hauptfenster_zeigt_Ueber()
    {
        var o = new TestOberflaeche();
        string basis = Path.Combine(Path.GetTempPath(), "andigen-ueber-" + Guid.NewGuid().ToString("N"));
        using var hf = new HauptfensterViewModel(o, () => throw new InvalidOperationException("Keine Fenster im Test"), basis, null);

        await hf.UeberCommand.ExecuteAsync(null);

        Assert.Equal(["Über"], o.Aufrufe);
        Assert.StartsWith("Version ", o.Ueber!.Version, StringComparison.Ordinal);
    }
}
