// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using AndiGenerator.UI.Ansichten;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace AndiGenerator.UI;

/// <summary>Ordnet den Ansicht-ViewModels ihre Views zu (explizit, ohne Reflexion).</summary>
public sealed class AnsichtenLocator : IDataTemplate
{
    /// <inheritdoc/>
    public Control? Build(object? param) => param switch
    {
        KostenAnsichtViewModel => new KostenAnsicht(),
        TerminplanAnsichtViewModel => new TerminplanAnsicht(),
        QualitaetAnsichtViewModel => new QualitaetAnsicht(),
        OptionenAnsichtViewModel => new OptionenAnsicht(),
        TerminwunschAnsichtViewModel => new TerminwunschAnsicht(),
        NachbarterminAnsichtViewModel => new NachbarterminAnsicht(),
        DiagrammAnsichtViewModel => new DiagrammAnsicht(),
        MeldungenAnsichtViewModel => new MeldungenAnsicht(),
        _ => null,
    };

    /// <inheritdoc/>
    public bool Match(object? data) => data is KostenAnsichtViewModel or TerminplanAnsichtViewModel or QualitaetAnsichtViewModel or OptionenAnsichtViewModel
        or TerminwunschAnsichtViewModel or NachbarterminAnsichtViewModel or DiagrammAnsichtViewModel or MeldungenAnsichtViewModel;
}
