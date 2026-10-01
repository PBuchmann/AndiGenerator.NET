// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace AndiGenerator.Architektur.Tests;

/// <summary>
/// Metadaten einer gebauten Schicht: verwendete Assemblies, verwendete Fremdtypen und eigene Typen. Gelesen mit
/// System.Reflection.Metadata, ohne die DLL zu laden – so spielen fehlende Abhängigkeiten keine Rolle.
/// </summary>
internal sealed class Baustein
{
    private Baustein(IReadOnlyList<string> verweise, IReadOnlyList<string> fremdtypen, IReadOnlyList<(string Namensraum, string Name)> typen)
    {
        Verweise = verweise;
        Fremdtypen = fremdtypen;
        Typen = typen;
    }

    /// <summary>Holt die Namen der verwendeten Assemblies.</summary>
    public IReadOnlyList<string> Verweise { get; }

    /// <summary>Holt die verwendeten Typen anderer Assemblies als <c>Namensraum.Name</c>.</summary>
    public IReadOnlyList<string> Fremdtypen { get; }

    /// <summary>Holt die eigenen Typen der obersten Ebene.</summary>
    public IReadOnlyList<(string Namensraum, string Name)> Typen { get; }

    /// <summary>Liest die gebaute DLL einer Schicht aus dem Testordner.</summary>
    /// <param name="schicht">Die Schicht.</param>
    /// <returns>Die Metadaten.</returns>
    public static Baustein Laden(string schicht)
    {
        string pfad = Path.Combine(AppContext.BaseDirectory, Schichten.Dateiname(schicht));
        Assert.True(File.Exists(pfad), $"Gebaute Datei fehlt: {pfad}");
        using FileStream datei = File.OpenRead(pfad);
        using var pe = new PEReader(datei);
        MetadataReader leser = pe.GetMetadataReader();

        List<string> verweise = leser.AssemblyReferences
            .Select(h => leser.GetString(leser.GetAssemblyReference(h).Name))
            .ToList();
        List<string> fremdtypen = leser.TypeReferences
            .Select(h => leser.GetTypeReference(h))
            .Select(t => leser.GetString(t.Namespace) + "." + leser.GetString(t.Name))
            .ToList();
        List<(string Namensraum, string Name)> typen = leser.TypeDefinitions
            .Select(h => leser.GetTypeDefinition(h))
            .Where(t => !t.IsNested)
            .Select(t => (leser.GetString(t.Namespace), leser.GetString(t.Name)))
            .ToList();
        return new Baustein(verweise, fremdtypen, typen);
    }
}
