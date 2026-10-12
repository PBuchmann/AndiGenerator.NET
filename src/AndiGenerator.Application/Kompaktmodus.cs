// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Ob eine Staffel mit Kompaktspieltagen geplant wird (nicht im Original).</summary>
public enum Kompaktmodus
{
    /// <summary>Noch nicht gefragt.</summary>
    Offen,

    /// <summary>Eingeschaltet; beim nächsten Öffnen wird noch einmal gefragt (zum Zurücknehmen).</summary>
    Ein,

    /// <summary>Eingeschaltet und bestätigt.</summary>
    Bestaetigt,

    /// <summary>Ausgeschaltet: normale Planung.</summary>
    Aus,
}
