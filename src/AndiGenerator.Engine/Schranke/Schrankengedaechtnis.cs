// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Concurrent;

namespace AndiGenerator.Engine.Schranke;

/// <summary>
/// Merkt sich je Modell und Ziel die beste bewiesene untere Schranke (solange das Programm läuft). HiGHS beweist in seiner
/// Zeitgrenze nicht immer gleich viel – eine neue Rechnung derselben Staffel mit denselben Optionen und derselben Einteilung soll
/// die Grenze nicht wieder senken: Gilt für dasselbe Modell „mindestens 8“, gilt das auch beim nächsten Mal.
/// </summary>
internal static class Schrankengedaechtnis
{
    private static readonly ConcurrentDictionary<string, int> Schranken = new(StringComparer.Ordinal);

    /// <summary>Merkt sich eine Schranke und gibt die beste bekannte zurück.</summary>
    /// <param name="schluessel">Modell (Fingerabdruck, festgehaltene Stufen) und Ziel.</param>
    /// <param name="schranke">Die gerade berechnete Schranke.</param>
    /// <returns>Die größere aus ihr und der gemerkten.</returns>
    internal static int Merken(string schluessel, int schranke) =>
        Schranken.AddOrUpdate(schluessel, schranke, (_, alt) => Math.Max(alt, schranke));
}
