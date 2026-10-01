// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Xml.Linq;

namespace AndiGenerator.Architektur.Tests;

/// <summary>
/// Prüft die Abhängigkeitsrichtung der Schichten (MIGRATIONSPLAN E5) an den Projektdateien: Jede Schicht verweist direkt
/// nur auf die erlaubten Schichten. Ergänzt <see cref="ArchitekturTests"/>, die den übersetzten Code prüfen.
/// </summary>
public sealed class ProjektverweisTests
{
    public static TheoryData<string> Alle() => Schichten.Alle();

    [Theory]
    [MemberData(nameof(Alle))]
    public void Schicht_verweist_nur_auf_erlaubte_Schichten(string schicht)
    {
        string[] verweise = Projekt(schicht)
            .Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension(((string?)r.Attribute("Include") ?? string.Empty).Replace('\\', '/')))
            .ToArray();
        string[] verboten = verweise.Where(v => !Schichten.Erlaubt[schicht].Contains(v)).ToArray();

        Assert.True(verboten.Length == 0, $"{schicht} verweist unerlaubt auf: {string.Join(", ", verboten)}");
    }

    [Fact]
    public void Rechenkern_hat_keine_Paketabhaengigkeiten()
    {
        // Domain, Engine und Rendering bleiben frei von Drittbibliotheken (MIGRATIONSPLAN E5/E11), Skia nur in der UI.
        foreach (string schicht in Schichten.Rechenkern)
        {
            Assert.Empty(Projekt(schicht).Descendants("PackageReference"));
        }
    }

    [Fact]
    public void Presentation_haengt_nicht_von_Avalonia_ab()
    {
        // ViewModels bleiben ohne Avalonia-Steuerelemente testbar (MIGRATIONSPLAN E5/E15), erlaubt ist das Dock-Modell.
        string[] pakete = Projekt("AndiGenerator.Presentation")
            .Descendants("PackageReference")
            .Select(r => (string?)r.Attribute("Include") ?? string.Empty)
            .ToArray();
        Assert.DoesNotContain(pakete, p => p.Contains("Avalonia", StringComparison.Ordinal));
    }

    private static XDocument Projekt(string schicht)
    {
        string csproj = Path.Combine(Schichten.Loesungsordner, "src", schicht, schicht + ".csproj");
        Assert.True(File.Exists(csproj), $"Projektdatei fehlt: {csproj}");
        return XDocument.Load(csproj);
    }
}
