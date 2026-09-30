// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using System.Xml.Linq;

namespace AndiGenerator.Persistence.Tests;

/// <summary>
/// Prüft die Abhängigkeitsrichtung der Schichten (MIGRATIONSPLAN E5) anhand der Projektverweise in den .csproj-Dateien.
/// Bewusst ohne Laden der gebauten DLLs: Die Intelligente App-Steuerung von Windows blockiert auf Peters Rechner
/// neu gebaute, unsignierte Test-DLLs teilweise (eigenes Architektur-Testprojekt wurde dadurch nicht ausführbar).
/// </summary>
public class SchichtenTests
{
    private static readonly Dictionary<string, string[]> Erlaubt = new()
    {
        ["AndiGenerator.Domain"] = [],
        ["AndiGenerator.Engine"] = ["AndiGenerator.Domain"],
        ["AndiGenerator.Persistence"] = ["AndiGenerator.Domain"],
        ["AndiGenerator.Rendering"] = ["AndiGenerator.Domain", "AndiGenerator.Engine"],
        ["AndiGenerator.Application"] = ["AndiGenerator.Domain", "AndiGenerator.Engine", "AndiGenerator.Persistence", "AndiGenerator.Rendering"],
        ["AndiGenerator.Presentation"] = ["AndiGenerator.Domain", "AndiGenerator.Application", "AndiGenerator.Rendering"],
        ["AndiGenerator.UI"] = ["AndiGenerator.Presentation"],
        ["AndiGenerator.Desktop"] = ["AndiGenerator.UI"],
    };

    public static TheoryData<string> Schichten()
    {
        var daten = new TheoryData<string>();
        foreach (string schicht in Erlaubt.Keys)
        {
            daten.Add(schicht);
        }

        return daten;
    }

    [Theory]
    [MemberData(nameof(Schichten))]
    public void Schicht_verweist_nur_auf_erlaubte_Schichten(string schicht)
    {
        string csproj = Path.Combine(Loesungsordner(), "src", schicht, schicht + ".csproj");
        Assert.True(File.Exists(csproj), $"Projektdatei fehlt: {csproj}");

        string[] verweise = XDocument.Load(csproj)
            .Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension(((string?)r.Attribute("Include") ?? string.Empty).Replace('\\', '/')))
            .ToArray();
        string[] verboten = verweise.Where(v => !Erlaubt[schicht].Contains(v)).ToArray();

        Assert.True(verboten.Length == 0, $"{schicht} verweist unerlaubt auf: {string.Join(", ", verboten)}");
    }

    [Fact]
    public void Kernschichten_haben_keine_Paketabhaengigkeiten()
    {
        // Domain, Engine und Rendering bleiben frei von Drittbibliotheken (MIGRATIONSPLAN E5/E11); Skia nur in der UI.
        foreach (string schicht in new[] { "AndiGenerator.Domain", "AndiGenerator.Engine", "AndiGenerator.Rendering" })
        {
            string csproj = Path.Combine(Loesungsordner(), "src", schicht, schicht + ".csproj");
            Assert.Empty(XDocument.Load(csproj).Descendants("PackageReference"));
        }
    }

    [Fact]
    public void Presentation_haengt_nicht_von_Avalonia_ab()
    {
        // ViewModels bleiben ohne Avalonia-Steuerelemente testbar (MIGRATIONSPLAN E5/E15); erlaubt ist das Dock-Modell.
        string csproj = Path.Combine(Loesungsordner(), "src", "AndiGenerator.Presentation", "AndiGenerator.Presentation.csproj");
        string[] pakete = XDocument.Load(csproj)
            .Descendants("PackageReference")
            .Select(r => (string?)r.Attribute("Include") ?? string.Empty)
            .ToArray();
        Assert.DoesNotContain(pakete, p => p.Contains("Avalonia", StringComparison.Ordinal));
    }

    private static string Loesungsordner()
    {
        string testdaten = typeof(SchichtenTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "Testdaten").Value!;

        // Testdaten = <Claude>\Testdaten; die Lösung liegt in <Claude>\Neuer Terminplangenerator (über den Pfad des Testprojekts ermittelt).
        string? loesung = typeof(SchichtenTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "Loesungsordner")?.Value;
        return Path.GetFullPath(loesung ?? Path.Combine(testdaten, "..", "Neuer Terminplangenerator"));
    }
}
