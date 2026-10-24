// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

// Schreibt zu den Beispielklassen aus mytischtennis.py die Hallenadressen in die .modifications-Datei daneben – wie ein
// übernommener Bericht „Terminwünsche“, aber nur mit Postleitzahl und Ort der Vereine (für Karte und Entfernungen der
// Kompaktspieltage). Hallen und Straßen sind erfunden; Heimtermine und Sperren bleiben wie in der XML-Datei.
// Aufruf im Repository (nach python3 tools/kompaktklasse/mytischtennis.py): dotnet run tools/kompaktklasse/mytischtennis-adressen.cs
#:project ../../src/AndiGenerator.Application/AndiGenerator.Application.csproj
#:project ../../src/AndiGenerator.Persistence/AndiGenerator.Persistence.csproj

using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Persistence.Terminwuensche;

string[] dateien =
[
    Path.GetFullPath("src/AndiGenerator.Desktop/Beispiele/Jugend 19 Hessenliga (HR).xml"),
    Path.GetFullPath("src/AndiGenerator.Desktop/Beispiele/Jugend 19 NRW-Liga 1 (HR).xml"),
];
var orte = new Dictionary<string, (string Plz, string Ort)>(StringComparer.Ordinal)
{
    ["Sporthalle Staffel"] = ("65556", "Limburg an der Lahn"),
    ["Sporthalle Eschersheim"] = ("60433", "Frankfurt am Main"),
    ["Sporthalle Bergen-Enkheim"] = ("60388", "Frankfurt am Main"),
    ["Sporthalle Darmstadt"] = ("64283", "Darmstadt"),
    ["Sporthalle Bad Homburg"] = ("61348", "Bad Homburg vor der Höhe"),
    ["Sporthalle Lampertheim"] = ("68623", "Lampertheim"),
    ["Sporthalle Niederrad"] = ("60528", "Frankfurt am Main"),
    ["Sporthalle Allendorf"] = ("35469", "Allendorf (Lumda)"),
    ["Sporthalle Langen"] = ("63225", "Langen (Hessen)"),
    ["Sporthalle Neuenhain"] = ("65812", "Bad Soden am Taunus"),
    ["Sporthalle Rauxel"] = ("44575", "Castrop-Rauxel"),
    ["Sporthalle Velbert"] = ("42551", "Velbert"),
    ["Sporthalle Hahlen"] = ("32427", "Minden"),
    ["Sporthalle Bönen"] = ("59199", "Bönen"),
    ["Sporthalle Ronsdorf"] = ("42369", "Wuppertal"),
    ["Sporthalle Bottrop"] = ("46236", "Bottrop"),
    ["Sporthalle Stiepel"] = ("44797", "Bochum"),
};

foreach (string datei in dateien)
{
    AdressenSchreiben(datei);
}

void AdressenSchreiben(string datei)
{
    File.Delete(Plansitzung.ModifikationenPfad(datei));
    string basis = Path.Combine(Path.GetTempPath(), "andigen-beispiel-" + Guid.NewGuid().ToString("N"));
    Plansitzung sitzung = Plansitzung.Oeffnen(datei, basis);
    Datenbearbeitung b = sitzung.DatenBearbeiten();
    var meldungen = new List<Vereinsmeldung>();
    foreach (Mannschaft m in sitzung.Staffel.Mannschaften)
    {
        string halle = m.Heimspieltermine.Select(t => t.Halle).First(h => h.Length > 0);
        (string plz, string ort) = orte[halle];
        meldungen.Add(new Vereinsmeldung(
            m.Name,
            int.Parse(m.VereinsId, System.Globalization.CultureInfo.InvariantCulture),
            m.Nummer > 1 ? "Jugend 19 " + new string('I', m.Nummer) : "Jugend 19",
            new Dictionary<int, Spiellokaladresse> { [1] = new(halle, string.Empty, plz, ort) },
            string.Empty,
            m.Heimspieltermine.Select(t => new GemeldeterHeimtermin(t.Zeitpunkt, halle, "2", "-")).ToList(),
            m.Sperrtermine));
    }

    b.TerminwuenscheUebernehmen(b.TerminwuenscheAbgleichen(meldungen), "Hallenadressen (erfunden)");
    sitzung.DatenUebernehmen(b);
    if (Directory.Exists(basis))
    {
        Directory.Delete(basis, recursive: true);
    }

    Console.WriteLine(Plansitzung.ModifikationenPfad(datei) + ": " + b.Hallenkoordinaten().Count + " Hallen mit Lage");
}
