// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Headless;

namespace AndiGenerator.UI.Tests;

/// <summary>
/// Die echte Anwendung (Stile, Schriften, Ansichten-Zuordnung), aber auf der Headless-Plattform ohne Bildschirm. Gezeichnet
/// wird wirklich mit Skia, damit auch Fehler in den eigenen Zeichenflächen (Diagramme, Terminwunschraster) auffallen.
/// </summary>
public static class TestAnwendung
{
    /// <summary>Konfiguriert die Anwendung; aufgerufen von <see cref="HeadlessUnitTestSession"/>.</summary>
    /// <returns>Der Builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
