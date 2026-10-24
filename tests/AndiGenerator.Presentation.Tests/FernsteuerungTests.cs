// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using AndiGenerator.Mcp;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AndiGenerator.Presentation.Tests;

/// <summary>
/// MCP-Server (tools/AndiGenerator.Mcp): Die Werkzeuge steuern das Hauptfenster-Modell ohne Fenster – Staffel öffnen,
/// Einrichtung, Generierung, Qualität, Merken, Export und Kompaktspieltage wie in der Oberfläche.
/// </summary>
public sealed class FernsteuerungTests : IDisposable
{
    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-mcp-" + Guid.NewGuid().ToString("N"));
    private readonly Fernsteuerung s;
    private readonly Werkzeuge w;

    public FernsteuerungTests()
    {
        Directory.CreateDirectory(Path.Combine(ordner, "daten"));
        s = new Fernsteuerung(Path.Combine(ordner, "basis"), null);
        w = new Werkzeuge(s);
    }

    public void Dispose()
    {
        s.Dispose();
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
    public void Jedes_Werkzeug_hat_einen_eindeutigen_Namen_und_eine_Beschreibung()
    {
        List<MethodInfo> methoden = typeof(Werkzeuge).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).ToList();
        List<string> namen = methoden.Select(m => Assert.IsType<McpServerToolAttribute>(m.GetCustomAttribute<McpServerToolAttribute>()).Name!).ToList();

        Assert.Equal(namen.Count, namen.Distinct().Count());
        Assert.All(namen, n => Assert.Matches("^[a-z_]+$", n));
        Assert.All(methoden, m => Assert.False(string.IsNullOrWhiteSpace(m.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description), m.Name));
        Assert.Contains("staffel_oeffnen", namen);
        Assert.Contains("schritt_fuer_schritt", namen);
    }

    /// <summary>Der Server über das echte Protokoll (stdio): Anmeldung, Liste der Werkzeuge, ein Aufruf.</summary>
    /// <returns>Der Testlauf.</returns>
    [Fact]
    public async Task Server_antwortet_ueber_stdio()
    {
        string dll = typeof(Werkzeuge).Assembly.Location;
        string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } pfad ? pfad : "dotnet";
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "andigen-mcp",
            Command = dotnet,
            Arguments = [dll, "--basis", Path.Combine(ordner, "basis-stdio")],
        });
        await using McpClient client = await McpClient.CreateAsync(transport);

        Assert.Equal("andigen-mcp", client.ServerInfo.Name);
        Assert.Contains("click-TT", client.ServerInstructions, StringComparison.Ordinal);
        IList<McpClientTool> werkzeuge = await client.ListToolsAsync();
        Assert.Contains(werkzeuge, t => t.Name == "staffel_oeffnen");
        CallToolResult antwort = await client.CallToolAsync("status");
        Assert.NotEqual(true, antwort.IsError);
        string text = Assert.IsType<TextContentBlock>(Assert.Single(antwort.Content)).Text;
        Assert.Contains("\"laeuft\":false", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Staffel_sagen_die_Werkzeuge_was_fehlt()
    {
        Stand stand = await w.Status();
        Assert.Equal(string.Empty, stand.Staffel);
        Assert.False(stand.Laeuft);

        Aktionsergebnis falsch = await w.StaffelOeffnen(Path.Combine(ordner, "gibt-es-nicht.xml"));
        Assert.False(falsch.Erfolg);
        Assert.Contains("keine vorhandene XML-Datei", falsch.Text, StringComparison.Ordinal);
        Assert.False((await w.Starten()).Erfolg);
        Assert.Contains("keine Staffel", (await w.VerfahrenWaehlen("automodus")).Text, StringComparison.Ordinal);
        Assert.Null(await w.Qualitaet());
        Assert.Null(await w.Kompakt());
        Assert.Empty(await w.Einrichtung());
        Assert.False((await w.Stoppen()).Erfolg);
        Assert.False((await w.Anhalten()).Erfolg);
        Assert.False((await w.PlanMerken("x")).Erfolg);
        Assert.False((await w.CsvExportieren(Path.Combine(ordner, "plan.txt"))).Erfolg);
    }

    [Fact]
    public async Task Normale_Runde_einrichten_rechnen_bewerten_merken_und_exportieren()
    {
        string datei = Path.Combine(ordner, "daten", "staffel.xml");
        File.Copy(Path.Combine(Testdaten.Referenz, "datendialoge", "D01_Allgemein.xml"), datei);

        Aktionsergebnis geoeffnet = await w.StaffelOeffnen(datei);
        Assert.True(geoeffnet.Erfolg, geoeffnet.Text);
        Assert.Equal("4. Kreisklasse Gruppe A", geoeffnet.Stand.Staffel);
        Assert.True(geoeffnet.Stand.Einrichtung);
        Assert.Contains("Erst die Einrichtung", (await w.Starten()).Text, StringComparison.Ordinal);

        IReadOnlyList<Einrichtungspunkt> punkte = await w.Einrichtung();
        Einrichtungspunkt runde = Assert.Single(punkte, p => p.Titel == "Rundenplanung");
        Assert.False((await w.EinrichtungWaehlen(runde.Nummer, "gibt es nicht")).Erfolg);
        Assert.True((await w.EinrichtungWaehlen(runde.Nummer, "Die ganze Runde")).Erfolg);
        Assert.False((await w.EinrichtungWaehlen(99, "x")).Erfolg);
        Assert.True((await w.EinrichtungAbschliessen()).Erfolg);
        Assert.True((await w.EinrichtungAbschliessen()).Erfolg);

        Assert.False((await w.StartplanWaehlen("gibt es nicht")).Erfolg);
        Assert.True((await w.StartplanWaehlen("in click-TT vorhandenem Plan")).Erfolg);
        Aktionsergebnis verfahren = await w.VerfahrenWaehlen("kostenoptimierung");
        Assert.Equal("Kostenoptimierung", verfahren.Stand.Verfahren);
        Assert.StartsWith("Schritt für Schritt: Kostenoptimierung", verfahren.Stand.NaechsterSchritt, StringComparison.Ordinal);

        Assert.True((await w.Starten()).Erfolg);
        Assert.False((await w.Starten()).Erfolg);
        Stand laeuft = await w.Warten(20);
        for (int i = 0; i < 10 && laeuft.Ergebnis is not { Verbesserungen: not "–" and not "0" }; i++)
        {
            laeuft = await w.Warten(5);
        }

        Assert.True(laeuft.Laeuft);
        Assert.NotNull(laeuft.Ergebnis);
        Assert.True((await w.Anhalten()).Erfolg);
        Assert.True((await w.Status()).Angehalten);
        Assert.Equal("Ist schon angehalten.", (await w.Anhalten()).Text);
        Assert.True((await w.Fortsetzen()).Erfolg);

        Qualitaet q = Assert.IsType<Qualitaet>(await w.Qualitaet());
        Assert.NotEmpty(q.Kriterien);
        Assert.Contains(q.Kriterien, k => k.Kriterium == "Sperrtermine");
        Verstossliste v = Assert.IsType<Verstossliste>(await w.Verstoesse("Sperrtermine"));
        Assert.EndsWith("Sperrtermine", v.Titel, StringComparison.Ordinal);
        Assert.Null(await w.Verstoesse("gibt es nicht"));

        // Einstufen geht nur im Automodus.
        Assert.False((await w.Einstufen("Sperrtermine", "A")).Erfolg);
        Assert.True((await w.VerfahrenWaehlen("automodus")).Erfolg);
        Assert.False((await w.Einstufen("Sperrtermine", "X")).Erfolg);
        Aktionsergebnis eingestuft = await w.Einstufen("Sperrtermine", "A");
        Assert.True(eingestuft.Erfolg, eingestuft.Text);
        Assert.StartsWith("A", (await w.Qualitaet())!.Kriterien.Single(k => k.Kriterium == "Sperrtermine").Stufe, StringComparison.Ordinal);
        Assert.False((await w.FestlegungAufheben()).Erfolg);
        Assert.False((await w.BVarianteFestlegen("gibt es nicht")).Erfolg);

        Assert.True((await w.Stoppen()).Erfolg);
        Assert.False((await w.PlanMerken(" ")).Erfolg);
        Assert.True((await w.PlanMerken("Von der KI")).Erfolg);
        Assert.Contains((await w.Status()).Startplaene, p => p.Contains("Von der KI", StringComparison.Ordinal));

        Aktionsergebnis export = await w.CsvExportieren(Path.Combine(ordner, "plan.csv"), trotzHinweisen: true);
        Assert.True(export.Erfolg, export.Text);
        Assert.True(File.Exists(Path.Combine(ordner, "plan.csv")));

        Schrittfolge schritte = await w.SchrittFuerSchritt();
        Assert.Equal("Schritt für Schritt: Automodus", schritte.Titel);
        Assert.Equal(Fuehrungen.Schritte(Fuehrungsart.Automodus).Count, schritte.Schritte.Count);

        Assert.True((await w.StaffelSchliessen()).Erfolg);
        Assert.Equal(string.Empty, (await w.Status()).Staffel);
    }

    [Fact]
    public async Task Gewichtungen_lesen_und_aendern()
    {
        await NormaleRundeOeffnenAsync();
        IReadOnlyList<Gewichtungsstand> alle = await w.Gewichtungen();
        Assert.Contains(alle, g => g.Ebene == "Kostenart" && g.Kostenart == "Sperrtermine" && g.Stufe == "normal");
        Assert.Contains(alle, g => g.Ebene == "Plan");
        Assert.Contains(alle, g => g.Ebene == "Mannschaft" && g.Mannschaft == Namen.WsvWorms);

        Aktionsergebnis hoch = await w.GewichtungSetzen("hoch", kostenart: "sperrtermine");
        Assert.True(hoch.Erfolg, hoch.Text);
        Assert.Equal("sperrtermine: hoch (vorher normal).", hoch.Text);
        Assert.True((await w.GewichtungSetzen("aus", mannschaft: Namen.WsvWorms)).Erfolg);
        Assert.True((await w.GewichtungSetzen("sehr hoch", "Sperrtermine", Namen.WsvWorms)).Erfolg);
        IReadOnlyList<Gewichtungsstand> geaendert = await w.Gewichtungen(nurGeaenderte: true);
        Assert.Equal(3, geaendert.Count);
        Assert.Contains(geaendert, g => g.Ebene == "Mannschaft und Kostenart" && g.Stufe == "sehr hoch");
        Assert.Contains(geaendert, g => g.Ebene == "Mannschaft" && g.Stufe == "Nicht berücksichtigen");

        Assert.False((await w.GewichtungSetzen("riesig", "Sperrtermine")).Erfolg);
        Assert.False((await w.GewichtungSetzen("hoch", "gibt es nicht")).Erfolg);
        Assert.False((await w.GewichtungSetzen("hoch")).Erfolg);

        // Gespeichert wie in der Oberfläche: Nach erneutem Öffnen gelten sie weiter.
        await w.StaffelSchliessen();
        await NormaleRundeOeffnenAsync(kopieren: false);
        Assert.Equal(3, (await w.Gewichtungen(nurGeaenderte: true)).Count);
    }

    [Fact]
    public async Task Spielplandaten_lesen_und_aendern()
    {
        await NormaleRundeOeffnenAsync();
        Spielplandaten daten = Assert.IsType<Spielplandaten>(await w.Spielplandaten());
        Assert.Equal(7, daten.Mannschaften.Count);
        Mannschaftstermine worms = Assert.IsType<Mannschaftstermine>(await w.Mannschaft(Namen.WsvWorms));
        Assert.NotEmpty(worms.Termine);
        Assert.Null(await w.Mannschaft("gibt es nicht"));

        // Termine: Heimtermin, Sperrtermin, entfernen.
        Assert.True((await w.TerminSetzen(Namen.WsvWorms, "02.01.2027", "heim", "19:30", "1")).Erfolg);
        Assert.Contains((await w.Mannschaft(Namen.WsvWorms))!.Termine, t => t.Contains("02.01.2027 19:30 Heimtermin (Lokal 1)", StringComparison.Ordinal));
        Assert.True((await w.TerminSetzen(Namen.WsvWorms, "02.01.2027", "sperre")).Erfolg);
        Assert.Contains((await w.Mannschaft(Namen.WsvWorms))!.Termine, t => t.Contains("02.01.2027 Sperrtermin", StringComparison.Ordinal));
        Assert.True((await w.TerminSetzen(Namen.WsvWorms, "2027-01-02", "entfernen")).Erfolg);
        Assert.DoesNotContain((await w.Mannschaft(Namen.WsvWorms))!.Termine, t => t.Contains("02.01.2027", StringComparison.Ordinal));
        Assert.False((await w.TerminSetzen(Namen.WsvWorms, "02.01.2027", "heim")).Erfolg);
        Assert.False((await w.TerminSetzen(Namen.WsvWorms, "32.01.2027", "sperre")).Erfolg);
        Assert.False((await w.TerminSetzen(Namen.WsvWorms, "02.01.2027", "irgendwas")).Erfolg);
        Assert.False((await w.TerminSetzen("gibt es nicht", "02.01.2027", "sperre")).Erfolg);

        // Spielfreie Tage: nur an möglichen Spieltagen (dem ersten Heimtermin von Worms).
        string heimtag = worms.Termine.First(t => t.Contains("Heimtermin", StringComparison.Ordinal)).Split(' ')[1];
        Assert.True((await w.SpielfreieTage([heimtag])).Erfolg);
        Assert.Contains((await w.Spielplandaten())!.SpielfreieTage, t => t.Contains(heimtag, StringComparison.Ordinal));
        Assert.True((await w.SpielfreieTage([heimtag], eintragen: false)).Erfolg);
        Assert.DoesNotContain((await w.Spielplandaten())!.SpielfreieTage, t => t.Contains(heimtag, StringComparison.Ordinal));
        Assert.False((await w.SpielfreieTage(["kein Tag"])).Erfolg);
        Assert.Contains("keine Mannschaft einen Heimtermin", (await w.SpielfreieTage(["01.01.2020"])).Text, StringComparison.Ordinal);

        // Setzliste: umgekehrte Reihenfolge, aktiv.
        string[] namen = (await w.Spielplandaten())!.Setzliste.Split(": ", 2)[1].Split(", ");
        string[] umgekehrt = [.. namen.Reverse()];
        Assert.True((await w.SetzlisteSetzen(true, umgekehrt)).Erfolg);
        Assert.Equal("aktiv: " + string.Join(", ", umgekehrt), (await w.Spielplandaten())!.Setzliste);
        Assert.False((await w.SetzlisteSetzen(true, umgekehrt[1..])).Erfolg);
        Assert.False((await w.SetzlisteSetzen(true, ["gibt es nicht"])).Erfolg);
        Assert.True((await w.SetzlisteSetzen(false)).Erfolg);
        Assert.StartsWith("aus: ", (await w.Spielplandaten())!.Setzliste, StringComparison.Ordinal);

        // 60-km-Regel.
        string gegner = namen.First(n => n != Namen.WsvWorms);
        Assert.True((await w.NurWochenende(Namen.WsvWorms, [gegner])).Erfolg);
        Assert.Equal([gegner], (await w.Mannschaft(Namen.WsvWorms))!.NurWochenendeGegen);
        Assert.False((await w.NurWochenende(Namen.WsvWorms, ["gibt es nicht"])).Erfolg);

        // Festgelegte Begegnung und Pflichtspielzeit.
        int vorher = (await w.Spielplandaten())!.FestgelegteBegegnungen.Count;
        Aktionsergebnis fest = await w.BegegnungFestlegen(Namen.WsvWorms, gegner, "12.09.2026 18:00");
        Assert.True(fest.Erfolg, fest.Text);
        Assert.Equal(vorher + 1, (await w.Spielplandaten())!.FestgelegteBegegnungen.Count);
        Assert.False((await w.BegegnungFestlegen(Namen.WsvWorms, gegner, "12.09.2026")).Erfolg);
        Assert.True((await w.BegegnungLoeschen(vorher + 1)).Erfolg);
        Assert.False((await w.BegegnungLoeschen(99)).Erfolg);
        Assert.True((await w.Pflichtspielzeit("01.03.2027", "14.03.2027", 1)).Erfolg);
        Assert.Contains((await w.Spielplandaten())!.Pflichtspielzeiten, p => p.Contains("01.03.2027", StringComparison.Ordinal));
        Assert.True((await w.Pflichtspielzeit("01.03.2027", "14.03.2027", 0)).Erfolg);
        Assert.False((await w.Pflichtspielzeit("14.03.2027", "01.03.2027", 1)).Erfolg);
        Assert.False((await w.Pflichtspielzeit("01.03.2027", "14.03.2027", 11)).Erfolg);

        // Nur bei Kompaktspieltagen.
        Assert.False((await w.KompaktMeldung(Namen.WsvWorms, "02.01.2027", "X")).Erfolg);
    }

    [Fact]
    public async Task Mannschaften_Spiellokale_Koppeln_Nachbarn_und_Heimrecht_bearbeiten()
    {
        await NormaleRundeOeffnenAsync();
        string[] namen = (await w.Spielplandaten())!.Setzliste.Split(": ", 2)[1].Split(", ");
        string a = namen.First(n => n != Namen.WsvWorms);
        string b = namen.Last(n => n != Namen.WsvWorms);

        // Mannschaft anlegen, umbenennen, löschen.
        Aktionsergebnis neu = await w.MannschaftSpeichern("Testverein", nummer: 2, id: "123456", vereinsId: "999", spiellokal: "1");
        Assert.True(neu.Erfolg, neu.Text);
        Assert.Equal(8, (await w.Spielplandaten())!.Mannschaften.Count);
        Assert.True((await w.MannschaftSpeichern("Testverein", neuerName: "Testverein II")).Erfolg);
        Assert.Contains("Nummer 2, Mannschafts-ID 123456", (await w.Mannschaft("Testverein II"))!.Stammdaten, StringComparison.Ordinal);
        Assert.False((await w.MannschaftSpeichern("Testverein II", neuerName: Namen.WsvWorms)).Erfolg);
        Assert.True((await w.MannschaftLoeschen("Testverein II")).Erfolg);
        Assert.False((await w.MannschaftLoeschen("Testverein II")).Erfolg);
        Assert.Equal(7, (await w.Spielplandaten())!.Mannschaften.Count);

        // Spiellokal: Standard und eines Heimtermins.
        Mannschaftstermine worms = (await w.Mannschaft(Namen.WsvWorms))!;
        string heimtag = worms.Termine.First(t => t.Contains("Heimtermin", StringComparison.Ordinal)).Split(' ')[1];
        Assert.True((await w.SpiellokalSetzen(Namen.WsvWorms, "2")).Erfolg);
        Assert.Contains("2", (await w.Mannschaft(Namen.WsvWorms))!.Spiellokal, StringComparison.Ordinal);
        Aktionsergebnis lokal = await w.SpiellokalSetzen(Namen.WsvWorms, "3", datum: heimtag);
        Assert.True(lokal.Erfolg, lokal.Text);
        Assert.Contains((await w.Mannschaft(Namen.WsvWorms))!.Termine, t => t.Contains(heimtag, StringComparison.Ordinal) && t.Contains("Lokal 3", StringComparison.Ordinal));
        Assert.False((await w.SpiellokalSetzen(Namen.WsvWorms, "9")).Erfolg);
        Assert.False((await w.SpiellokalSetzen(Namen.WsvWorms, "1", datum: "01.01.2020")).Erfolg);
        Assert.False((await w.SpiellokalSetzen(Namen.WsvWorms, "1", nachbar: "gibt es nicht")).Erfolg);

        // Heimkoppel.
        Aktionsergebnis koppel = await w.HeimkoppelSetzen(Namen.WsvWorms, heimtag, "gewuenscht");
        Assert.True(koppel.Erfolg, koppel.Text);
        Assert.Contains((await w.Mannschaft(Namen.WsvWorms))!.Heimkoppeln, k => k.Contains(heimtag + ": ", StringComparison.Ordinal));
        Assert.True((await w.HeimkoppelSetzen(Namen.WsvWorms, heimtag, "keiner")).Erfolg);
        Assert.Empty((await w.Mannschaft(Namen.WsvWorms))!.Heimkoppeln);
        Assert.False((await w.HeimkoppelSetzen(Namen.WsvWorms, heimtag, "unsinn")).Erfolg);
        Assert.False((await w.HeimkoppelSetzen(Namen.WsvWorms, "01.01.2020", "hoch")).Erfolg);

        // Auswärtskoppel.
        Aktionsergebnis auswaerts = await w.AuswaertskoppelSetzen(Namen.WsvWorms, a, b, "mit Übernachtung");
        Assert.True(auswaerts.Erfolg, auswaerts.Text);
        Assert.Single((await w.Mannschaft(Namen.WsvWorms))!.Auswaertskoppeln);
        Assert.False((await w.AuswaertskoppelSetzen(Namen.WsvWorms, a, b, "unsinn")).Erfolg);
        Assert.False((await w.AuswaertskoppelSetzen(Namen.WsvWorms, a, "gibt es nicht")).Erfolg);
        Assert.True((await w.AuswaertskoppelLoeschen(Namen.WsvWorms, b, a)).Erfolg);
        Assert.Empty((await w.Mannschaft(Namen.WsvWorms))!.Auswaertskoppeln);
        Assert.False((await w.AuswaertskoppelLoeschen(Namen.WsvWorms, b, a)).Erfolg);

        // Nachbarmannschaft mit einem Spiel.
        int nachbarnVorher = (await w.Mannschaft(Namen.WsvWorms))!.Nachbarmannschaften.Count;
        Aktionsergebnis nachbar = await w.NachbarmannschaftSpeichern(Namen.WsvWorms, "Testnachbar", "Damen", nummer: 1, spiellokal: "1", keineParallelenSpiele: true);
        Assert.True(nachbar.Erfolg, nachbar.Text);
        Assert.Equal(nachbarnVorher + 1, (await w.Mannschaft(Namen.WsvWorms))!.Nachbarmannschaften.Count);
        Aktionsergebnis spiel = await w.NachbarspielSetzen(Namen.WsvWorms, "Testnachbar", heimtag + " 19:30", "Gastverein");
        Assert.True(spiel.Erfolg, spiel.Text);
        Assert.Contains((await w.Mannschaft(Namen.WsvWorms))!.Nachbarmannschaften, n => n.Contains(heimtag + " 19:30", StringComparison.Ordinal));
        Assert.False((await w.NachbarspielSetzen(Namen.WsvWorms, "Testnachbar", heimtag)).Erfolg);
        Assert.True((await w.NachbarspielSetzen(Namen.WsvWorms, "Testnachbar", heimtag + " 19:30")).Erfolg);
        Assert.False((await w.NachbarspielSetzen(Namen.WsvWorms, "Testnachbar", heimtag + " 19:30")).Erfolg);
        Assert.False((await w.NachbarmannschaftSpeichern(Namen.WsvWorms, "X", "Damen", bisher: "gibt es nicht")).Erfolg);
        Assert.True((await w.NachbarmannschaftLoeschen(Namen.WsvWorms, "Testnachbar")).Erfolg);
        Assert.Equal(nachbarnVorher, (await w.Mannschaft(Namen.WsvWorms))!.Nachbarmannschaften.Count);
        Assert.False((await w.NachbarmannschaftLoeschen(Namen.WsvWorms, "Testnachbar")).Erfolg);

        // Heimrecht.
        Aktionsergebnis heimrecht = await w.HeimrechtSetzen(Namen.WsvWorms, heimspieleVorrunde: 3, gegner: a, runde: "rueckrunde");
        Assert.True(heimrecht.Erfolg, heimrecht.Text);
        string text = (await w.Mannschaft(Namen.WsvWorms))!.Heimrecht;
        Assert.Contains("Heimspiele in der Vorrunde: 3", text, StringComparison.Ordinal);
        Assert.Contains(a + ": Heimspiel in der Rückrunde", text, StringComparison.Ordinal);
        Assert.True((await w.HeimrechtSetzen(Namen.WsvWorms, heimspieleVorrunde: -1, gegner: a)).Erfolg);
        Assert.Equal("Heimspiele in der Vorrunde: automatisch", (await w.Mannschaft(Namen.WsvWorms))!.Heimrecht);
        Assert.False((await w.HeimrechtSetzen(Namen.WsvWorms, heimspieleVorrunde: -2)).Erfolg);
        Assert.False((await w.HeimrechtSetzen(Namen.WsvWorms, gegner: "gibt es nicht")).Erfolg);
        Assert.False((await w.HeimrechtSetzen(Namen.WsvWorms, gegner: a, runde: "unsinn")).Erfolg);
        Assert.False((await w.HeimrechtSetzen("gibt es nicht")).Erfolg);
    }

    [Fact]
    public async Task Terminplan_und_Diagramme_lesen()
    {
        await Assert.ThrowsAsync<McpException>(() => w.Terminplan());
        await NormaleRundeOeffnenAsync();
        const string ClickTt = "In click-TT vorhandener Plan";

        Terminplan plan = await w.Terminplan(plan: ClickTt);
        Assert.Equal(ClickTt, plan.Plan);
        Assert.Contains(ClickTt, plan.Plaene);
        Assert.NotEmpty(plan.Wochen);
        Assert.EndsWith("Spiele", plan.Zusammenfassung.Split(',')[0], StringComparison.Ordinal);
        Assert.Contains(" – ", plan.Wochen[0].Spiele[0], StringComparison.Ordinal);
        Terminplan worms = await w.Terminplan(Namen.WsvWorms, ClickTt);
        Assert.All(worms.Wochen.SelectMany(x => x.Spiele), sp => Assert.Contains(Namen.WsvWorms, sp, StringComparison.Ordinal));
        Assert.True(worms.Wochen.Sum(x => x.Spiele.Count) < plan.Wochen.Sum(x => x.Spiele.Count));
        await Assert.ThrowsAsync<McpException>(() => w.Terminplan("gibt es nicht", ClickTt));
        await Assert.ThrowsAsync<McpException>(() => w.Terminplan(plan: "gibt es nicht"));

        CallToolResult diagramm = await w.Diagramm("wechsel", ClickTt);
        string text = Assert.IsType<TextContentBlock>(diagramm.Content[0]).Text;
        Assert.StartsWith("Wechsel Heim/Auswärts (" + ClickTt + ")", text, StringComparison.Ordinal);
        Assert.Contains(Namen.WsvWorms + ": ", text, StringComparison.Ordinal);
        ImageContentBlock bild = Assert.IsType<ImageContentBlock>(diagramm.Content[1]);
        Assert.Equal("image/png", bild.MimeType);
        byte[] png = bild.DecodedData.ToArray();
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
        Assert.True(png.Length > 2000);
        await Assert.ThrowsAsync<McpException>(() => w.Diagramm("unsinn", ClickTt));

        // Jedes Diagramm, das es für den Plan gibt, lässt sich zeichnen.
        foreach (string name in text.Split('\n')[1]["Diagramme: ".Length..].Split(", "))
        {
            Assert.IsType<ImageContentBlock>((await w.Diagramm(name, ClickTt)).Content[1]);
        }
    }

    [Fact]
    public async Task Drucken_schreibt_ein_PDF()
    {
        await NormaleRundeOeffnenAsync();
        Assert.False((await w.Drucken(Path.Combine(ordner, "plan.txt"))).Erfolg);
        Assert.False((await w.Drucken(Path.Combine(ordner, "x.pdf"), plan: "gibt es nicht")).Erfolg);
        Assert.False((await w.Drucken(Path.Combine(ordner, "x.pdf"), abschnitte: ["unsinn"])).Erfolg);

        string pdf = Path.Combine(ordner, "plan.pdf");
        Aktionsergebnis gedruckt = await w.Drucken(pdf, plan: "in click-TT vorhandener Plan", abschnitte: ["spielplan", "mannschaftsplaene"], querformat: false);
        Assert.True(gedruckt.Erfolg, gedruckt.Text + string.Join(" ", gedruckt.Stand.Meldungen));
        byte[] inhalt = await File.ReadAllBytesAsync(pdf);
        Assert.Equal("%PDF-"u8.ToArray(), inhalt[..5]);
        Assert.True(inhalt.Length > 10_000);
    }

    [Fact]
    public async Task Kompaktspieltage_berechnen_und_einen_Plan_waehlen()
    {
        Assert.True((await w.StaffelOeffnen(Kompaktklasse.Vorbereiten(ordner))).Erfolg);
        await w.EinrichtungAbschliessen();
        Assert.Equal("Kompaktspieltage", (await w.Status()).Verfahren);
        Assert.False((await w.VerfahrenWaehlen("automodus")).Erfolg);

        Kompaktstand vorher = Assert.IsType<Kompaktstand>(await w.Kompakt());
        string team = (await w.Spielplandaten())!.Mannschaften[0].Split(':')[0];
        Aktionsergebnis gemeldet = await w.KompaktMeldung(team, "13.09.2026", "-");
        Assert.True(gemeldet.Erfolg, gemeldet.Text);
        Assert.Contains((await w.Mannschaft(team))!.Termine, t => t.Contains("13.09.2026 Sperrtermin", StringComparison.Ordinal));
        Assert.Contains("Jugend 19 Testliga", vorher.Kopf, StringComparison.Ordinal);
        Assert.Empty(vorher.Spieltage);

        // Beenden nach der ersten Fortschrittsmeldung: Der Planer liefert die bis dahin besten Pläne.
        Assert.True((await w.Starten()).Erfolg);
        Stand stand = await w.Status();
        for (int i = 0; i < 300 && stand.Laeuft && !stand.Statuszeile.Contains("geplant – ", StringComparison.Ordinal); i++)
        {
            await Task.Delay(100);
            stand = await w.Status();
        }

        if (stand.Laeuft)
        {
            Assert.True((await w.Stoppen()).Erfolg);
        }

        for (int i = 0; i < 60 && (await w.Status()).Laeuft; i++)
        {
            await Task.Delay(500);
        }

        Kompaktstand k = Assert.IsType<Kompaktstand>(await w.Kompakt());
        Assert.False(k.Rechnet);
        List<string> titel = k.Plaene.Select(p => p.TrimStart('▶', ' ').Split(" – ")[0]).Where(t => t != "Plan aus click-TT").ToList();
        Assert.NotEmpty(titel);
        string zweiter = titel.FirstOrDefault(t => !k.Plaene.Any(p => p.StartsWith("▶ " + t, StringComparison.Ordinal))) ?? titel[0];
        Assert.False((await w.KompaktPlanWaehlen("gibt es nicht")).Erfolg);
        Assert.True((await w.KompaktPlanWaehlen(zweiter)).Erfolg);
        Kompaktstand gewaehlt = Assert.IsType<Kompaktstand>(await w.Kompakt());
        Assert.StartsWith("▶ " + zweiter, Assert.Single(gewaehlt.Plaene, p => p.StartsWith('▶')), StringComparison.Ordinal);
        Assert.Equal(zweiter, gewaehlt.Erlaeuterung[0]);
        Assert.NotEmpty(gewaehlt.Spieltage);
        Assert.Null(await w.Qualitaet());
    }

    private async Task NormaleRundeOeffnenAsync(bool kopieren = true)
    {
        string datei = Path.Combine(ordner, "daten", "staffel.xml");
        if (kopieren)
        {
            File.Copy(Path.Combine(Testdaten.Referenz, "datendialoge", "D01_Allgemein.xml"), datei);
        }

        Assert.True((await w.StaffelOeffnen(datei)).Erfolg);
        await w.EinrichtungAbschliessen();
    }
}
