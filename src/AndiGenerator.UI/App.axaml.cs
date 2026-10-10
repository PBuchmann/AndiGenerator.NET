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
            var oberflaeche = new AvaloniaOberflaeche(fenster);
            var modell = new HauptfensterViewModel(oberflaeche, () => new HostWindow());
            fenster.DataContext = modell;
            desktop.MainWindow = fenster;

            // Beim Schließen des Hauptfensters beenden, auch wenn herausgelöste Ansichten oder (unsichtbare) Hilfsfenster
            // des Andock-Layouts noch offen sind; sonst bleibt der Prozess samt Rechen-Threads im Hintergrund stehen.
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.Exit += (_, _) => modell.Dispose();
            fenster.Opened += (_, _) => Aktualisierung.Starten(oberflaeche);
            if (OperatingSystem.IsMacOS())
            {
                NativeMenu.SetMenu(this, Programmmenue(modell));
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Programmmenü des Macs (oben links in der Menüleiste, unter dem Programmnamen): „Über“ und die Anleitung statt der
    /// Vorgabe von Avalonia. Beenden, Ausblenden usw. ergänzt macOS selbst.
    /// </summary>
    /// <param name="modell">Das Hauptfenster mit den Befehlen.</param>
    /// <returns>Das Menü.</returns>
    private static NativeMenu Programmmenue(HauptfensterViewModel modell)
    {
        var menue = new NativeMenu();
        menue.Items.Add(new NativeMenuItem("Über AndiGenerator.NET") { Command = modell.UeberCommand });
        menue.Items.Add(new NativeMenuItem("Anleitung") { Command = modell.AnleitungCommand });
        return menue;
    }
}
