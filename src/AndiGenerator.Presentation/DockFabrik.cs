// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace AndiGenerator.Presentation;

/// <summary>
/// Erzeugt das Andock-Layout des Hauptfensters (MIGRATIONSPLAN E15): ein Dokumentbereich mit den Ansichten,
/// die sich als Reiter, nebeneinander oder als eigene Fenster anordnen lassen.
/// </summary>
public sealed class DockFabrik : Factory
{
    private readonly Func<IHostWindow> fensterErzeugen;
    private readonly IReadOnlyList<IDockable> startansichten;
    private IDocumentDock? dokumente;

    /// <summary>Initialisiert die Fabrik.</summary>
    /// <param name="fensterErzeugen">Erzeugt ein Fenster für herausgelöste Ansichten (kommt aus der Oberfläche).</param>
    /// <param name="startansichten">Ansichten des Startlayouts; mindestens eine.</param>
    public DockFabrik(Func<IHostWindow> fensterErzeugen, IReadOnlyList<IDockable> startansichten)
    {
        ArgumentNullException.ThrowIfNull(fensterErzeugen);
        ArgumentNullException.ThrowIfNull(startansichten);
        ArgumentOutOfRangeException.ThrowIfZero(startansichten.Count);
        this.fensterErzeugen = fensterErzeugen;
        this.startansichten = startansichten;
    }

    /// <inheritdoc/>
    public override IRootDock CreateLayout()
    {
        var dock = new DocumentDock
        {
            Id = "Ansichten",
            Title = "Ansichten",
            IsCollapsable = false,
            CanCreateDocument = false,
            VisibleDockables = CreateList(startansichten.ToArray()),
            ActiveDockable = startansichten[0],
        };

        IRootDock wurzel = CreateRootDock();
        wurzel.Id = "Wurzel";
        wurzel.IsCollapsable = false;
        wurzel.VisibleDockables = CreateList<IDockable>(dock);
        wurzel.ActiveDockable = dock;
        wurzel.DefaultDockable = dock;
        dokumente = dock;
        return wurzel;
    }

    /// <inheritdoc/>
    public override void InitLayout(IDockable layout)
    {
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => fensterErzeugen(),
        };
        DefaultHostWindowLocator = () => fensterErzeugen();
        base.InitLayout(layout);
    }

    /// <summary>Fügt eine Ansicht dem Dokumentbereich hinzu und aktiviert sie.</summary>
    /// <param name="ansicht">Die Ansicht.</param>
    public void AnsichtHinzufuegen(IDockable ansicht)
    {
        ArgumentNullException.ThrowIfNull(ansicht);
        IDocumentDock ziel = dokumente ?? throw new InvalidOperationException("Das Layout wurde noch nicht erzeugt.");
        AddDockable(ziel, ansicht);
        SetActiveDockable(ansicht);
        SetFocusedDockable(ziel, ansicht);
    }
}
