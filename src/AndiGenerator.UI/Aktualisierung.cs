// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Velopack;
using Velopack.Sources;

namespace AndiGenerator.UI;

/// <summary>
/// Sucht nach dem Start auf GitHub Releases nach einer neuen Version (Velopack, MIGRATIONSPLAN E12), lädt sie im
/// Hintergrund und fragt dann, ob sie gleich eingespielt werden soll; sonst geschieht das beim Beenden. Nur in einer
/// installierten Version, nicht beim Start aus der Entwicklungsumgebung.
/// </summary>
internal static class Aktualisierung
{
    private const string Repository = "https://github.com/PBuchmann/AndiGenerator.NET";
    private static readonly TimeSpan Verzoegerung = TimeSpan.FromSeconds(5);

    /// <summary>Startet die Suche, ohne den Programmstart aufzuhalten.</summary>
    /// <param name="oberflaeche">Für die Rückfrage.</param>
    public static void Starten(IOberflaeche oberflaeche) => _ = PruefenAsync(oberflaeche);

    private static async Task PruefenAsync(IOberflaeche oberflaeche)
    {
        try
        {
            var verwaltung = new UpdateManager(new GithubSource(Repository, null, false));
            if (!verwaltung.IsInstalled)
            {
                return;
            }

            await Task.Delay(Verzoegerung);
            UpdateInfo? neu = await verwaltung.CheckForUpdatesAsync();
            if (neu is null)
            {
                return;
            }

            await verwaltung.DownloadUpdatesAsync(neu);
            string version = neu.TargetFullRelease.Version.ToString();
            string frage = $"AndiGenerator.NET {version} ist bereit. Jetzt neu starten und aktualisieren?"
                + Environment.NewLine + Environment.NewLine
                + "Eine laufende Generierung wird dabei beendet; nicht gemerkte Pläne gehen verloren. "
                + "Mit „Abbrechen“ wird die neue Version beim nächsten Beenden eingespielt.";
            bool jetzt = await oberflaeche.FragenAsync("Neue Version", frage);
            if (jetzt)
            {
                verwaltung.ApplyUpdatesAndRestart(neu.TargetFullRelease);
            }
            else
            {
                await verwaltung.WaitExitThenApplyUpdatesAsync(neu.TargetFullRelease, silent: true, restart: false);
            }
        }
        catch (Exception fehler) when (fehler is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException)
        {
            // Ohne Netz oder bei gesperrtem Zugriff gibt es eben kein Update, das Programm arbeitet normal weiter.
            System.Diagnostics.Debug.WriteLine("Updateprüfung: " + fehler.Message);
        }
    }
}
