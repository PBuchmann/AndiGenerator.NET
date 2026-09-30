// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Ein Hinweis des Erststart-Assistenten (Original <c>TDialogFirstStart.UpdateTexts</c>).</summary>
/// <param name="Punkt">Der Punkt; bestimmt die Seite, die „Bearbeiten“ öffnet.</param>
/// <param name="Titel">Überschrift, z. B. <c>Spiellokale</c>.</param>
/// <param name="Text">Erläuterung (Absätze durch Zeilenumbrüche getrennt).</param>
/// <param name="Aktion">Beschriftung der Schaltfläche, z. B. <c>Spiellokale bearbeiten</c>.</param>
public sealed record Erststarthinweis(Erststartpunkt Punkt, string Titel, string Text, string Aktion);
