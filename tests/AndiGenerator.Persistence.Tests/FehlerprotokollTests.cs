// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Protokoll unerwarteter Fehler und die Meldung dazu.</summary>
public sealed class FehlerprotokollTests : IDisposable
{
    private readonly string basis = Path.Combine(Path.GetTempPath(), "andigen-fehler-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(basis))
        {
            Directory.Delete(basis, recursive: true);
        }
    }

    [Fact]
    public void Fehler_werden_angehaengt_und_ein_zu_grosses_Protokoll_wird_beiseitegelegt()
    {
        string pfad = Fehlerprotokoll.Schreiben(basis, new InvalidOperationException("erster Fehler"), "Oberfläche");
        Fehlerprotokoll.Schreiben(basis, new InvalidOperationException("zweiter Fehler"), "Generierung");
        string inhalt = File.ReadAllText(pfad);
        Assert.Contains("· Oberfläche ·", inhalt, StringComparison.Ordinal);
        Assert.Contains("erster Fehler", inhalt, StringComparison.Ordinal);
        Assert.Contains("· Generierung ·", inhalt, StringComparison.Ordinal);
        Assert.Contains("zweiter Fehler", inhalt, StringComparison.Ordinal);

        File.AppendAllText(pfad, new string('x', (int)Fehlerprotokoll.Hoechstgroesse));
        Fehlerprotokoll.Schreiben(basis, new InvalidOperationException("dritter Fehler"), "Programm");
        Assert.True(File.Exists(Path.Combine(basis, Fehlerprotokoll.AlterDateiname)));
        string neu = File.ReadAllText(pfad);
        Assert.Contains("dritter Fehler", neu, StringComparison.Ordinal);
        Assert.DoesNotContain("erster Fehler", neu, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Meldung_nennt_Fehler_Notfallsicherung_und_Protokoll()
    {
        string meldung = Fehlerprotokoll.Meldung(new InvalidOperationException("kaputt"), "Notfallsicherung", @"C:\Daten\fehlerprotokoll.txt");
        Assert.Contains("kaputt", meldung, StringComparison.Ordinal);
        Assert.Contains("„Notfallsicherung“", meldung, StringComparison.Ordinal);
        Assert.Contains(@"C:\Daten\fehlerprotokoll.txt", meldung, StringComparison.Ordinal);
        Assert.Contains(Fehlerprotokoll.Fehlermeldungsadresse, meldung, StringComparison.Ordinal);

        string knapp = Fehlerprotokoll.Meldung(new InvalidOperationException("kaputt"), null, null);
        Assert.DoesNotContain("gesichert", knapp, StringComparison.Ordinal);
        Assert.DoesNotContain("Einzelheiten", knapp, StringComparison.Ordinal);
    }
}
