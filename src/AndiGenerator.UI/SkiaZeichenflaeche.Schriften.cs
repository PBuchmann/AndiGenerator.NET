// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Platform;

namespace AndiGenerator.UI;

/// <summary>Schriften des PDF-Ausdrucks in der Oberfläche: aus den Avalonia-Ressourcen.</summary>
internal sealed partial class SkiaZeichenflaeche
{
    /// <summary>Ordner der Schriften in den Avalonia-Ressourcen.</summary>
    private const string Schriftordner = "avares://AndiGenerator.UI/Assets/Fonts/";

    private static partial Stream SchriftOeffnen(string datei) => AssetLoader.Open(new Uri(Schriftordner + datei));
}
