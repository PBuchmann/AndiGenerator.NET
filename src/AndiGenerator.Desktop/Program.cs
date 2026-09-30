// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.UI;
using Avalonia;

namespace AndiGenerator.Desktop;

/// <summary>Programmstart der Desktop-Anwendung.</summary>
internal static class Program
{
    /// <summary>Einstiegspunkt.</summary>
    /// <param name="args">Befehlszeilenargumente.</param>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Konfiguriert Avalonia (auch vom XAML-Previewer der IDE verwendet).</summary>
    /// <returns>Der konfigurierte Builder.</returns>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
