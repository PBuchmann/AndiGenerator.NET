// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace AndiGenerator.Application;

/// <summary>
/// Protokoll unerwarteter Fehler im eigenen Datenordner (<c>fehlerprotokoll.txt</c>): Zeitpunkt, wobei der Fehler auftrat,
/// Programmversion, Betriebssystem und der vollständige Fehler. Das Original kennt so etwas nicht; dort beendet ein
/// unerwarteter Fehler das Programm ohne Spur. Die Datei bleibt auf dem Rechner, bis der Anwender sie selbst weitergibt.
/// </summary>
public static class Fehlerprotokoll
{
    /// <summary>Dateiname des Protokolls im eigenen Datenordner.</summary>
    public const string Dateiname = "fehlerprotokoll.txt";

    /// <summary>Name der Datei, in die ein zu groß gewordenes Protokoll verschoben wird.</summary>
    public const string AlterDateiname = "fehlerprotokoll.alt.txt";

    /// <summary>Ab dieser Größe (Bytes) beginnt ein neues Protokoll; das alte bleibt als <see cref="AlterDateiname"/>.</summary>
    public const long Hoechstgroesse = 1_000_000;

    /// <summary>Wohin Anwender Fehler melden.</summary>
    public const string Fehlermeldungsadresse = "https://github.com/PBuchmann/AndiGenerator.NET/issues";

    /// <summary>Hängt einen Fehler an das Protokoll an.</summary>
    /// <param name="basis">Eigener Datenordner (<see cref="Plansitzung.EigeneBasis"/>).</param>
    /// <param name="fehler">Der Fehler.</param>
    /// <param name="wobei">Wobei er auftrat, z. B. „Oberfläche“ oder „Generierung“.</param>
    /// <returns>Pfad des Protokolls.</returns>
    /// <exception cref="IOException">Das Protokoll ließ sich nicht schreiben.</exception>
    /// <exception cref="UnauthorizedAccessException">Kein Schreibrecht im Datenordner.</exception>
    public static string Schreiben(string basis, Exception fehler, string wobei)
    {
        ArgumentException.ThrowIfNullOrEmpty(basis);
        ArgumentNullException.ThrowIfNull(fehler);
        Directory.CreateDirectory(basis);
        string pfad = Path.Combine(basis, Dateiname);
        if (File.Exists(pfad) && new FileInfo(pfad).Length > Hoechstgroesse)
        {
            File.Move(pfad, Path.Combine(basis, AlterDateiname), overwrite: true);
        }

        string version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unbekannt";
        string eintrag = string.Create(
            CultureInfo.InvariantCulture,
            $"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} · {wobei} · Version {version} · {RuntimeInformation.OSDescription}{Environment.NewLine}{fehler}{Environment.NewLine}{Environment.NewLine}");
        File.AppendAllText(pfad, eintrag, Encoding.UTF8);
        return pfad;
    }

    /// <summary>Meldung nach einem unerwarteten Fehler: was geschah, was gesichert wurde und wo das Protokoll liegt.</summary>
    /// <param name="fehler">Der Fehler.</param>
    /// <param name="gesichert">Name des gemerkten Plans mit der Notfallsicherung oder <c>null</c>.</param>
    /// <param name="protokoll">Pfad des Fehlerprotokolls oder <c>null</c>.</param>
    /// <returns>Der Text.</returns>
    public static string Meldung(Exception fehler, string? gesichert, string? protokoll)
    {
        ArgumentNullException.ThrowIfNull(fehler);
        var text = new StringBuilder();
        text.Append("Es ist ein unerwarteter Fehler aufgetreten: ").Append(fehler.Message).AppendLine().AppendLine();
        if (gesichert is not null)
        {
            text.Append("Der aktuelle Plan wurde als gemerkter Plan „").Append(gesichert)
                .Append("“ gesichert. Sie finden ihn unter „Start mit“ und in der Planauswahl der Ansichten.").AppendLine().AppendLine();
        }

        if (protokoll is not null)
        {
            text.Append("Einzelheiten stehen in ").Append(protokoll).Append(". Bitte schicken Sie diese Datei mit einer kurzen Beschreibung, ")
                .Append("was Sie gerade getan haben, an die Entwickler (").Append(Fehlermeldungsadresse).Append(").").AppendLine().AppendLine();
        }

        text.Append("Sie können weiterarbeiten. Sicherer ist es, den Plan zu exportieren oder zu merken und das Programm neu zu starten.");
        return text.ToString();
    }
}
