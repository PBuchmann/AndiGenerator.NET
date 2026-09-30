// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Gemeinsam;

/// <summary>Ein gemerkter Plan im Staffelordner.</summary>
/// <param name="Name">Anzeigename (dekodierter Dateiname ohne Endung).</param>
/// <param name="Pfad">Vollständiger Dateipfad.</param>
/// <param name="Geaendert">Letzte Änderung der Datei (Sortierung: neueste zuerst).</param>
public sealed record GemerkterPlanEintrag(string Name, string Pfad, DateTime Geaendert);
