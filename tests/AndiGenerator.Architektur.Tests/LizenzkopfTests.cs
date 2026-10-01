// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Architektur.Tests;

/// <summary>Jede Quelldatei nennt Urheber und Lizenz maschinenlesbar (REUSE, LIZENZ.md, CONTRIBUTING.md).</summary>
public sealed class LizenzkopfTests
{
    private const int Kopfzeilen = 5;

    [Fact]
    public void Jede_Quelldatei_hat_den_SPDX_Kopf()
    {
        string[] ohne = new[] { "src", "tests", "tools" }
            .Select(o => Path.Combine(Schichten.Loesungsordner, o))
            .Where(Directory.Exists)
            .SelectMany(o => Directory.EnumerateFiles(o, "*.cs", SearchOption.AllDirectories))
            .Where(d => !Erzeugt(d))
            .Where(d => !KopfVollstaendig(File.ReadLines(d).Take(Kopfzeilen).ToArray()))
            .Select(d => Path.GetRelativePath(Schichten.Loesungsordner, d))
            .ToArray();

        Assert.True(ohne.Length == 0, "Ohne vollständigen SPDX-Kopf: " + string.Join(", ", ohne));
    }

    private static bool KopfVollstaendig(string[] kopf) =>
        kopf.Any(z => z.StartsWith("// SPDX-FileCopyrightText: ", StringComparison.Ordinal))
        && kopf.Any(z => z == "// SPDX-License-Identifier: GPL-3.0-only");

    private static bool Erzeugt(string datei)
    {
        string pfad = datei.Replace('\\', '/');
        return pfad.Contains("/obj/", StringComparison.Ordinal) || pfad.Contains("/bin/", StringComparison.Ordinal);
    }
}
