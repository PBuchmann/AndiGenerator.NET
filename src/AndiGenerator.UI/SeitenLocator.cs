// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using AndiGenerator.UI.Seiten;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace AndiGenerator.UI;

/// <summary>Ordnet den Seiten des Dialogs „Spielplandaten bearbeiten“ ihre Views zu (explizit, ohne Reflexion).</summary>
public sealed class SeitenLocator : IDataTemplate
{
    /// <inheritdoc/>
    public Control? Build(object? param) => param switch
    {
        LigadatenSeiteViewModel => new LigadatenSeite(),
        MannschaftenSeiteViewModel => new MannschaftenSeite(),
        SetzlistenSeiteViewModel => new SetzlistenSeite(),
        MehrMannschaftenSeiteViewModel => new MehrMannschaftenSeite(),
        SpiellokalDetailViewModel => new SpiellokalDetail(),
        HeimkoppelDetailViewModel => new HeimkoppelDetail(),
        AuswaertskoppelDetailViewModel => new AuswaertskoppelDetail(),
        WunschterminDetailViewModel => new WunschterminDetail(),
        NachbarmannschaftDetailViewModel => new NachbarmannschaftDetail(),
        WochenendeDetailViewModel => new WochenendeDetail(),
        SpielfreieTageSeiteViewModel => new SpielfreieTageSeite(),
        PflichtspieltageSeiteViewModel => new PflichtspieltageSeite(),
        VorgabespieleSeiteViewModel => new VorgabespieleSeite(),
        HeimrechtDetailViewModel => new HeimrechtDetail(),
        _ => null,
    };

    /// <inheritdoc/>
    public bool Match(object? data) => data is DatenSeiteViewModel;
}
