// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Plandaten;

/// <summary>Fehler im Plandaten-Format (z.B. unbekannter Knotenname, doppelter Schlüssel).</summary>
public sealed class PlanDatenFormatException(string meldung, Exception? ursache = null) : Exception(meldung, ursache);
