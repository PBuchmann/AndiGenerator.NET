// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Concurrent;
using Avalonia.Logging;

namespace AndiGenerator.UI.Tests;

/// <summary>
/// Sammelt die Meldungen von Avalonia während der Tests, z. B. Bindungsfehler oder Ausnahmen beim Layout. Fehler lassen
/// die Tests scheitern, Warnungen werden nur ausgegeben.
/// </summary>
internal sealed class Protokoll : ILogSink
{
    private readonly ConcurrentQueue<(LogEventLevel Stufe, string Bereich, string Text)> eintraege = new();

    /// <summary>Holt die gesammelten Meldungen.</summary>
    public IReadOnlyCollection<(LogEventLevel Stufe, string Bereich, string Text)> Eintraege => eintraege;

    /// <inheritdoc/>
    public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning;

    /// <inheritdoc/>
    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
        Log(level, area, source, messageTemplate, []);

    /// <inheritdoc/>
    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        if (IsEnabled(level, area))
        {
            string werte = propertyValues.Length == 0 ? string.Empty : " [" + string.Join(", ", propertyValues) + "]";
            eintraege.Enqueue((level, area, messageTemplate + werte + " (" + source?.GetType().Name + ")"));
        }
    }

    /// <summary>Leert das Protokoll.</summary>
    public void Leeren() => eintraege.Clear();
}
