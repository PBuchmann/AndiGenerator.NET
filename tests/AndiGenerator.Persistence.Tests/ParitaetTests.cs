// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

/// <summary>
/// Paritätstests (MIGRATIONSPLAN E7): Das Referenzmodell muss für die im Original abgelesenen Pläne (Referenz/werte)
/// dieselben Anzahlen und dieselben angezeigten Kostenwerte liefern.
/// </summary>
public class ParitaetTests
{
    private int verglichen;

    public static TheoryData<string> Referenzfaelle()
    {
        var daten = new TheoryData<string>();
        foreach (string datei in Directory.GetFiles(Path.Combine(Referenzordner(), "werte"), "R*.json").Order(StringComparer.Ordinal))
        {
            daten.Add(Path.GetFileNameWithoutExtension(datei));
        }

        return daten;
    }

    [Theory]
    [MemberData(nameof(Referenzfaelle))]
    public void Kostentabelle_entspricht_dem_Original(string fall)
    {
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Referenzordner(), "werte", fall + ".json")));
        JsonElement erwartet = json.RootElement;
        Planbewertung bewertung = Referenzbewertung.Bewerten(StaffelLaden(fall), Berechnungsoptionen.Standard);
        var fehler = new List<string>();
        int vorher = verglichen;

        foreach (JsonProperty eintrag in erwartet.GetProperty("plan_kosten").EnumerateObject())
        {
            double? wert = eintrag.Name switch
            {
                "Gesamtkosten" => bewertung.Gesamtkosten,
                "ungültige Spiele" => bewertung.UngueltigeSpiele,
                "Spiele nicht terminiert" => bewertung.NichtTerminiert,
                "Spiele an spielfreien Tagen" => bewertung.SpieleAnSpielfreienTagen,
                "Überlappung Spieltage" => bewertung.UeberlappungSpieltage,
                "Länge Spieltage" => bewertung.LaengeSpieltage,
                "Überlappung letzter Spieltag" => bewertung.UeberlappungLetzterSpieltag,
                "Länge letzter Spieltag" => bewertung.LaengeLetzterSpieltag,
                "Vereinsinterne Spiele am Anfang" => bewertung.VereinsinterneSpieleAmAnfang,
                _ => null,
            };
            Vergleichen(fehler, eintrag.Name, eintrag.Value.GetString(), wert is null ? "(unbekannt)" : Kostenanzeige.Kurz(wert.Value));
        }

        string[] spalten = erwartet.GetProperty("spalten").EnumerateArray().Select(s => s.GetString()!).ToArray();
        string[] eigeneSpalten = bewertung.SichtbareKostenarten.Select(a => Referenzbewertung.Kostenartnamen[(int)a]).Append("Gesamt").ToArray();
        Vergleichen(fehler, "Spalten", string.Join(" | ", spalten), string.Join(" | ", eigeneSpalten));

        foreach (JsonProperty zeile in erwartet.GetProperty("mannschaften").EnumerateObject())
        {
            Mannschaftsbewertung? mannschaft = bewertung.Mannschaften.FirstOrDefault(m => m.Name == zeile.Name);
            if (mannschaft is null)
            {
                fehler.Add($"Mannschaft fehlt: {zeile.Name}");
                continue;
            }

            string[] zellen = zeile.Value.EnumerateArray().Select(z => z.GetString()!).ToArray();
            for (int i = 0; i < Math.Min(zellen.Length, bewertung.SichtbareKostenarten.Count); i++)
            {
                MannschaftsKostenart art = bewertung.SichtbareKostenarten[i];
                Vergleichen(fehler, $"{zeile.Name} / {Referenzbewertung.Kostenartnamen[(int)art]}", zellen[i], mannschaft.JeKostenart[(int)art].Anzeige());
            }

            Vergleichen(fehler, $"{zeile.Name} / Gesamt", zellen[^1], Kostenanzeige.Kurz(mannschaft.Gesamt));
        }

        Assert.True(fehler.Count == 0, $"{fall}: {fehler.Count} Abweichung(en):\n" + string.Join("\n", fehler));
        Assert.True(verglichen - vorher >= 30, $"{fall}: nur {verglichen - vorher} Werte verglichen.");
    }

    private static Staffel StaffelLaden(string fall)
    {
        string ordner = Path.Combine(Referenzordner(), "eingabe");
        string xml = Directory.GetFiles(ordner, fall + "_*.xml").Single();
        DatenKnoten stand = ClickTtNachPlanDaten.Laden(xml);
        string modifikationen = Path.ChangeExtension(xml, ".modifications");
        if (File.Exists(modifikationen))
        {
            stand.Zusammenfuehren(PlanDatenDatei.Laden(modifikationen));
        }

        return StaffelAbbildung.AusPlanDaten(stand);
    }

    private static string Referenzordner() => Testdaten.Referenz;

    private void Vergleichen(List<string> fehler, string was, string? erwartet, string ist)
    {
        verglichen++;
        if (erwartet != ist)
        {
            fehler.Add($"{was}: Original „{erwartet}“, neu „{ist}“");
        }
    }
}
