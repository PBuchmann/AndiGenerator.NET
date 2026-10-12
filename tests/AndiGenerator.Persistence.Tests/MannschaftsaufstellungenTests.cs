// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Aufstellungen;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Mannschaftsmeldung aus click-TT (CSV): Stammspieler der Mannschaft in der Staffel mit ihren TTR-Werten.</summary>
public class MannschaftsaufstellungenTests
{
    // Wie der click-TT-Export: Semikolon, Zeilenende CR, alle Mannschaften der Vereine; Namen sind erfunden.
    private const string Meldung =
        "MeisterschaftName;LigaName;StaffelName;VereinNr;MannschaftName;Aufstellung;SpielerMannschaftNr;SpielerRang;TTR;SpielerName;SpielerGebDatum;SpielerNation;SpielerStatus\r"
        + "M;L;S;001;Verein A;Erwachsene;1;2;1900;X;;GER;\r"
        + "M;L;S;001;Verein A;Erwachsene;1;1;2000;X;;GER;\r"
        + "M;L;S;001;Verein A;Erwachsene;1;3;1950;X;;GER;RES\r"
        + "M;L;S;001;Verein A;Erwachsene;2;1;1500;X;;GER;\r"
        + "M;L;S;002;Verein B II;Erwachsene;1;1;2100;X;;GER;\r"
        + "M;L;S;002;Verein B II;Erwachsene;2;1;1800;X;;GER;SBEM\r"
        + "M;L;S;002;Verein B II;Erwachsene;2;2;1700;X;;GER;\r"
        + "M;L;S;002;Verein B II;Erwachsene;2;3;1600;X;;GER;RES, ab 28.08.\r";

    [Fact]
    public void Nur_Stammspieler_der_eigenen_Mannschaft_nach_Rang()
    {
        IReadOnlyList<Mannschaftsaufstellung> aufstellungen = MannschaftsaufstellungenCsv.Lesen(Meldung);

        Assert.Equal(new[] { "Verein A", "Verein B II" }, aufstellungen.Select(a => a.Mannschaft));
        Assert.Equal([2000, 1900], aufstellungen[0].Stammspieler);
        Assert.Equal([1800, 1700], aufstellungen[1].Stammspieler);
        Assert.Equal(1950.0, aufstellungen[0].Schnitt(4));
        Assert.Equal(2000.0, aufstellungen[0].Schnitt(1));
    }

    [Theory]
    [InlineData("TTC Wirges II", 2)]
    [InlineData("TTF Konz (Z) IV", 4)]
    [InlineData("SV Windhagen", 1)]
    [InlineData("SG Mitte VI", 6)]
    [InlineData("TuS Neun IX", 9)]
    public void Mannschaftsnummer_aus_dem_Namen(string name, int nummer) =>
        Assert.Equal(nummer, MannschaftsaufstellungenCsv.Nummer(name));

    [Fact]
    public void Andere_Dateien_werden_abgelehnt() =>
        Assert.Throws<FormatException>(() => MannschaftsaufstellungenCsv.Lesen("Datum;Heim;Gast\r01.01.2027;A;B\r"));
}
