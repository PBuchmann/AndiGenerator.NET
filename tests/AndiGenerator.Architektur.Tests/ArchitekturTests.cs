// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Architektur.Tests;

/// <summary>
/// Architekturregeln, geprüft an den gebauten DLLs (MIGRATIONSPLAN E5/E11/E15): Was tatsächlich im übersetzten Code
/// verwendet wird, nicht nur, was in den Projektdateien steht.
/// </summary>
public sealed class ArchitekturTests
{
    private static readonly string[] Oberflaechenbibliotheken = ["Avalonia", "SkiaSharp", "HarfBuzzSharp", "Dock.Avalonia", "Velopack"];

    // Zugriffe auf Dateien, Netz, Prozesse und Konsole gehören nicht in den Rechenkern, sondern in Persistence und darüber.
    private static readonly string[] VerboteneTypen =
    [
        "System.IO.File", "System.IO.Directory", "System.IO.FileInfo", "System.IO.DirectoryInfo", "System.IO.FileStream",
        "System.IO.StreamReader", "System.IO.StreamWriter", "System.Diagnostics.Process", "System.Console",
    ];

    private static readonly string[] VerboteneNamensraeume = ["System.Net."];

    // Vom Compiler oder vom XAML-Compiler erzeugte Typen, deren Namensraum nicht dem Projekt folgt.
    private static readonly string[] ErzeugteNamensraeume = ["CompiledAvaloniaXaml", "XamlIl", "Avalonia", "Microsoft.", "System."];

    public static TheoryData<string> Alle() => Schichten.Alle();

    public static TheoryData<string> NurDotNet() => Schichten.Daten(Schichten.NurDotNet);

    public static TheoryData<string> Rechenkern() => Schichten.Daten(Schichten.Rechenkern);

    public static TheoryData<string> OhneOberflaeche() =>
        Schichten.Daten(Schichten.Erlaubt.Keys.Where(s => s is not "AndiGenerator.UI" and not "AndiGenerator.Desktop"));

    [Theory]
    [MemberData(nameof(Alle))]
    public void Schicht_verwendet_nur_tieferliegende_Schichten(string schicht)
    {
        HashSet<string> erlaubt = Schichten.Huelle(schicht);
        string[] verboten = Baustein.Laden(schicht).Verweise
            .Where(v => v.StartsWith("AndiGenerator.", StringComparison.Ordinal) && !erlaubt.Contains(v))
            .ToArray();

        Assert.True(verboten.Length == 0, $"{schicht} verwendet höher- oder gleichrangige Schichten: {string.Join(", ", verboten)}");
    }

    [Theory]
    [MemberData(nameof(NurDotNet))]
    public void Kernschichten_verwenden_nur_DotNet_selbst(string schicht)
    {
        string[] fremd = Baustein.Laden(schicht).Verweise
            .Where(v => !v.StartsWith("AndiGenerator.", StringComparison.Ordinal) && !IstDotNet(v))
            .ToArray();

        Assert.True(fremd.Length == 0, $"{schicht} verwendet Drittbibliotheken: {string.Join(", ", fremd)}");
    }

    [Theory]
    [MemberData(nameof(OhneOberflaeche))]
    public void Nur_die_Oberflaeche_kennt_Avalonia_Skia_und_Velopack(string schicht)
    {
        string[] verboten = Baustein.Laden(schicht).Verweise
            .Where(v => Oberflaechenbibliotheken.Any(o => v == o || v.StartsWith(o + ".", StringComparison.Ordinal)))
            .ToArray();

        Assert.True(verboten.Length == 0, $"{schicht} verwendet Oberflächenbibliotheken: {string.Join(", ", verboten)}");
    }

    [Theory]
    [MemberData(nameof(Rechenkern))]
    public void Rechenkern_greift_nicht_auf_Dateien_Netz_und_Prozesse_zu(string schicht)
    {
        string[] verboten = Baustein.Laden(schicht).Fremdtypen
            .Where(t => VerboteneTypen.Contains(t) || VerboteneNamensraeume.Any(n => t.StartsWith(n, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.True(verboten.Length == 0, $"{schicht} verwendet: {string.Join(", ", verboten)}");
    }

    [Theory]
    [MemberData(nameof(Alle))]
    public void Namensraeume_folgen_dem_Projekt(string schicht)
    {
        string[] falsch = Baustein.Laden(schicht).Typen
            .Where(t => t.Namensraum.Length > 0 && !t.Name.Contains('<', StringComparison.Ordinal))
            .Where(t => !ErzeugteNamensraeume.Any(e => t.Namensraum.StartsWith(e, StringComparison.Ordinal)))
            .Where(t => t.Namensraum != schicht && !t.Namensraum.StartsWith(schicht + ".", StringComparison.Ordinal))
            .Select(t => t.Namensraum + "." + t.Name)
            .ToArray();

        Assert.True(falsch.Length == 0, $"Typen außerhalb des Namensraums {schicht}: {string.Join(", ", falsch)}");
    }

    [Fact]
    public void ViewModels_liegen_in_der_Presentation()
    {
        foreach (string schicht in Schichten.Erlaubt.Keys.Where(s => s != "AndiGenerator.Presentation"))
        {
            string[] falsch = Baustein.Laden(schicht).Typen
                .Where(t => t.Name.EndsWith("ViewModel", StringComparison.Ordinal))
                .Select(t => t.Namensraum + "." + t.Name)
                .ToArray();
            Assert.True(falsch.Length == 0, $"ViewModels in {schicht}: {string.Join(", ", falsch)}");
        }
    }

    private static bool IstDotNet(string assembly) =>
        assembly is "System" or "netstandard" or "mscorlib" or "Microsoft.CSharp"
        || assembly.StartsWith("System.", StringComparison.Ordinal)
        || assembly.StartsWith("Microsoft.Win32.", StringComparison.Ordinal);
}
