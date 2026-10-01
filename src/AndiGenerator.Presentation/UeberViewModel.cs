// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using AndiGenerator.Application;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>
/// Dialog „Über“: Programm und Version, Urheber des Originals und der Portierung, wie die Portierung entstanden ist,
/// und die Rechtshinweise, die die GPL-3.0 (Abschnitt 5d, „Appropriate Legal Notices“) für interaktive Programme verlangt.
/// </summary>
public sealed class UeberViewModel
{
    /// <summary>Adresse des Lizenztexts.</summary>
    public const string Lizenzadresse = "https://www.gnu.org/licenses/gpl-3.0.html";

    private readonly IOberflaeche oberflaeche;
    private readonly string? basis;
    private bool updatesSuchen = true;

    /// <summary>Initialisiert den Dialog ohne Programmeinstellungen.</summary>
    /// <param name="oberflaeche">Dienste der Oberfläche (Lizenz im Browser öffnen).</param>
    /// <param name="version">Anzuzeigende Programmversion.</param>
    public UeberViewModel(IOberflaeche oberflaeche, string version)
        : this(oberflaeche, version, null)
    {
    }

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="oberflaeche">Dienste der Oberfläche (Lizenz im Browser öffnen, Fehler melden).</param>
    /// <param name="version">Anzuzeigende Programmversion.</param>
    /// <param name="basis">Eigener Ordner mit den Programmeinstellungen oder <c>null</c> (dann ohne Einstellung).</param>
    public UeberViewModel(IOberflaeche oberflaeche, string version, string? basis)
    {
        ArgumentNullException.ThrowIfNull(oberflaeche);
        this.oberflaeche = oberflaeche;
        this.basis = basis;
        Version = "Version " + version;
        LizenzAnzeigenCommand = new AsyncRelayCommand(() => this.oberflaeche.AdresseOeffnenAsync(new Uri(Lizenzadresse)));
        if (basis is not null)
        {
            try
            {
                updatesSuchen = Programmeinstellungen.Laden(basis).UpdatesSuchen;
            }
            catch (IOException)
            {
                updatesSuchen = Programmeinstellungen.Standard.UpdatesSuchen;
            }
        }
    }

    /// <summary>Holt den Programmnamen.</summary>
    public static string Programm => "AndiGenerator.NET";

    /// <summary>Holt die Kurzbeschreibung.</summary>
    public static string Beschreibung => "Terminplangenerator für Tischtennis-Staffeln (click-TT)";

    /// <summary>Holt den Hinweis auf das Original und seinen Urheber.</summary>
    public static string Original =>
        "Idee und Herzstück stammen von Andreas Hofmann, der den AndiGenerator (Delphi, Copyright © 2017 ff.) entwickelt hat: "
        + "die Suche nach dem besten Plan mit mehreren parallel optimierenden „Inseln“, die Kostenfunktion mit allen Regeln, "
        + "Kostenarten und Gewichtungen, der Import aus click-TT samt Änderungsdateien, die Auswertung der Terminwünsche und "
        + "Nachbarmannschaften, die Diagramme sowie die Plan- und Optionsdateien. Sein fachliches Wissen darüber, was einen "
        + "guten Spielplan ausmacht, steckt unverändert in AndiGenerator.NET – Rechenweg und Ergebnisse entsprechen dem Original.";

    /// <summary>Holt den Urheber der Portierung.</summary>
    public static string Portierung => "C#-Portierung und neue Oberfläche: Copyright © 2026 Peter Buchmann";

    /// <summary>Holt die Beschreibung, wie die Portierung entstanden ist.</summary>
    public static string Entstehung =>
        "AndiGenerator.NET ist eine Portierung des AndiGenerators auf C#/.NET und Avalonia und ist 2026 in Zusammenarbeit mit dem KI-Assistenten Claude (Anthropic) "
        + "entstanden: Analyse des Delphi-Quelltexts, Migrationsplan, schrittweise Umsetzung in Phasen und Abgleich mit dem "
        + "Original durch Paritätstests (gleiche Kosten, gleiche Dateien). Die Oberfläche ist dabei neu gestaltet worden "
        + "(Einrichtungsseite, Kostenmatrix mit direkter Gewichtung, Karten, Touch-Bedienung). Fehlerbehebungen gegenüber dem Original sind in "
        + "Doku/MIGRATIONSPLAN.md dokumentiert, der Ablauf in Doku/ENTSTEHUNG.md.";

    /// <summary>Holt den Lizenz- und Gewährleistungshinweis (GPL-3.0, Abschnitt 5d).</summary>
    public static string Lizenzhinweis =>
        "Dieses Programm ist freie Software: Sie können es unter den Bedingungen der GNU General Public License, Version 3, "
        + "wie von der Free Software Foundation veröffentlicht, weitergeben und/oder verändern.\n\n"
        + "Es wird in der Hoffnung verbreitet, dass es nützlich ist, aber OHNE JEDE GEWÄHRLEISTUNG – sogar ohne die "
        + "implizite Gewährleistung der MARKTFÄHIGKEIT oder der EIGNUNG FÜR EINEN BESTIMMTEN ZWECK. Einzelheiten stehen in "
        + "der GNU General Public License.";

    /// <summary>Holt den Hinweis auf die verwendeten Bibliotheken.</summary>
    public static string Drittbibliotheken =>
        "Verwendet Avalonia, Dock, CommunityToolkit.Mvvm, SkiaSharp und Velopack (jeweils MIT-Lizenz) sowie die Schriften IBM Plex Sans und Mono "
        + "(SIL Open Font License 1.1); Einzelheiten in THIRD-PARTY-NOTICES.md.";

    /// <summary>Holt die Programmversion.</summary>
    public string Version { get; }

    /// <summary>Holt den Befehl, der den Lizenztext im Browser öffnet.</summary>
    public IAsyncRelayCommand LizenzAnzeigenCommand { get; }

    /// <summary>Holt, ob die Programmeinstellung angezeigt wird (nur mit eigenem Ordner).</summary>
    public bool HatEinstellungen => basis is not null;

    /// <summary>
    /// Holt oder setzt, ob das Programm beim Start auf GitHub nach einer neuen Version sucht; wird sofort gespeichert.
    /// </summary>
    public bool UpdatesSuchen
    {
        get => updatesSuchen;
        set
        {
            if (value == updatesSuchen || basis is null)
            {
                return;
            }

            updatesSuchen = value;
            try
            {
                new Programmeinstellungen(value).Speichern(basis);
            }
            catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
            {
                _ = oberflaeche.MeldenAsync("Einstellung speichern", "Die Einstellung konnte nicht gespeichert werden:" + Environment.NewLine + fehler.Message);
            }
        }
    }

    /// <summary>Ermittelt die Version einer Programmdatei (ohne angehängten Quelltext-Stand).</summary>
    /// <param name="assembly">Die Programmdatei.</param>
    /// <returns>Die Version, z. B. <c>1.0.0</c>.</returns>
    public static string VersionVon(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString(3);
        return version is null ? "unbekannt" : version.Split('+')[0];
    }
}
