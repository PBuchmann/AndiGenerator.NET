// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Dock.Avalonia.Controls;

namespace AndiGenerator.UI;

/// <summary>Avalonia-Anwendung: Themes, Ansichten-Zuordnung und Hauptfenster.</summary>
public partial class App : global::Avalonia.Application
{
    /// <inheritdoc/>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Bildschirmfoto.Anmelden();
            var fenster = new HauptfensterView();
            var modell = new HauptfensterViewModel(new AvaloniaOberflaeche(fenster), () => new HostWindow());
            fenster.DataContext = modell;
            desktop.MainWindow = fenster;

            // Beim Schließen des Hauptfensters beenden, auch wenn herausgelöste Ansichten oder (unsichtbare) Hilfsfenster
            // des Andock-Layouts noch offen sind; sonst bleibt der Prozess samt Rechen-Threads im Hintergrund stehen.
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.Exit += (_, _) => modell.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
