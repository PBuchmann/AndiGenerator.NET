// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Antwort auf die Frage, ob der gesicherte Plan durch den aktuellen ersetzt werden soll.</summary>
public enum Sicherungswahl
{
    /// <summary>Abbrechen: Der Neustart bzw. das Schließen unterbleibt, der aktuelle Plan bleibt erhalten.</summary>
    Abbrechen,

    /// <summary>Den aktuellen Plan sichern und damit den bisher gesicherten ersetzen.</summary>
    Ueberschreiben,

    /// <summary>Den bisher gesicherten Plan behalten; der aktuelle Plan wird verworfen.</summary>
    Behalten,
}
