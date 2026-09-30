// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;

namespace AndiGenerator.Cli;

/// <summary>Schreibt die Konsolenausgabe zusätzlich in eine Datei (Option <c>--ausgabe</c>).</summary>
internal sealed class DoppelSchreiber(TextWriter konsole, TextWriter datei) : TextWriter
{
    public override Encoding Encoding => konsole.Encoding;

    public override void Write(char value)
    {
        konsole.Write(value);
        datei.Write(value);
    }

    public override void Write(string? value)
    {
        konsole.Write(value);
        datei.Write(value);
    }

    public override void WriteLine(string? value)
    {
        konsole.WriteLine(value);
        datei.WriteLine(value);
        datei.Flush();
    }

    public override void Flush()
    {
        konsole.Flush();
        datei.Flush();
    }
}
