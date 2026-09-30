// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.ClickTt;

/// <summary>Fehler beim Lesen einer click-TT-Exportdatei.</summary>
public sealed class ClickTtFormatException(string meldung, Exception? ursache = null) : Exception(meldung, ursache);
