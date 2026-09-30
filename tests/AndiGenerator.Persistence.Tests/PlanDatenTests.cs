// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using AndiGenerator.Persistence.ClickTt;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Kompatibilität der .modifications (MIGRATIONSPLAN E8): Lesen, Zusammenführen, Differenz, Schreiben.</summary>
public class PlanDatenTests
{
    /// <summary>Alle .modifications mit dem click-TT-Export, gegen den sie angewendet werden.</summary>
    /// <returns>Paare aus Dateiname der .modifications und der click-TT-Datei.</returns>
    public static TheoryData<string, string> AlleModifikationen()
    {
        var daten = new TheoryData<string, string>();
        foreach (string datei in Directory.GetFiles(Testdaten.Ordner, "*.modifications").Select(p => Path.GetFileName(p)).Order(StringComparer.Ordinal))
        {
            string xml = datei == "Verbandsoberliga.modifications" ? "Verbandsoberliga (1).xml" : datei[..^".modifications".Length] + ".xml";
            daten.Add(datei, xml);
        }

        return daten;
    }

    [Theory]
    [MemberData(nameof(AlleModifikationen))]
    public void Modifikationen_werden_byte_gleich_zurueckgeschrieben(string modifikationen, string xml)
    {
        _ = xml;
        byte[] original = File.ReadAllBytes(Testdaten.Datei(modifikationen));

        byte[] neu = PlanDatenDatei.ErzeugenBytes(PlanDatenDatei.Laden(Testdaten.Datei(modifikationen)));

        if (!original.AsSpan().SequenceEqual(neu))
        {
            int stelle = Enumerable.Range(0, Math.Min(original.Length, neu.Length)).FirstOrDefault(i => original[i] != neu[i], Math.Min(original.Length, neu.Length));
            Assert.Fail($"Abweichung ab Byte {stelle}: Original …{Ausschnitt(original, stelle)}… / neu …{Ausschnitt(neu, stelle)}…");
        }
    }

    [Theory]
    [MemberData(nameof(AlleModifikationen))]
    public void Differenz_des_Zusammengefuehrten_ergibt_die_Modifikationen(string modifikationen, string xml)
    {
        if (modifikationen == "Verbandsoberliga.modifications")
        {
            return; // gegen einen älteren Exportstand gespeichert (TESTDATEN.md), siehe Stabilitätstest
        }

        DatenKnoten basis = ClickTtNachPlanDaten.Laden(Testdaten.Datei(xml));
        DatenKnoten m = PlanDatenDatei.Laden(Testdaten.Datei(modifikationen));
        DatenKnoten stand = basis.Kopie();
        stand.Zusammenfuehren(m);

        DatenKnoten differenz = DatenKnoten.Differenz(stand, basis);

        Assert.Equal(m.Kanonisch(), differenz.Kanonisch());
    }

    [Theory]
    [MemberData(nameof(AlleModifikationen))]
    public void Erneutes_Zusammenfuehren_der_Differenz_ist_stabil(string modifikationen, string xml)
    {
        DatenKnoten basis = ClickTtNachPlanDaten.Laden(Testdaten.Datei(xml));
        DatenKnoten stand = basis.Kopie();
        stand.Zusammenfuehren(PlanDatenDatei.Laden(Testdaten.Datei(modifikationen)));

        // Speichern und wieder Laden wie im Programm: Differenz schreiben, lesen, erneut auf die Basis anwenden.
        byte[] datei = PlanDatenDatei.ErzeugenBytes(DatenKnoten.Differenz(stand, basis));
        DatenKnoten wieder = basis.Kopie();
        wieder.Zusammenfuehren(PlanDatenDatei.Laden(new MemoryStream(datei)));

        Assert.True(stand.IstGleich(wieder));
        Assert.Equal(stand.Kanonisch(), wieder.Kanonisch());
    }

    [Fact]
    public void Koppeltermin_bekommt_Zweitzeit_und_Prioritaet()
    {
        DatenKnoten plan = AusXml(Mannschaft("""
            <homeGame day="02.09.2026" time="20:00" coupleGame="soft"/>
            <homeGame day="03.09.2026" time="16:59" coupleGame="hard"/>
            <homeGame day="04.09.2026" time="17:00" coupleGame="x"/>
            """));
        List<DatenKnoten> termine = plan.ErstesKind("team")!.KinderMitNamen("homegameday").ToList();

        Assert.Equal(("16:00", "1"), (termine[0].Lesen("couplesecondtime"), termine[0].Lesen("coupleprio")));
        Assert.Equal(("20:59", "2"), (termine[1].Lesen("couplesecondtime"), termine[1].Lesen("coupleprio")));
        Assert.Equal(("13:00", "2"), (termine[2].Lesen("couplesecondtime"), termine[2].Lesen("coupleprio")));
        Assert.All(termine, t => Assert.Equal("true", t.Lesen("couplegameday")));
    }

    [Fact]
    public void Doppelspieltage_nur_bei_noDoubleDays_false_und_nur_vorwaerts()
    {
        const string termine = """
            <homeGame day="04.09.2026" time="20:00"/>
            <homeGame day="05.09.2026" time="18:00"/>
            <homeGame day="06.09.2026" time="10:00"/>
            """;
        DatenKnoten mit = AusXml(Mannschaft(termine, noDoubleDays: "false"));
        DatenKnoten ohne = AusXml(Mannschaft(termine));

        // Fr+Sa werden gepaart; So findet keinen Folgetag und kein unmarkiertes Vortagspaar (Original-Verhalten).
        Assert.Equal(new[] { "true", "true", "false" }, mit.ErstesKind("team")!.KinderMitNamen("homegameday").Select(t => t.Lesen("doublegameday")));
        Assert.All(ohne.ErstesKind("team")!.KinderMitNamen("homegameday"), t => Assert.Equal("false", t.Lesen("doublegameday")));
    }

    [Fact]
    public void Pflichtspieltage_werden_zu_Bereichen_zusammengefasst()
    {
        const string pflichtspieltage = """
            <mandatoryGameDays><mandatoryDay day="15.04.2027"/><mandatoryDay day="13.04.2027"/><mandatoryDay day="14.04.2027"/><mandatoryDay day="20.04.2027"/></mandatoryGameDays>
            """;
        DatenKnoten plan = AusXml(Mannschaft(string.Empty), pflichtspieltage);

        Assert.Equal(
            new[] { "13.04.2027-15.04.2027/1", "20.04.2027-20.04.2027/1" },
            plan.KinderMitNamen("mandatorygames").Select(m => $"{m.Lesen("datefrom")}-{m.Lesen("dateto")}/{m.Lesen("numbergames")}"));
    }

    [Fact]
    public void Aenderungen_an_nicht_mehr_vorhandenen_Knoten_werden_verworfen()
    {
        DatenKnoten basis = AusXml(Mannschaft(string.Empty));
        var m = new DatenKnoten("plan");
        var team = new DatenKnoten("team", m) { Status = KnotenStatus.Geaendert };
        team.Setzen("teamname", "Gibt es nicht");
        team.Setzen("location", "2");

        DatenKnoten stand = basis.Kopie();
        stand.Zusammenfuehren(m);

        Assert.Equal(basis.Kanonisch(), stand.Kanonisch());
    }

    [Fact]
    public void Unbekannter_Knotenname_wird_abgelehnt()
    {
        byte[] datei = Encoding.UTF8.GetBytes("<andigenerator><plan><unbekannt/></plan></andigenerator>");

        Assert.Throws<PlanDatenFormatException>(() => PlanDatenDatei.Laden(new MemoryStream(datei)));
    }

    private static string Mannschaft(string spieltage, string noDoubleDays = "true") => $"""
        <team clubId="1" id="100" number="2" noDoubleDays="{noDoubleDays}"><name> Testverein II </name><gameDays>{spieltage}</gameDays></team>
        """;

    private static DatenKnoten AusXml(string mannschaften, string weiteres = "")
    {
        string xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <TT name="Test" id="1" gender="Erwachsene" from="17.08.2026" until="09.05.2027" mid="07.12.2026"><teams>{mannschaften}</teams>{weiteres}</TT>
            """;
        return ClickTtNachPlanDaten.Erzeugen(ClickTtLeser.Lesen(new MemoryStream(Encoding.UTF8.GetBytes(xml))));
    }

    private static string Ausschnitt(byte[] daten, int stelle) =>
        Encoding.UTF8.GetString(daten, Math.Max(0, stelle - 40), Math.Min(80, daten.Length - Math.Max(0, stelle - 40)));
}
